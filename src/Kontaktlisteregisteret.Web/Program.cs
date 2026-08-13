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
        await SeedData.SeedAsync(db);
}

// ── API v1 ────────────────────────────────────────────────────────────────────
// Maskinporten-token med scope digdir:kontaktliste.read kreves i produksjon.
// I PoC er autentisering ikke aktivert.

var api = app.MapGroup("/api/v1/virksomheter/{orgnr}")
    .AddEndpointFilter(async (context, next) =>
    {
        var orgnr = context.HttpContext.Request.RouteValues["orgnr"]?.ToString();
        var svc = context.HttpContext.RequestServices.GetRequiredService<VirksomhetService>();
        var virksomhet = await svc.GetAktivByOrgnrAsync(orgnr ?? "");
        if (virksomhet is null)
            return Results.NotFound(new { error = "Virksomheten finnes ikke eller er ikke aktiv." });
        context.HttpContext.Items["VirksomhetId"] = virksomhet.Id;
        return await next(context);
    });

// GET /api/v1/virksomheter/{orgnr}/adresselister — låste adresselister for virksomheten
api.MapGet("/adresselister", async (HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var lister = await db.Adresselister
        .Where(a => a.Status == AdresselisteStatus.Låst && a.VirksomhetId == virksomhetId)
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

// GET /api/v1/virksomheter/{orgnr}/adresselister/{id} — metadata for én låst liste
api.MapGet("/adresselister/{id:int}", async (int id, HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var a = await db.Adresselister
        .Where(x => x.Id == id && x.Status == AdresselisteStatus.Låst && x.VirksomhetId == virksomhetId)
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

// GET /api/v1/virksomheter/{orgnr}/adresselister/{id}/mottakere — snapshot-mottakere
api.MapGet("/adresselister/{id:int}/mottakere", async (int id, HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var finnes = await db.Adresselister
        .AnyAsync(a => a.Id == id && a.Status == AdresselisteStatus.Låst && a.VirksomhetId == virksomhetId);

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

// GET /api/v1/virksomheter/{orgnr}/abonnementslister — abonnementslister for virksomheten
api.MapGet("/abonnementslister", async (HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var lister = await db.Abonnementslister
        .Where(l => l.VirksomhetId == virksomhetId)
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

// GET /api/v1/virksomheter/{orgnr}/abonnementslister/{id}/abonnenter — abonnenter for én liste
api.MapGet("/abonnementslister/{id:int}/abonnenter", async (int id, HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    if (!await db.Abonnementslister.AnyAsync(l => l.Id == id && l.VirksomhetId == virksomhetId))
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

// POST /api/v1/virksomheter/{orgnr}/abonnementslister/{id}/abonnenter — legg til abonnent
// Body: { "epost": "navn@eksempel.no" }
api.MapPost("/abonnementslister/{id:int}/abonnenter",
    async (int id, HttpContext http, AbonnentRegistrerRequest req, AppDbContext db, AbonnementslisteService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.Epost))
        return Results.BadRequest(new { error = "E-post er påkrevd." });

    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    if (!await db.Abonnementslister.AnyAsync(l => l.Id == id && l.VirksomhetId == virksomhetId))
        return Results.NotFound(new { error = "Abonnementslisten finnes ikke." });

    var (ok, error, abonnent) = await svc.LeggTilAsync(id, req.Epost, AbonnentKilde.Api);
    if (!ok) return Results.Conflict(new { error });

    var routeOrgnr = http.Request.RouteValues["orgnr"]?.ToString();
    return Results.Created($"/api/v1/virksomheter/{routeOrgnr}/abonnementslister/{id}/abonnenter/{abonnent!.Id}", new
    {
        id = abonnent.Id,
        epost = abonnent.Epost,
        lagtTilAt = abonnent.LagtTilAt
    });
});

// DELETE /api/v1/virksomheter/{orgnr}/abonnenter/{id} — fjern abonnent
api.MapDelete("/abonnenter/{id:int}", async (int id, HttpContext http, AppDbContext db, AbonnementslisteService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var tilhørerVirksomhet = await db.Abonnenter
        .Where(a => a.Id == id)
        .AnyAsync(a => a.Abonnementsliste.VirksomhetId == virksomhetId);
    if (!tilhørerVirksomhet)
        return Results.NotFound(new { error = "Abonnenten finnes ikke." });
    var ok = await svc.SlettAbonnentAsync(id);
    return ok ? Results.NoContent() : Results.NotFound(new { error = "Abonnenten finnes ikke." });
});

// ── API v1: målgrupper ────────────────────────────────────────────────────────
// Alle 4xx/5xx-svar bruker application/problem+json (RFC 9457).

// GET /api/v1/virksomheter/{orgnr}/malgrupper — målgrupper for virksomheten
api.MapGet("/malgrupper", async (HttpContext http, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    return Results.Ok((await svc.GetAllAsync(virksomhetId)).Select(MålgruppeShape));
});

// GET /api/v1/virksomheter/{orgnr}/malgrupper/{id}
api.MapGet("/malgrupper/{id:int}", async (int id, HttpContext http, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var g = await svc.GetAsync(id, virksomhetId);
    return g is null
        ? Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet")
        : Results.Ok(MålgruppeShape(g));
});

// GET /api/v1/virksomheter/{orgnr}/malgrupper/{id}/medlemmer?page=1&size=50
api.MapGet("/malgrupper/{id:int}/medlemmer", async (int id, HttpContext http, int? page, int? size, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var g = await svc.GetAsync(id, virksomhetId);
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

// GET /api/v1/virksomheter/{orgnr}/malgrupper/{id}/eksport.json — JSON-filnedlasting
api.MapGet("/malgrupper/{id:int}/eksport.json", async (int id, HttpContext http, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    if (await svc.GetAsync(id, virksomhetId) is null)
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");
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

// GET /api/v1/virksomheter/{orgnr}/malgrupper/{id}/eksport.csv — CSV-filnedlasting
api.MapGet("/malgrupper/{id:int}/eksport.csv", async (int id, HttpContext http, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    if (await svc.GetAsync(id, virksomhetId) is null)
        return Results.Problem(title: "Målgruppe ikke funnet", statusCode: 404,
            detail: $"Målgruppe {id} finnes ikke.",
            type: "https://kontaktlisteregisteret.no/problems/ikke-funnet");
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

// POST /api/v1/virksomheter/{orgnr}/malgrupper — opprett statisk eller dynamisk målgruppe
// For Statisk: validerer orgnr mot Brreg og legger til de som finnes (Ok).
// For Dynamisk: kjører umiddelbart SyncDynamicGroupAsync — kan ta 10–30 s.
api.MapPost("/malgrupper", async (HttpContext http, OpprettMålgruppeRequest req, TargetGroupService svc, IBrregService brreg) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var routeOrgnr = http.Request.RouteValues["orgnr"]?.ToString();

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

            gruppe = await svc.CreateStaticAsync(req.Navn.Trim(), scope.Value, recipients, virksomhetId);
            break;
        }
        case "Dynamisk":
        {
            var criteria = (req.Kriterier
                ?? new DynamicCriteriaDto(null, null, null, null, null, null, null))
                .TilIntern();
            gruppe = await svc.CreateDynamicAsync(req.Navn.Trim(), scope.Value, criteria, virksomhetId);
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
    return Results.Created($"/api/v1/virksomheter/{routeOrgnr}/malgrupper/{gruppe.Id}", MålgruppeShape(nyGruppe!));
});

// PATCH /api/v1/virksomheter/{orgnr}/malgrupper/{id} — endre navn
api.MapPatch("/malgrupper/{id:int}", async (int id, HttpContext http, NavnEndreRequest req, TargetGroupService svc, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    if (!await db.TargetGroups.AnyAsync(g => g.Id == id && g.VirksomhetId == virksomhetId))
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

// PUT /api/v1/virksomheter/{orgnr}/malgrupper/{id}/kriterier — oppdater filterregler og resynkroniser mot Brreg
// OBS: Kallet kan ta 10–30 s — SyncDynamicGroupAsync henter alle sider fra Brreg sekvensiell.
api.MapPut("/malgrupper/{id:int}/kriterier", async (int id, HttpContext http, DynamicCriteriaDto dto, TargetGroupService svc) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var g = await svc.GetAsync(id, virksomhetId);
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

    var oppdatert = await svc.GetAsync(id, virksomhetId);
    return Results.Ok(new { antallMedlemmer = oppdatert!.Members.Count });
});

// DELETE /api/v1/virksomheter/{orgnr}/malgrupper/{id}
// Blokkeres med 409 hvis målgruppen er koblet til en låst adresseliste —
// snapshotet er immutabelt, men sletting ville gitt inkonsistente oppslag.
api.MapDelete("/malgrupper/{id:int}", async (int id, HttpContext http, AppDbContext db) =>
{
    var virksomhetId = (int)http.Items["VirksomhetId"]!;
    var g = await db.TargetGroups.FirstOrDefaultAsync(x => x.Id == id && x.VirksomhetId == virksomhetId);
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
