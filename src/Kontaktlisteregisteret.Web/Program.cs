using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;
using Kontaktlisteregisteret.Web.Services;
using Kontaktlisteregisteret.Web.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Bytt til PostgreSQL: sett Database:Provider=postgresql og ConnectionStrings:Postgresql i appsettings
// For ny migrasjon mot PostgreSQL: dotnet ef migrations add <Namn> -- om nødvendig med --output-dir Migrations/Pg
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "sqlite";
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (dbProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
    {
        var pgConn = builder.Configuration.GetConnectionString("Postgresql")
            ?? "Host=localhost;Database=kontaktliste;Username=postgres;Password=postgres";
        opt.UseNpgsql(pgConn);
    }
    else
    {
        opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=kontaktliste.db");
    }
});

if (builder.Configuration.GetValue<bool>("Tenor:Enabled"))
{
    builder.Services.AddHttpClient<IBrregService, TenorBrregService>(c =>
    {
        c.BaseAddress = new Uri("https://tenor.test.brreg.no");
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.Timeout = TimeSpan.FromSeconds(15);
    });
}
else
{
    builder.Services.AddHttpClient<IBrregService, BrregService>(c =>
    {
        c.BaseAddress = new Uri("https://data.brreg.no");
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.Timeout = TimeSpan.FromSeconds(15);
    });
}

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<SsbKlassService>(c =>
{
    c.BaseAddress = new Uri("https://data.ssb.no");
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<TargetGroupService>();
builder.Services.AddScoped<AdresselisteService>();
builder.Services.AddScoped<AbonnementslisteService>();
builder.Services.AddScoped<VirksomhetService>();
builder.Services.AddScoped<VirksomhetContext>();
builder.Services.AddScoped<VarslingsService>();

var app = builder.Build();

// ── Database setup + seed ────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Kjør ventende migrasjoner — oppretter databasen om den ikke finnes ennå
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Ikke slett en eksisterende database dersom migrering feiler. En feil her kan
        // skyldes blant annet feil tilkoblingsstreng, manglende rettigheter eller en
        // uforenlig migrasjon og må håndteres eksplisitt av operatøren.
        app.Logger.LogCritical(ex, "Kunne ikke migrere databasen. Oppstart avbrytes uten å endre eksisterende data.");
        throw;
    }
    if (!app.Configuration.GetValue<bool>("SkipSeed"))
        await SeedAsync(db);
}

// ── API v1 ────────────────────────────────────────────────────────────────────
// Maskinporten-token med scope digdir:kontaktliste.read kreves i produksjon.
// I PoC er autentisering ikke aktivert.

var api = app.MapGroup("/api/v1");

// GET /api/v1/adresselister — liste over låste adresselister
api.MapGet("/adresselister", async (AppDbContext db) =>
{
    var lister = await db.Adresselister
        .Where(a => a.Status == AdresselisteStatus.Låst)
        .Include(a => a.Mottakere)
        .OrderByDescending(a => a.LåstAt)
        .ToListAsync();

    return Results.Ok(lister.Select(a => new
    {
        id = a.Id,
        tittel = a.Tittel,
        beskrivelse = a.Beskrivelse,
        status = a.Status.ToString().ToLower(),
        antallMottakere = a.Mottakere.Count,
        låstAt = a.LåstAt,
        opprettetAt = a.OpprettetAt
    }));
});

// GET /api/v1/adresselister/{id} — metadata for én låst liste
api.MapGet("/adresselister/{id:int}", async (int id, AppDbContext db) =>
{
    var a = await db.Adresselister
        .Where(x => x.Id == id && x.Status == AdresselisteStatus.Låst)
        .Include(x => x.Mottakere)
        .FirstOrDefaultAsync();

    if (a is null) return Results.NotFound(new { error = "Adresselisten finnes ikke eller er ikke låst." });

    return Results.Ok(new
    {
        id = a.Id,
        tittel = a.Tittel,
        beskrivelse = a.Beskrivelse,
        status = a.Status.ToString().ToLower(),
        antallMottakere = a.Mottakere.Count,
        låstAt = a.LåstAt,
        opprettetAt = a.OpprettetAt
    });
});

