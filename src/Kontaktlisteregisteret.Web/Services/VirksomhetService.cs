using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

public class VirksomhetService(AppDbContext db)
{
    // ── Queries ─────────────────────────────────────────────────────────────

    public Task<List<Virksomhet>> GetAllAsync() =>
        db.Virksomheter.OrderBy(v => v.Navn).ToListAsync();

    public Task<Virksomhet?> GetByOrgnrAsync(string orgnr) =>
        db.Virksomheter.FirstOrDefaultAsync(v => v.Orgnr == orgnr);

    public Task<Virksomhet?> GetAktivByOrgnrAsync(string orgnr) =>
        db.Virksomheter.FirstOrDefaultAsync(v => v.Orgnr == orgnr && v.Status == VirksomhetStatus.Aktiv);

    // ── Admin-operasjoner ────────────────────────────────────────────────────

    public async Task<(Virksomhet? Virksomhet, string? Error)> OnboardAsync(
        string orgnr, string navn, string? onboardetAv = null)
    {
        orgnr = orgnr.Trim();
        navn = navn.Trim();

        if (orgnr.Length != 9 || !orgnr.All(char.IsDigit))
            return (null, "Organisasjonsnummer må være nøyaktig 9 siffer.");
        if (string.IsNullOrWhiteSpace(navn))
            return (null, "Navn er påkrevd.");
        if (await db.Virksomheter.AnyAsync(v => v.Orgnr == orgnr))
            return (null, $"Virksomhet med orgnr {orgnr} er allerede registrert.");

        var v = new Virksomhet
        {
            Orgnr = orgnr,
            Navn = navn,
            OnboardetAv = onboardetAv
        };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return (v, null);
    }

    public async Task<bool> SetStatusAsync(int id, VirksomhetStatus status)
    {
        var v = await db.Virksomheter.FindAsync(id);
        if (v is null) return false;
        v.Status = status;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SlettAsync(int id)
    {
        var v = await db.Virksomheter.FindAsync(id);
        if (v is null) return false;
        db.Virksomheter.Remove(v);
        await db.SaveChangesAsync();
        return true;
    }
}
