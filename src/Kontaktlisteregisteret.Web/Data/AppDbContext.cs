using Microsoft.EntityFrameworkCore;

namespace Kontaktlisteregisteret.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TargetGroup> TargetGroups => Set<TargetGroup>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<TargetGroupMember> TargetGroupMembers => Set<TargetGroupMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TargetGroup>().HasIndex(x => x.Name);
        b.Entity<Recipient>().HasIndex(x => x.ExternalId).IsUnique();
        b.Entity<TargetGroupMember>()
            .HasKey(x => new { x.TargetGroupId, x.RecipientId });
    }
}

public class TargetGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TargetGroupType Type { get; set; }
    public TargetGroupScope Scope { get; set; }
    public string? DynamicCriteriaJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TargetGroupMember> Members { get; set; } = [];
}

public class Recipient
{
    public int Id { get; set; }
    public RecipientType Type { get; set; }
    public string ExternalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? OrgForm { get; set; }
    public string? NaceCode { get; set; }
    public string? PostalAddress { get; set; }
    public string? PostalCode { get; set; }
    public string? PostalCity { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TargetGroupMember> Memberships { get; set; } = [];
}

public class TargetGroupMember
{
    public int TargetGroupId { get; set; }
    public TargetGroup TargetGroup { get; set; } = null!;
    public int RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public enum TargetGroupType { Dynamic, Static }
public enum TargetGroupScope { Private, Shared }
public enum RecipientType { Organization, Person, Subscriber }