// GET /api/v1/adresselister/{id}/mottakere — snapshot-mottakere
api.MapGet("/adresselister/{id:int}/mottakere", async (int id, AppDbContext db) =>
{
    var finnes = await db.Adresselister
        .AnyAsync(a => a.Id == id && a.Status == AdresselisteStatus.Låst);

    if (!finnes) return Results.NotFound(new { error = "Adresselisten finnes ikke eller er ikke låst." });

    var mottakere = await db.AdresselisteMottakere
        .Where(m => m.AdresselisteId == id)
        .Include(m => m.Recipient)
        .OrderBy(m => m.Recipient.Name)
        .ToListAsync();

    return Results.Ok(mottakere.Select(m => new
    {
        organisasjonsnummer = m.Recipient.ExternalId,
        navn = m.Visningsnavn ?? m.Recipient.Name,
        brregNavn = m.Visningsnavn is not null ? m.Recipient.Name : null,
        coAdresse = m.CoAdresse,
        type = m.Recipient.Type.ToString().ToLower(),
        orgForm = m.Recipient.OrgForm,
        postadresse = m.Recipient.PostalAddress is not null ? new
        {
            adresse = m.Recipient.PostalAddress,
            postnummer = m.Recipient.PostalCode,
            poststed = m.Recipient.PostalCity
        } : null,
        kildeMålgruppeId = m.KildeMålgruppeId
    }));
});

// GET /api/v1/abonnementslister — liste over alle abonnementslister
api.MapGet("/abonnementslister", async (AppDbContext db) =>
{
    var lister = await db.Abonnementslister
        .Include(l => l.Abonnenter)
        .OrderByDescending(l => l.OpprettetAt)
        .ToListAsync();
    return Results.Ok(lister.Select(l => new
    {
        id = l.Id,
        navn = l.Navn,
        beskrivelse = l.Beskrivelse,
        antallAbonnenter = l.Abonnenter.Count,
        opprettetAt = l.OpprettetAt
    }));
});

// GET /api/v1/abonnementslister/{id}/abonnenter — abonnenter for én liste
api.MapGet("/abonnementslister/{id:int}/abonnenter", async (int id, AppDbContext db) =>
{
    if (!await db.Abonnementslister.AnyAsync(l => l.Id == id))
        return Results.NotFound(new { error = "Abonnementslisten finnes ikke." });

    var abonnenter = await db.Abonnenter
        .Where(a => a.AbonnementslisteId == id)
        .OrderBy(a => a.Epost)
        .ToListAsync();

    return Results.Ok(abonnenter.Select(a => new
    {
        id = a.Id,
        epost = a.Epost,
        lagtTilAt = a.LagtTilAt,
        kilde = a.Kilde.ToString().ToLower()
    }));
});

// POST /api/v1/abonnementslister/{id}/abonnenter — legg til abonnent i liste
// Body: { "epost": "navn@eksempel.no" }
api.MapPost("/abonnementslister/{id:int}/abonnenter",
    async (int id, AbonnentRegistrerRequest req, AbonnementslisteService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.Epost))
        return Results.BadRequest(new { error = "E-post er påkrevd." });

    var (ok, error, abonnent) = await svc.LeggTilAsync(id, req.Epost, AbonnentKilde.Api);
    if (!ok) return Results.Conflict(new { error });

    return Results.Created($"/api/v1/abonnementslister/{id}/abonnenter/{abonnent!.Id}", new
    {
        id = abonnent.Id,
        epost = abonnent.Epost,
        lagtTilAt = abonnent.LagtTilAt
    });
});

// DELETE /api/v1/abonnenter/{id} — fjern abonnent (uavhengig av liste)
api.MapDelete("/abonnenter/{id:int}", async (int id, AbonnementslisteService svc) =>
{
    var ok = await svc.SlettAbonnentAsync(id);
    return ok ? Results.NoContent() : Results.NotFound(new { error = "Abonnenten finnes ikke." });
});

// ── API v1: målgrupper ────────────────────────────────────────────────────────
// Alle 4xx/5xx-svar bruker application/problem+json (RFC 9457).

// GET /api/v1/malgrupper — alle målgrupper på tvers av virksomheter
api.MapGet("/malgrupper", async (TargetGroupService svc) =>
    Results.Ok((await svc.GetAllAsync()).Select(MålgruppeShape)));

// GET /api/v1/malgrupper/{id}
api.MapGet("/malgrupper/{id:int}", async (int id, TargetGroupService svc) =>
{
    var g = await svc.GetAsync(id);
    return g is null
        ? Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet")
        : Results.Ok(MålgruppeShape(g));
});

