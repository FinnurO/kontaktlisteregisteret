using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

public class TargetGroupService(AppDbContext db, BrregService brreg)
{
    public async Task<List<TargetGroup>> GetAllAsync() =>
        await db.TargetGroups
            .Include(g => g.Members).ThenInclude(m => m.Recipient)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

    public async Task<TargetGroup?> GetAsync(int id) =>
        await db.TargetGroups
            .Include(g => g.Members).ThenInclude(m => m.Recipient)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<TargetGroup> CreateDynamicAsync(string name, TargetGroupScope scope, DynamicCriteria criteria)
    {
        var group = new TargetGroup
        {
            Name = name,
            Type = TargetGroupType.Dynamic,
            Scope = scope,
            DynamicCriteriaJson = JsonSerializer.Serialize(criteria)
        };
        db.TargetGroups.Add(group);
        await db.SaveChangesAsync();
        await SyncDynamicGroupAsync(group);
        return group;
    }

    public async Task<TargetGroup> CreateStaticAsync(string name, TargetGroupScope scope, List<Recipient> recipients)
    {
        var group = new TargetGroup
        {
            Name = name,
            Type = TargetGroupType.Static,
            Scope = scope
        };
        db.TargetGroups.Add(group);
        await db.SaveChangesAsync();
        await AddRecipientsAsync(group.Id, recipients);
        return group;
    }

    public async Task DeleteAsync(int id)
    {
        var group = await db.TargetGroups.FindAsync(id);
        if (group is not null)
        {
            db.TargetGroups.Remove(group);
            await db.SaveChangesAsync();
        }
    }

    public async Task SyncDynamicGroupAsync(TargetGroup group)
    {
        if (group.Type != TargetGroupType.Dynamic || group.DynamicCriteriaJson is null) return;
        var criteria = JsonSerializer.Deserialize<DynamicCriteria>(group.DynamicCriteriaJson)!;
        var enheter = await brreg.EvaluateDynamicCriteriaAsync(criteria);
        var recipients = enheter.Select(e => BrregEnhetToRecipient(e)).ToList();
        await ReplaceRecipientsAsync(group.Id, recipients);
    }

    private async Task AddRecipientsAsync(int groupId, List<Recipient> recipients)
    {
        foreach (var r in recipients)
        {
            var existing = await db.Recipients.FirstOrDefaultAsync(x => x.ExternalId == r.ExternalId);
            if (existing is null)
            {
                db.Recipients.Add(r);
                await db.SaveChangesAsync();
                existing = r;
            }
            if (!await db.TargetGroupMembers.AnyAsync(m => m.TargetGroupId == groupId && m.RecipientId == existing.Id))
                db.TargetGroupMembers.Add(new() { TargetGroupId = groupId, RecipientId = existing.Id });
        }
        await db.SaveChangesAsync();
    }

    private async Task ReplaceRecipientsAsync(int groupId, List<Recipient> recipients)
    {
        var existing = await db.TargetGroupMembers.Where(m => m.TargetGroupId == groupId).ToListAsync();
        db.TargetGroupMembers.RemoveRange(existing);
        await db.SaveChangesAsync();
        await AddRecipientsAsync(groupId, recipients);
    }

    public static Recipient BrregEnhetToRecipient(BrregEnhet e) => new()
    {
        Type = RecipientType.Organization,
        ExternalId = e.organisasjonsnummer,
        Name = e.navn,
        OrgForm = e.organisasjonsform?.kode,
        NaceCode = e.naeringskode1?.kode,
        PostalAddress = e.postadresse?.adresse is { Count: > 0 } a ? string.Join(", ", a) : null,
        PostalCode = e.postadresse?.postnummer,
        PostalCity = e.postadresse?.poststed,
        IsActive = e.slettedato is null
    };

    public async Task<byte[]> ExportJsonAsync(int groupId)
    {
        var group = await GetAsync(groupId) ?? throw new KeyNotFoundException();
        var output = group.Members.Select(m => new
        {
            organizationId = m.Recipient.ExternalId,
            name = m.Recipient.Name,
            type = m.Recipient.Type.ToString(),
            orgForm = m.Recipient.OrgForm,
            naceCode = m.Recipient.NaceCode,
            postalAddress = m.Recipient.PostalAddress,
            postalCode = m.Recipient.PostalCode,
            postalCity = m.Recipient.PostalCity,
            isActive = m.Recipient.IsActive
        });
        return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<byte[]> ExportCsvAsync(int groupId)
    {
        var group = await GetAsync(groupId) ?? throw new KeyNotFoundException();
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, System.Text.Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.WriteHeader<CsvRow>();
        await csv.NextRecordAsync();
        foreach (var m in group.Members)
        {
            csv.WriteRecord(new CsvRow(m.Recipient));
            await csv.NextRecordAsync();
        }
        await writer.FlushAsync();
        return ms.ToArray();
    }

    private record CsvRow(
        string OrganizationId, string Name, string Type,
        string? OrgForm, string? NaceCode,
        string? PostalAddress, string? PostalCode, string? PostalCity)
    {
        public CsvRow(Recipient r) : this(
            r.ExternalId, r.Name, r.Type.ToString(),
            r.OrgForm, r.NaceCode,
            r.PostalAddress, r.PostalCode, r.PostalCity) { }
    }

    public DynamicCriteria? GetCriteria(TargetGroup g) =>
        g.DynamicCriteriaJson is null ? null
            : JsonSerializer.Deserialize<DynamicCriteria>(g.DynamicCriteriaJson);
}
