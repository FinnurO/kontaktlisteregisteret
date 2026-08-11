using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

public class AuditLogService(AppDbContext db)
{
    public async Task LogAsync(
        string handling, string enhetsType, int enhetsId, string? enhetsNavn,
        int? virksomhetId = null, string? utførtAv = null)
    {
        db.AuditLogg.Add(new AuditLog
        {
            Handling = handling,
            EnhetsType = enhetsType,
            EnhetsId = enhetsId,
            EnhetsNavn = enhetsNavn,
            VirksomhetId = virksomhetId,
            UtførtAv = utførtAv ?? "System"
        });
        await db.SaveChangesAsync();
    }

    public Task<List<AuditLog>> HentForVirksomhetAsync(int virksomhetId, int antall = 50) =>
        db.AuditLogg
            .Where(a => a.VirksomhetId == virksomhetId)
            .OrderByDescending(a => a.Tidspunkt)
            .Take(antall)
            .ToListAsync();
}