// GET /api/v1/malgrupper/{id}/medlemmer?page=1&size=50
api.MapGet("/malgrupper/{id:int}/medlemmer", async (int id, int? page, int? size, TargetGroupService svc) =>
{
    var g = await svc.GetAsync(id);
    if (g is null)
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");

    var p = Math.Max(1, page ?? 1);
    var s = Math.Clamp(size ?? 50, 1, 200);
    var alle = g.Members.OrderBy(m => m.Recipient.Name).ToList();
    var items = alle.Skip((p - 1) * s).Take(s).Select(m => new
    {
        id = m.Id,
        organisasjonsnummer = m.Recipient.ExternalId,
        navn = m.Visningsnavn ?? m.Recipient.Name,
        brregNavn = m.Visningsnavn is not null ? m.Recipient.Name : null,
        orgForm = m.Recipient.OrgForm,
        naceKode = m.Recipient.NaceCode,
        coAdresse = m.CoAdresse
    });
    return Results.Ok(new { items, page = p, size = s, totalCount = alle.Count });
});

// GET /api/v1/malgrupper/{id}/eksport.json — JSON-filnedlasting
api.MapGet("/malgrupper/{id:int}/eksport.json", async (int id, TargetGroupService svc) =>
{
    try
    {
        var bytes = await svc.ExportJsonAsync(id);
        return Results.File(bytes, "application/json", $"malgruppe-{id}.json");
    }
    catch (KeyNotFoundException)
    {
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");
    }
});

// GET /api/v1/malgrupper/{id}/eksport.csv — CSV-filnedlasting
api.MapGet("/malgrupper/{id:int}/eksport.csv", async (int id, TargetGroupService svc) =>
{
    try
    {
        var bytes = await svc.ExportCsvAsync(id);
        return Results.File(bytes, "text/csv; charset=utf-8", $"malgruppe-{id}.csv");
    }
    catch (KeyNotFoundException)
    {
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");
    }
});

// POST /api/v1/malgrupper — opprett statisk eller dynamisk målgruppe
// For Statisk: validerer orgnr mot Brreg og legger til de som finnes (Ok).
// For Dynamisk: kjører umiddelbart SyncDynamicGroupAsync — kan ta 10–30 s.
api.MapPost("/malgrupper", async (OpprettMålgruppeRequest req, TargetGroupService svc, IBrregService brreg) =>
{
    if (string.IsNullOrWhiteSpace(req.Navn))
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { { "navn", ["Navn er påkrevd."] } },
            type: "https://kontaktlisteregisteret.no/problems/validering");

    var scope = ParseScope(req.Scope);
    if (scope is null)
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { { "scope", ["Ugyldig verdi. Gyldige verdier: \"Privat\", \"Delt\"."] } },
            type: "https://kontaktlisteregisteret.no/problems/validering");

    TargetGroup gruppe;

    switch (req.Type)
    {
        case "Statisk":
        {
            if (req.Orgnr is null or { Count: 0 })
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { { "orgnr", ["Orgnr-liste er påkrevd for type Statisk."] } },
                    type: "https://kontaktlisteregisteret.no/problems/validering");

            var valideringer = await brreg.ValidateOrgnrListAsync(req.Orgnr);
            var ugyldigFormat = valideringer.Where(v => v.Status == ValidationStatus.InvalidFormat).ToList();
            if (ugyldigFormat.Count > 0)
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        { "orgnr", ugyldigFormat.Select(u => $"{u.Orgnr}: ugyldig format (ikke 9 siffer).").ToArray() }
                    },
                    type: "https://kontaktlisteregisteret.no/problems/validering");

            // Orgnr med NotFound/Deleted inkluderes ikke, men gir heller ikke feil —
            // lar kalleren sjekke antallMedlemmer i responsen for å oppdage frafall.
            var recipients = valideringer
                .Where(v => v.Status == ValidationStatus.Ok)
                .Select(v => TargetGroupService.BrregEnhetToRecipient(v.Enhet!))
                .ToList();

            gruppe = await svc.CreateStaticAsync(req.Navn.Trim(), scope.Value, recipients);
            break;
        }
        case "Dynamisk":
        {
            var criteria = (req.Kriterier
                ?? new DynamicCriteriaDto(null, null, null, null, null, null, null))
                .TilIntern();
            gruppe = await svc.CreateDynamicAsync(req.Navn.Trim(), scope.Value, criteria);
            break;
        }
        default:
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    { "type", ["Ugyldig verdi. Gyldige verdier: \"Statisk\", \"Dynamisk\"."] }
                },
                type: "https://kontaktlisteregisteret.no/problems/validering");
    }

    var nyGruppe = await svc.GetAsync(gruppe.Id);
    return Results.Created($"/api/v1/malgrupper/{gruppe.Id}", MålgruppeShape(nyGruppe!));
});

