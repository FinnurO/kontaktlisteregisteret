using ClosedXML.Excel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

public class AdresselisteService(AppDbContext db, VarslingsService varslinger)
{
    private readonly VarslingsService _varslinger = varslinger;
    // ── Queries ─────────────────────────────────────────────────────────────

    /// Henter alle lister uavhengig av virksomhet — brukes av maskin-API.
    public async Task<List<Adresseliste>> GetAllAsync() =>
        await db.Adresselister
            .Include(a => a.Målgrupper).ThenInclude(m => m.Målgruppe)
            .Include(a => a.Mottakere)
            .OrderByDescending(a => a.OpprettetAt)
            .ToListAsync();

    /// Henter lister filtrert på virksomhet — brukes av Blazor UI.
    public async Task<List<Adresseliste>> GetAllAsync(int virksomhetId) =>
        await db.Adresselister
            .Where(a => a.VirksomhetId == virksomhetId)
            .Include(a => a.Målgrupper).ThenInclude(m => m.Målgruppe)
            .Include(a => a.Mottakere)
            .OrderByDescending(a => a.OpprettetAt)
            .ToListAsync();

    public async Task<Adresseliste?> GetAsync(int id) =>
        await db.Adresselister
            .Include(a => a.Målgrupper).ThenInclude(m => m.Målgruppe)
                .ThenInclude(g => g.Members).ThenInclude(m => m.Recipient)
            .Include(a => a.Abonnementslister).ThenInclude(k => k.Abonnementsliste)
                .ThenInclude(l => l.Abonnenter)
            .Include(a => a.Mottakere).ThenInclude(m => m.Recipient)
            .FirstOrDefaultAsync(a => a.Id == id);

