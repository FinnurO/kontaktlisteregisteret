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

    public async Task<TargetGroup> KopierAsync(int id)
    {
        var original = await GetAsync(id) ?? throw new KeyNotFoundException();
        var kopi = new TargetGroup
        {
            Name = original.Name + " (kopi)",
            Type = original.Type,
            Scope = original.Scope,
            DynamicCriteriaJson = original.DynamicCriteriaJson
        };
        db.TargetGroups.Add(kopi);
        await db.SaveChangesAsync();
        if (original.Type == TargetGroupType.Static)
        {
            // Kopierer alle TGM-rader inkludert Visningsnavn/CoAdresse
            foreach (var m in original.Members)
                db.TargetGroupMembers.Add(new()
                {
                    TargetGroupId = kopi.Id,
                    RecipientId = m.RecipientId,
                    Visningsnavn = m.Visningsnavn,
                    CoAdresse = m.CoAdresse
                });
            await db.SaveChangesAsync();
        }
        return kopi;
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

    public async Task SaveCriteriaAsync(TargetGroup group)
    {
        db.TargetGroups.Update(group);
        await db.SaveChangesAsync();
    }

    public async Task SyncDynamicGroupAsync(TargetGroup group)
    {
        if (group.Type != TargetGroupType.Dynamic || group.DynamicCriteriaJson is null) return;
        var criteria = JsonSerializer.Deserialize<DynamicCriteria>(group.DynamicCriteriaJson)!;
        var enheter = await brreg.EvaluateDynamicCriteriaAsync(criteria);
        var recipients = enheter.Select(e => BrregEnhetToRecipient(e)).ToList();
        await ReplaceRecipientsAsync(group.Id, recipients);
    }

    /// Oppdaterer visningsnavn/coAdresse på en konkret TGM-rad (identifisert ved TGM.Id)
    public async Task SetVisningsnavnAsync(int memberId, string? visningsnavn, string? coAdresse)
    {
        var member = await db.TargetGroupMembers.FindAsync(memberId);
        if (member is null) return;
        member.Visningsnavn = string.IsNullOrWhiteSpace(visningsnavn) ? null : visningsnavn.Trim();
        member.CoAdresse = string.IsNullOrWhiteSpace(coAdresse) ? null : coAdresse.Trim();
        await db.SaveChangesAsync();
    }

    /// Fjerner én konkret TGM-rad (identifisert ved TGM.Id)
    public async Task RemoveMemberAsync(int memberId)
    {
        var member = await db.TargetGroupMembers.FindAsync(memberId);
        if (member is not null)
        {
            db.TargetGroupMembers.Remove(member);
            await db.SaveChangesAsync();
        }
    }

    /// Legger til et orgnr med eksplisitt visningsnavn — tillater samme Recipient å ligge to ganger
    public async Task AddMedVisningsnavnAsync(int groupId, BrregEnhet e, string visningsnavn, string? coAdresse)
    {
        var existing = await db.Recipients.FirstOrDefaultAsync(x => x.ExternalId == e.organisasjonsnummer);
        if (existing is null)
        {
            existing = BrregEnhetToRecipient(e);
            db.Recipients.Add(existing);
            await db.SaveChangesAsync();
        }
        db.TargetGroupMembers.Add(new()
        {
            TargetGroupId = groupId,
            RecipientId = existing.Id,
            Visningsnavn = visningsnavn.Trim(),
            CoAdresse = string.IsNullOrWhiteSpace(coAdresse) ? null : coAdresse.Trim()
        });
        await db.SaveChangesAsync();
    }

    public async Task AddMembersAsync(int groupId, List<Recipient> recipients) =>
        await AddRecipientsAsync(groupId, recipients);

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