// PATCH /api/v1/malgrupper/{id} — endre navn
api.MapPatch("/malgrupper/{id:int}", async (int id, NavnEndreRequest req, TargetGroupService svc, AppDbContext db) =>
{
    if (!await db.TargetGroups.AnyAsync(g => g.Id == id))
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");

    if (string.IsNullOrWhiteSpace(req.Navn))
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { { "navn", ["Navn kan ikke være tomt."] } },
            type: "https://kontaktlisteregisteret.no/problems/validering");

    await svc.UpdateNameAsync(id, req.Navn);
    return Results.NoContent();
});

// PUT /api/v1/malgrupper/{id}/kriterier — oppdater filterregler og resynkroniser mot Brreg
// OBS: Kallet kan ta 10–30 s — SyncDynamicGroupAsync henter alle sider fra Brreg sekvensiell.
api.MapPut("/malgrupper/{id:int}/kriterier", async (int id, DynamicCriteriaDto dto, TargetGroupService svc) =>
{
    var g = await svc.GetAsync(id);
    if (g is null)
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");

    if (g.Type != TargetGroupType.Dynamic)
        return Results.Problem(title: "Målgruppen er ikke dynamisk", statusCode: 400,
            detail: $"Målgruppe {id} er av type Statisk og har ingen kriterier å oppdatere.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-dynamisk");

    g.DynamicCriteriaJson = System.Text.Json.JsonSerializer.Serialize(dto.TilIntern());
    await svc.SaveCriteriaAsync(g);
    await svc.SyncDynamicGroupAsync(g);

    var oppdatert = await svc.GetAsync(id);
    return Results.Ok(new { antallMedlemmer = oppdatert!.Members.Count });
});