    /// Returnerer kun Låste lister — for API-eksponering
    public async Task<List<Adresseliste>> GetLåsteAsync() =>
        await db.Adresselister
            .Where(a => a.Status == AdresselisteStatus.Låst)
            .Include(a => a.Mottakere)
            .OrderByDescending(a => a.LåstAt)
            .ToListAsync();

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<Adresseliste> CreateAsync(
        string tittel, string? beskrivelse = null, string? opprettetAv = null, int? virksomhetId = null)
    {
        var liste = new Adresseliste
        {
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            OpprettetAv = opprettetAv,
            VirksomhetId = virksomhetId
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();
        return liste;
    }

    public async Task<bool> AddMålgruppeAsync(int adresselisteId, int målgruppeId)
    {
        var liste = await db.Adresselister.FindAsync(adresselisteId);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;

        if (await db.AdresselisteMålgrupper.AnyAsync(
                x => x.AdresselisteId == adresselisteId && x.MålgruppeId == målgruppeId))
            return false;

        var nestRekkefølge = await db.AdresselisteMålgrupper
            .Where(x => x.AdresselisteId == adresselisteId)
            .MaxAsync(x => (int?)x.Rekkefølge) ?? 0;

        db.AdresselisteMålgrupper.Add(new AdresselisteMålgruppe
        {
            AdresselisteId = adresselisteId,
            MålgruppeId = målgruppeId,
            Rekkefølge = nestRekkefølge + 1
        });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMålgruppeAsync(int adresselisteId, int målgruppeId)
    {
        var liste = await db.Adresselister.FindAsync(adresselisteId);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;

        var kobling = await db.AdresselisteMålgrupper
            .FirstOrDefaultAsync(x => x.AdresselisteId == adresselisteId && x.MålgruppeId == målgruppeId);
        if (kobling is null) return false;

        db.AdresselisteMålgrupper.Remove(kobling);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddAbonnementslisteAsync(int adresselisteId, int abonnementslisteId)
    {
        var liste = await db.Adresselister.FindAsync(adresselisteId);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;

        if (await db.AdresselisteAbonnementslister.AnyAsync(
                x => x.AdresselisteId == adresselisteId && x.AbonnementslisteId == abonnementslisteId))
            return false;

        var nestRekkefølge = await db.AdresselisteAbonnementslister
            .Where(x => x.AdresselisteId == adresselisteId)
            .MaxAsync(x => (int?)x.Rekkefølge) ?? 0;

        db.AdresselisteAbonnementslister.Add(new AdresselisteAbonnementsliste
        {
            AdresselisteId = adresselisteId,
            AbonnementslisteId = abonnementslisteId,
            Rekkefølge = nestRekkefølge + 1
        });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAbonnementslisteAsync(int adresselisteId, int abonnementslisteId)
    {
        var liste = await db.Adresselister.FindAsync(adresselisteId);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;

        var kobling = await db.AdresselisteAbonnementslister
            .FirstOrDefaultAsync(x => x.AdresselisteId == adresselisteId && x.AbonnementslisteId == abonnementslisteId);
        if (kobling is null) return false;

        db.AdresselisteAbonnementslister.Remove(kobling);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(int id, string tittel, string? beskrivelse)
    {
        var liste = await db.Adresselister.FindAsync(id);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;
        liste.Tittel = tittel;
        liste.Beskrivelse = beskrivelse;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Eksklusjoner ─────────────────────────────────────────────────────────

    public static HashSet<string> GetEkskluderte(Adresseliste liste) =>
        liste.EkskluderteJson is null
            ? []
            : JsonSerializer.Deserialize<HashSet<string>>(liste.EkskluderteJson) ?? [];

    public async Task SetEkskluderteAsync(int id, HashSet<string> ekskluderte)
    {
        var liste = await db.Adresselister.FindAsync(id);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return;
        liste.EkskluderteJson = ekskluderte.Count == 0
            ? null
            : JsonSerializer.Serialize(ekskluderte);
        await db.SaveChangesAsync();
    }

    public async Task<bool> SetStatusAsync(int id, AdresselisteStatus nyStatus)
    {
        var liste = await db.Adresselister.FindAsync(id);
        if (liste is null || liste.Status == AdresselisteStatus.Låst) return false;
        liste.Status = nyStatus;
        await db.SaveChangesAsync();
        return true;
    }

    /// Tar snapshot av alle tilknyttede målgrupper og låser listen.
    /// Etter låsing er Mottakere-tabellen kilden til sannhet — ikke målgruppene.
    public async Task<(bool Ok, string? Error)> LåsAsync(int id)
    {
        var liste = await GetAsync(id);
        if (liste is null) return (false, "Adresselisten finnes ikke.");
        if (liste.Status == AdresselisteStatus.Låst) return (false, "Listen er allerede låst.");
        if (!liste.Målgrupper.Any()) return (false, "Listen har ingen tilknyttede målgrupper.");

        // Hent ekskluderte orgnr
        var ekskluderte = GetEkskluderte(liste);

        // Hent alle nåværende mottakere fra alle tilknyttede målgrupper.
        // To rader med samme RecipientId men ulikt Visningsnavn er intendert unike (c/o-tilfeller).
        var sett = new HashSet<(int RecipientId, string? Visningsnavn, string? CoAdresse)>();
        var nyeMottakere = new List<AdresselisteMottaker>();

        foreach (var kobling in liste.Målgrupper.OrderBy(m => m.Rekkefølge))
        {
            var gruppe = kobling.Målgruppe;
            foreach (var member in gruppe.Members)
            {
                // Hopp over ekskluderte og duplikater
                if (ekskluderte.Contains(member.Recipient.ExternalId)) continue;
                if (sett.Add((member.RecipientId, member.Visningsnavn, member.CoAdresse)))
                {
                    nyeMottakere.Add(new AdresselisteMottaker
                    {
                        AdresselisteId = id,
                        RecipientId = member.RecipientId,
                        KildeMålgruppeId = gruppe.Id,
                        Visningsnavn = member.Visningsnavn,
                        CoAdresse = member.CoAdresse
                    });
                }
            }
        }

        db.AdresselisteMottakere.AddRange(nyeMottakere);

        // Snapshot abonnenter fra tilknyttede abonnementslister
        var settEpost = new HashSet<string>();
        foreach (var kobling in liste.Abonnementslister.OrderBy(k => k.Rekkefølge))
        {
            foreach (var abonnent in kobling.Abonnementsliste.Abonnenter)
            {
                if (!settEpost.Add(abonnent.Epost)) continue;

                // Upsert Recipient med Type=Subscriber og ExternalId=epost
                var recipient = await db.Recipients.FirstOrDefaultAsync(r => r.ExternalId == abonnent.Epost);
                if (recipient is null)
                {
                    recipient = new Recipient
                    {
                        Type = RecipientType.Subscriber,
                        ExternalId = abonnent.Epost,
                        Name = abonnent.Epost,
                        IsActive = true
                    };
                    db.Recipients.Add(recipient);
                    await db.SaveChangesAsync();
                }

                db.AdresselisteMottakere.Add(new AdresselisteMottaker
                {
                    AdresselisteId = id,
                    RecipientId = recipient.Id,
                    KildeAbonnementslisteId = kobling.AbonnementslisteId
                });
            }
        }

        liste.Status = AdresselisteStatus.Låst;
        liste.LåstAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _varslinger.SendLåstVarslingAsync(liste.Tittel, liste.Mottakere.Count);

        return (true, null);
    }

    public async Task<Adresseliste> KopierAsync(int id)
    {
        var original = await GetAsync(id) ?? throw new KeyNotFoundException();
        var kopi = new Adresseliste
        {
            Tittel = original.Tittel + " (kopi)",
            Beskrivelse = original.Beskrivelse,
            OpprettetAv = original.OpprettetAv,
            Status = AdresselisteStatus.Utkast,
            EkskluderteJson = original.EkskluderteJson,  // behold eksklusjoner
            VirksomhetId = original.VirksomhetId          // behold tenant-tilknytning
        };
        db.Adresselister.Add(kopi);
        await db.SaveChangesAsync();
        // Koble samme målgrupper (ikke snapshot — det tas på nytt ved låsing)
        foreach (var m in original.Målgrupper.OrderBy(x => x.Rekkefølge))
            db.AdresselisteMålgrupper.Add(new AdresselisteMålgruppe
            {
                AdresselisteId = kopi.Id,
                MålgruppeId = m.MålgruppeId,
                Rekkefølge = m.Rekkefølge
            });
        await db.SaveChangesAsync();
        return kopi;
    }

    public async Task DeleteAsync(int id)
    {
        var liste = await db.Adresselister.FindAsync(id);
        if (liste is not null)
        {
            db.Adresselister.Remove(liste);
            await db.SaveChangesAsync();
        }
    }

    // ── Eksport ──────────────────────────────────────────────────────────────

    /// Lager en Excel-fil (.xlsx) med snapshot-mottakerne fra en låst adresseliste.
    /// Returnerer filinnholdet som byte-array.
    public async Task<byte[]> ExportXlsxAsync(int listeId)
    {
        var mottakere = await db.AdresselisteMottakere
            .Where(m => m.AdresselisteId == listeId)
            .Include(m => m.Recipient)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mottakere");

        // Kolonneoverskrifter i rad 1
        ws.Cell(1, 1).Value = "Orgnr";
        ws.Cell(1, 2).Value = "BrregNavn";
        ws.Cell(1, 3).Value = "Visningsnavn";
        ws.Cell(1, 4).Value = "CoAdresse";

        // Fet overskrift
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;

        // Data fra rad 2
        var row = 2;
        foreach (var m in mottakere)
        {
            ws.Cell(row, 1).Value = m.Recipient.ExternalId;
            ws.Cell(row, 2).Value = m.Recipient.Name;
            ws.Cell(row, 3).Value = m.Visningsnavn ?? "";
            ws.Cell(row, 4).Value = m.CoAdresse ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// Live union av alle tilknyttede målgrupper (for ulåste lister)
    /// Deduplicerer på (RecipientId, Visningsnavn) — samme org kan ligge to ganger med ulikt visningsnavn (c/o)
    public static List<LiveMottaker> GetLiveMottakere(Adresseliste liste)
    {
        var seen = new HashSet<(int, string?, string?)>();
        var result = new List<LiveMottaker>();
        foreach (var kobling in liste.Målgrupper.OrderBy(m => m.Rekkefølge))
            foreach (var member in kobling.Målgruppe.Members)
                if (seen.Add((member.RecipientId, member.Visningsnavn, member.CoAdresse)))
                    result.Add(new LiveMottaker(member.Recipient, member.Visningsnavn, member.CoAdresse, kobling.MålgruppeId));
        return result;
    }
}

/// Hjelpetype for live (ulåst) mottakerliste med visningsnavn
public record LiveMottaker(
    Recipient Recipient,
    string? Visningsnavn,
    string? CoAdresse,
    int KildeMålgruppeId)
{
    public string VisNavn => Visningsnavn ?? Recipient.Name;
}
