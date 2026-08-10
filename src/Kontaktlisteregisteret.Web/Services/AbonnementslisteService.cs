using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

public class AbonnementslisteService(AppDbContext db)
{
    // ── Lister ────────────────────────────────────────────────────────────────

    public Task<List<Abonnementsliste>> GetAllAsync() =>
        db.Abonnementslister
            .Include(l => l.Abonnenter)
            .OrderByDescending(l => l.OpprettetAt)
            .ToListAsync();

    public Task<Abonnementsliste?> GetAsync(int id) =>
        db.Abonnementslister
            .Include(l => l.Abonnenter)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task<Abonnementsliste> OpprettAsync(string navn, string? beskrivelse, string? opprettetAv = null)
    {
        var liste = new Abonnementsliste
        {
            Navn = navn.Trim(),
            Beskrivelse = string.IsNullOrWhiteSpace(beskrivelse) ? null : beskrivelse.Trim(),
            OpprettetAv = string.IsNullOrWhiteSpace(opprettetAv) ? null : opprettetAv.Trim()
        };
        db.Abonnementslister.Add(liste);
        await db.SaveChangesAsync();
        return liste;
    }

    public async Task<bool> OppdaterAsync(int id, string navn, string? beskrivelse)
    {
        var liste = await db.Abonnementslister.FindAsync(id);
        if (liste is null) return false;
        liste.Navn = navn.Trim();
        liste.Beskrivelse = string.IsNullOrWhiteSpace(beskrivelse) ? null : beskrivelse.Trim();
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SlettListeAsync(int id)
    {
        var liste = await db.Abonnementslister.FindAsync(id);
        if (liste is null) return false;
        db.Abonnementslister.Remove(liste);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Abonnenter ────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string? Error, Abonnent? Abonnent)> LeggTilAsync(
        int listeId, string epost, AbonnentKilde kilde = AbonnentKilde.Manuell)
    {
        epost = epost.Trim().ToLowerInvariant();

        if (!System.Net.Mail.MailAddress.TryCreate(epost, out _))
            return (false, "Ugyldig e-postadresse.", null);

        if (!await db.Abonnementslister.AnyAsync(l => l.Id == listeId))
            return (false, "Abonnementslisten finnes ikke.", null);

        if (await db.Abonnenter.AnyAsync(a => a.AbonnementslisteId == listeId && a.Epost == epost))
            return (false, "E-postadressen er allerede registrert i denne listen.", null);

        var abonnent = new Abonnent { AbonnementslisteId = listeId, Epost = epost, Kilde = kilde };
        db.Abonnenter.Add(abonnent);
        await db.SaveChangesAsync();
        return (true, null, abonnent);
    }

    public async Task<bool> SlettAbonnentAsync(int id)
    {
        var a = await db.Abonnenter.FindAsync(id);
        if (a is null) return false;
        db.Abonnenter.Remove(a);
        await db.SaveChangesAsync();
        return true;
    }

    public Task<int> AntallAbonnenterAsync(int listeId) =>
        db.Abonnenter.CountAsync(a => a.AbonnementslisteId == listeId);
}