// DELETE /api/v1/malgrupper/{id}
// Blokkeres med 409 hvis målgruppen er koblet til en låst adresseliste —
// snapshotet er immutabelt, men sletting ville gitt inkonsistente oppslag.
api.MapDelete("/malgrupper/{id:int}", async (int id, AppDbContext db) =>
{
    var g = await db.TargetGroups.FindAsync(id);
    if (g is null)
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");

    var kobletTilLåst = await db.AdresselisteMålgrupper
        .Where(m => m.MålgruppeId == id)
        .Join(db.Adresselister, m => m.AdresselisteId, a => a.Id, (m, a) => a.Status)
        .AnyAsync(s => s == AdresselisteStatus.Låst);

    if (kobletTilLåst)
        return Results.Problem(title: "Målgruppen er koblet til en låst adresseliste",
            statusCode: 409,
            detail: $"Målgruppe {id} kan ikke slettes — én eller flere låste adresselister refererer til den.",
            type: "https://kontaktlisteregisteret.no/problems/referert-av-låst-liste");

    db.TargetGroups.Remove(g);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ── Admin API: virksomheter ──────────────────────────────────────────────────
// Krever Maskinporten-scope digdir:kontaktliste.admin i produksjon.
// I PoC er autentisering ikke aktivert.

var adminApi = app.MapGroup("/api/v1/admin");

adminApi.MapGet("/virksomheter", async (VirksomhetService svc) =>
{
    var virksomheter = await svc.GetAllAsync();
    return Results.Ok(virksomheter.Select(v => new
    {
        id = v.Id,
        orgnr = v.Orgnr,
        navn = v.Navn,
        status = v.Status.ToString().ToLower(),
        onboardetAt = v.OnboardetAt,
        onboardetAv = v.OnboardetAv
    }));
});

adminApi.MapPost("/virksomheter", async (VirksomhetOnboardRequest req, VirksomhetService svc) =>
{
    var (v, error) = await svc.OnboardAsync(req.Orgnr, req.Navn, req.OnboardetAv);
    if (error is not null) return Results.Conflict(new { error });
    return Results.Created($"/api/v1/admin/virksomheter/{v!.Orgnr}", new
    {
        id = v.Id,
        orgnr = v.Orgnr,
        navn = v.Navn,
        status = v.Status.ToString().ToLower(),
        onboardetAt = v.OnboardetAt
    });
});

adminApi.MapDelete("/virksomheter/{id:int}", async (int id, VirksomhetService svc) =>
{
    var ok = await svc.SlettAsync(id);
    return ok ? Results.NoContent() : Results.NotFound(new { error = "Virksomheten finnes ikke." });
});

// ── Web ───────────────────────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// ── Seed ─────────────────────────────────────────────────────────────────────

static async Task SeedAsync(AppDbContext db)
{
    // Kjør bare én gang
    if (await db.Virksomheter.AnyAsync()) return;

    // ── Demo-virksomhet: Digitaliseringsdirektoratet ─────────────────────────
    var digdir = new Virksomhet
    {
        Orgnr = "991825827",
        Navn = "Digitaliseringsdirektoratet",
        Status = VirksomhetStatus.Aktiv,
        OnboardetAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        OnboardetAv = "Seed"
    };
    db.Virksomheter.Add(digdir);
    await db.SaveChangesAsync();

    // ── Hjelpefunksjon: legg til Recipient hvis ExternalId ikke finnes ───────
    async Task<Recipient> UpsertRecipient(string externalId, string name, string orgForm)
    {
        var r = await db.Recipients.FirstOrDefaultAsync(x => x.ExternalId == externalId);
        if (r is not null) return r;
        r = new Recipient
        {
            Type = RecipientType.Organization,
            ExternalId = externalId,
            Name = name,
            OrgForm = orgForm,
            IsActive = true
        };
        db.Recipients.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    async Task<TargetGroup> CreateStaticGroup(string name, List<Recipient> recipients, int virksomhetId)
    {
        var g = new TargetGroup
        {
            Name = name,
            Type = TargetGroupType.Static,
            Scope = TargetGroupScope.Shared,
            VirksomhetId = virksomhetId
        };
        db.TargetGroups.Add(g);
        await db.SaveChangesAsync();
        foreach (var r in recipients)
            db.TargetGroupMembers.Add(new TargetGroupMember { TargetGroupId = g.Id, RecipientId = r.Id });
        await db.SaveChangesAsync();
        return g;
    }

    // ── Departementene ───────────────────────────────────────────────────────
    var departementer = new (string orgnr, string navn)[]
    {
        ("972417777", "Statsministerens kontor"),
        ("983887457", "Arbeids- og inkluderingsdepartementet"),
        ("972417793", "Barne- og familiedepartementet"),
        ("972417807", "Finansdepartementet"),
        ("972417823", "Forsvarsdepartementet"),
        ("983887406", "Helse- og omsorgsdepartementet"),
        ("972417831", "Justis- og beredskapsdepartementet"),
        ("972417882", "Klima- og miljødepartementet"),
        ("972417858", "Kommunal- og distriktsdepartementet"),
        ("972417866", "Kultur- og likestillingsdepartementet"),
        ("872417842", "Kunnskapsdepartementet"),
        ("972417874", "Landbruks- og matdepartementet"),
        ("912660680", "Nærings- og fiskeridepartementet"),
        ("977161630", "Energidepartementet"),
        ("972417904", "Samferdselsdepartementet"),
        ("972417920", "Utenriksdepartementet"),
    };
    var deptRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in departementer)
        deptRecipients.Add(await UpsertRecipient(orgnr, navn, "STAT"));
    var gruppeDept = await CreateStaticGroup("Departementene", deptRecipients, digdir.Id);

    // ── Statsforvaltere ──────────────────────────────────────────────────────
    var statsforvaltere = new (string orgnr, string navn)[]
    {
        ("974761319", "Statsforvalteren i Østfold, Buskerud, Oslo og Akershus"),
        ("974761645", "Statsforvalteren i Innlandet"),
        ("974762501", "Statsforvalteren i Vestfold og Telemark"),
        ("974762994", "Statsforvalteren i Agder"),
        ("974763230", "Statsforvaltaren i Rogaland"),
        ("974760665", "Statsforvaltaren i Vestland"),
        ("974764067", "Statsforvaltaren i Møre og Romsdal"),
        ("974764350", "Statsforvalteren i Trøndelag"),
        ("974764687", "Statsforvalteren i Nordland"),
        ("967311014", "Statsforvalteren i Troms og Finnmark"),
    };
    var sfRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in statsforvaltere)
        sfRecipients.Add(await UpsertRecipient(orgnr, navn, "STAT"));
    var gruppeSF = await CreateStaticGroup("Statsforvaltere", sfRecipients, digdir.Id);

    // ── Statlige etater og direktorater ─────────────────────────────────────
    var etater = new (string orgnr, string navn)[]
    {
        ("889640782", "Arbeids- og velferdsetaten (NAV)"),
        ("974761211", "Arbeidstilsynet"),
        ("986128433", "Barne-, ungdoms- og familiedirektoratet (Bufdir)"),
        ("971527765", "Barneombudet"),
        ("974761467", "Datatilsynet"),
        ("986252932", "Direktoratet for forvaltning og økonomistyring (DFØ)"),
        ("926721720", "Domstoladministrasjonen"),
        ("983744516", "Folkehelseinstituttet"),
        ("871033382", "Forbrukerrådet"),
        ("983544622", "Helsedirektoratet"),
        ("974761394", "Statens helsetilsyn"),
        ("942114184", "Husbanken"),
        ("987879696", "Integrerings- og mangfoldsdirektoratet (IMDi)"),
        ("818066872", "Den norske kirke"),
        ("974761246", "Konkurransetilsynet"),
        ("988681873", "Likestillings- og diskrimineringsombudet"),
        ("984047851", "Longyearbyen lokalstyre"),
        ("960885406", "Statens lånekasse for utdanning"),
        ("970141669", "Norges forskningsråd"),
        ("974761378", "Regjeringsadvokaten"),
        ("974760843", "Riksrevisjonen"),
        ("974760347", "Samediggi / Sametinget"),
        ("974761270", "Sivilombudet"),
        ("974761076", "Skatteetaten"),
        ("971526920", "Statistisk sentralbyrå"),
        ("970018131", "Utdanningsdirektoratet"),
        ("974760746", "Utlendingsdirektoratet"),
    };
    var etaterRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in etater)
        etaterRecipients.Add(await UpsertRecipient(orgnr, navn, "STAT"));
    var gruppeEtater = await CreateStaticGroup("Statlige etater og direktorater", etaterRecipients, digdir.Id);

    // ── Interesseorganisasjoner ──────────────────────────────────────────────
    // Alle orgnr er verifisert mot Brreg Enhetsregisteret API.
    // Forum for Barnekonvensjonen (sekretariat hos Redd Barna 941296459) —
    // sett Visningsnavn manuelt i UI etter opprettelse.
    var orger = new (string orgnr, string navn)[]
    {
        ("971474475", "Adopsjonsforum"),
        ("971493224", "Aleneforeldreforeningen"),
        ("970148698", "Amnesty International Norge"),
        ("970168907", "ANSA – Association of Norwegian Students Abroad"),
        ("962323855", "Blå Kors Norge"),
        ("985702314", "Buddhistforbundet"),
        ("971436514", "Caritas Norge"),
        ("820140362", "Den katolske kirke i Norge"),
        ("981070151", "Elevorganisasjonen"),
        ("991926577", "Espira Gruppen"),
        ("989628011", "Foreldreutvalget for grunnopplæringen (FUG)"),
        ("980396010", "Foreningen 2 foreldre"),
        ("938498318", "Frelsesarmeen"),
        ("971525843", "FRI – Foreningen for kjønns- og seksualitetsmangfold"),
        ("943762236", "Human-Etisk Forbund"),
        ("985066302", "Stiftelsen Human Rights Service (HRS)"),
        ("982842840", "Islamsk Råd Norge"),
        ("944384448", "Stiftelsen Kirkens Bymisjon"),
        ("971382325", "Krisesentersekretariatet"),
        ("971435739", "Landsrådet for Norges barne- og ungdomsorganisasjoner (LNU)"),
        ("955371755", "Stiftelsen Kvinneuniversitetet (tidl. Likestillingssenteret)"),
        ("995242311", "LIM – Likestilling, integrering, mangfold"),
        ("971038179", "Norges Blindeforbund"),
        ("938661316", "Norges Handikapforbund"),
        ("947975072", "Norges idrettsforbund"),
        ("971273577", "Norges Kristne Råd"),
        ("864139442", "Norges Røde Kors"),
        ("871033552", "Norsk Folkehjelp"),
        ("943260672", "Norsk Forbund for utviklingshemmede"),
        ("995523868", "Norsk studentorganisasjon"),
        ("941296459", "Redd Barna"),
        ("915972438", "UNICEF-komiteen i Norge"),
        ("971528214", "Unge funksjonshemmede"),
        ("981007751", "Velferdsalliansen, EAPN Norway"),
    };
    var orgerRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in orger)
    {
        var existing = await db.Recipients.FirstOrDefaultAsync(x => x.ExternalId == orgnr);
        if (existing is null)
        {
            existing = new Recipient { Type = RecipientType.Organization, ExternalId = orgnr, Name = navn, OrgForm = "FLI", IsActive = true };
            db.Recipients.Add(existing);
            await db.SaveChangesAsync();
        }
        orgerRecipients.Add(existing);
    }
    var gruppeOrger = await CreateStaticGroup("Interesseorganisasjoner", orgerRecipients, digdir.Id);

    // ── UH-sektor og forskning ───────────────────────────────────────────────
    // Alle orgnr er verifisert mot Brreg Enhetsregisteret API
    var uh = new (string orgnr, string navn)[]
    {
        ("985638187", "NUBU – Nasjonalt utviklingssenter for barn og unge (tidl. Atferdssenteret)"),
        ("986343113", "Fafo – Institutt for arbeidslivs- og velferdsforskning"),
        ("971228865", "Handelshøyskolen BI"),
        ("974760991", "Institutt for samfunnsforskning"),
        ("974767880", "NTNU – Norges teknisk-naturvitenskapelige universitet"),
        ("974789523", "Norges Handelshøyskole (NHH)"),
        ("971035854", "Universitetet i Oslo"),
        ("874789542", "Universitetet i Bergen"),
        ("970422528", "UiT – Norges arktiske universitet"),
        ("970546200", "Universitetet i Agder"),
        ("971564679", "Universitetet i Stavanger"),
        ("997058925", "OsloMet – storbyuniversitetet"),
    };
    var uhRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in uh)
        uhRecipients.Add(await UpsertRecipient(orgnr, navn, "SF"));
    var gruppeUH = await CreateStaticGroup("Forskningsmiljøer og UH-sektor", uhRecipients, digdir.Id);

    // ── Arbeidslivs- og næringsorganisasjoner ───────────────────────────────
    // Alle orgnr er verifisert mot Brreg Enhetsregisteret API
    var arbeid = new (string orgnr, string navn)[]
    {
        ("979371349", "Akademikerne"),
        ("871281602", "Arbeidsgiverforeningen Spekter"),
        ("936575668", "Den norske Advokatforening"),
        ("971075252", "Fagforbundet"),
        ("870953852", "FO – Fellesorganisasjonen"),
        ("996549488", "Finans Norge"),
        ("971032146", "KS – Kommunesektorens organisasjon"),
        ("971074337", "Landsorganisasjonen i Norge (LO)"),
        ("856331482", "NITO – Norges ingeniør- og teknologorganisasjon"),
        ("960893506", "Norsk Sykepleierforbund"),
        ("955600436", "Næringslivets Hovedorganisasjon (NHO)"),
        ("984152175", "Unio"),
        ("884026172", "Utdanningsforbundet"),
        ("970134646", "Virke"),
        ("971454431", "Yrkesorganisasjonenes Sentralforbund (YS)"),
    };
    var arbeidRecipients = new List<Recipient>();
    foreach (var (orgnr, navn) in arbeid)
    {
        var existing = await db.Recipients.FirstOrDefaultAsync(x => x.ExternalId == orgnr);
        if (existing is null)
        {
            existing = new Recipient { Type = RecipientType.Organization, ExternalId = orgnr, Name = navn, OrgForm = "FLI", IsActive = true };
            db.Recipients.Add(existing);
            await db.SaveChangesAsync();
        }
        arbeidRecipients.Add(existing);
    }
    var gruppeArbeid = await CreateStaticGroup("Arbeidslivs- og næringsorganisasjoner", arbeidRecipients, digdir.Id);

    // ── Kommuner-målgruppe: dynamisk gruppe (KOMM, kun aktive) ──────────────
    // Mottakere hentes ikke automatisk ved seed — brukeren synkroniserer
    // mot Brreg fra detaljsiden. Gruppen opprettes tom her.
    var kommunerGruppe = new TargetGroup
    {
        Name = "Kommuner",
        Type = TargetGroupType.Dynamic,
        Scope = TargetGroupScope.Shared,
        VirksomhetId = digdir.Id,
        DynamicCriteriaJson = System.Text.Json.JsonSerializer.Serialize(new DynamicCriteria
        {
            OrgForm = "KOMM",
            Aktivitet = "aktive"
        })
    };
    db.TargetGroups.Add(kommunerGruppe);
    await db.SaveChangesAsync();

    // ── Adresseliste: Høring av NOU 2026:2 ──────────────────────────────────
    var adresseliste = new Adresseliste
    {
        Tittel = "Høring av NOU 2026:2 Politikk for nye generasjoner",
        Beskrivelse = "Barne- og familiedepartementet sender NOU 2026:2 på høring. Høringsfrist: 01.08.2026.",
        OpprettetAv = "Barne- og familiedepartementet",
        Status = AdresselisteStatus.Utkast,
        OpprettetAt = new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
        VirksomhetId = digdir.Id
    };
    db.Adresselister.Add(adresseliste);
    await db.SaveChangesAsync();

    // Koble målgrupper (rekkefølge matcher regjeringen.no)
    var koblinger = new List<(int gruppeId, int rekkefølge)>
    {
        (gruppeDept.Id, 1),
        (gruppeSF.Id, 2),
        (kommunerGruppe.Id, 3),
        (gruppeEtater.Id, 4),
        (gruppeOrger.Id, 5),
        (gruppeUH.Id, 6),
        (gruppeArbeid.Id, 7),
    };

    foreach (var (gruppeId, rekkefølge) in koblinger)
        db.AdresselisteMålgrupper.Add(new AdresselisteMålgruppe
        {
            AdresselisteId = adresseliste.Id,
            MålgruppeId = gruppeId,
            Rekkefølge = rekkefølge
        });
    await db.SaveChangesAsync();

    // Lås listen — ta snapshot
    var seen = new HashSet<int>();
    var alle = await db.AdresselisteMålgrupper
        .Where(x => x.AdresselisteId == adresseliste.Id)
        .Include(x => x.Målgruppe).ThenInclude(g => g.Members)
        .OrderBy(x => x.Rekkefølge)
        .ToListAsync();

    foreach (var kobling in alle)
        foreach (var member in kobling.Målgruppe.Members)
            if (seen.Add(member.RecipientId))
                db.AdresselisteMottakere.Add(new AdresselisteMottaker
                {
                    AdresselisteId = adresseliste.Id,
                    RecipientId = member.RecipientId,
                    KildeMålgruppeId = kobling.MålgruppeId
                });

    adresseliste.Status = AdresselisteStatus.Låst;
    adresseliste.LåstAt = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);
    await db.SaveChangesAsync();
}

// ── Hjelpere for målgruppe-API ────────────────────────────────────────────────

static object MålgruppeShape(TargetGroup g) => new
{
    id = g.Id,
    navn = g.Name,
    type = g.Type == TargetGroupType.Dynamic ? "Dynamisk" : "Statisk",
    scope = g.Scope == TargetGroupScope.Shared ? "Delt" : "Privat",
    antallMedlemmer = g.Members.Count,
    opprettetAt = g.CreatedAt,
    kriterier = g.DynamicCriteriaJson is null
        ? null
        : KriterierShape(System.Text.Json.JsonSerializer.Deserialize<DynamicCriteria>(g.DynamicCriteriaJson)!)
};

static object KriterierShape(DynamicCriteria c) => new
{
    orgForm = c.OrgForm,
    naceKode = c.NacePrefix,
    sektorKode = c.SektorKode,
    virksomhetsstatus = c.Aktivitet,
    aktivitetFilter = c.AktivitetFilter,
    inkluderUnderenheter = c.IncludeSubUnits,
    ekskludertFraGruppe = c.ExcludedOrgnrs
};

static TargetGroupScope? ParseScope(string? scope) => scope switch
{
    "Privat" => TargetGroupScope.Private,
    "Delt" or null => TargetGroupScope.Shared,
    _ => (TargetGroupScope?)null
};

record AbonnentRegistrerRequest(string Epost);
record VirksomhetOnboardRequest(string Orgnr, string Navn, string? OnboardetAv = null);

// Eksponerer Program-klassen for WebApplicationFactory i testprosjektet
public partial class Program { }
