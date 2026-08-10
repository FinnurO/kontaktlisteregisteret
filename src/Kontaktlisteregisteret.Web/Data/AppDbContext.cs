using Microsoft.EntityFrameworkCore;

namespace Kontaktlisteregisteret.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Virksomhet> Virksomheter => Set<Virksomhet>();
    public DbSet<TargetGroup> TargetGroups => Set<TargetGroup>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<TargetGroupMember> TargetGroupMembers => Set<TargetGroupMember>();
    public DbSet<Adresseliste> Adresselister => Set<Adresseliste>();
    public DbSet<AdresselisteMålgruppe> AdresselisteMålgrupper => Set<AdresselisteMålgruppe>();
    public DbSet<AdresselisteMottaker> AdresselisteMottakere => Set<AdresselisteMottaker>();
    public DbSet<Abonnementsliste> Abonnementslister => Set<Abonnementsliste>();
    public DbSet<Abonnent> Abonnenter => Set<Abonnent>();
    public DbSet<AdresselisteAbonnementsliste> AdresselisteAbonnementslister => Set<AdresselisteAbonnementsliste>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Virksomhet>().HasIndex(x => x.Orgnr).IsUnique();

        b.Entity<TargetGroup>().HasIndex(x => x.Name);
        b.Entity<TargetGroup>().HasIndex(x => x.VirksomhetId);
        b.Entity<Recipient>().HasIndex(x => x.ExternalId).IsUnique();
        b.Entity<TargetGroupMember>().HasKey(x => x.Id);
        b.Entity<TargetGroupMember>()
            .HasIndex(x => new { x.TargetGroupId, x.RecipientId }); // ikke unik — tillater c/o-duplikater
        b.Entity<AdresselisteMålgruppe>()
            .HasKey(x => new { x.AdresselisteId, x.MålgruppeId });
        b.Entity<AdresselisteAbonnementsliste>()
            .HasKey(x => new { x.AdresselisteId, x.AbonnementslisteId });
        b.Entity<Adresseliste>().HasIndex(x => x.Status);
        b.Entity<Adresseliste>().HasIndex(x => x.VirksomhetId);
        b.Entity<Abonnent>()
            .HasIndex(x => new { x.AbonnementslisteId, x.Epost }).IsUnique();
        b.Entity<Abonnementsliste>().HasIndex(x => x.VirksomhetId);
    }
}

// ── Virksomhet ───────────────────────────────────────────────────────────────

public class Virksomhet
{
    public int Id { get; set; }
    /// Organisasjonsnummer — 9 siffer, unikt
    public string Orgnr { get; set; } = "";
    public string Navn { get; set; } = "";
    public VirksomhetStatus Status { get; set; } = VirksomhetStatus.Aktiv;
    public DateTime OnboardetAt { get; set; } = DateTime.UtcNow;
    public string? OnboardetAv { get; set; }
}

public enum VirksomhetStatus { Aktiv, Inaktiv }

public class TargetGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TargetGroupType Type { get; set; }
    public TargetGroupScope Scope { get; set; }
    public string? DynamicCriteriaJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? VirksomhetId { get; set; }
    public Virksomhet? Virksomhet { get; set; }
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
    /// Løpenummer — tillater samme Recipient å ligge i gruppen flere ganger med ulikt Visningsnavn
    public int Id { get; set; }
    public int TargetGroupId { get; set; }
    public TargetGroup TargetGroup { get; set; } = null!;
    public int RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// Vises istedenfor mottakerens Brreg-navn (f.eks. "Forum for Barnekonvensjonen")
    public string? Visningsnavn { get; set; }
    /// c/o-adresse eller tilleggsopplysning (f.eks. "c/o Redd Barna")
    public string? CoAdresse { get; set; }
}

public enum TargetGroupType { Dynamic, Static }
public enum TargetGroupScope { Private, Shared }
public enum RecipientType { Organization, Person, Subscriber }

// ── Adresseliste ────────────────────────────────────────────────────────────

public class Adresseliste
{
    public int Id { get; set; }
    public string Tittel { get; set; } = "";
    public string? Beskrivelse { get; set; }
    public AdresselisteStatus Status { get; set; } = AdresselisteStatus.Utkast;
    public string? OpprettetAv { get; set; }
    public DateTime OpprettetAt { get; set; } = DateTime.UtcNow;
    public DateTime? LåstAt { get; set; }
    /// JSON-serialisert List<string> med ExternalId (orgnr) for ekskluderte mottakere
    public string? EkskluderteJson { get; set; }
    public int? VirksomhetId { get; set; }
    public Virksomhet? Virksomhet { get; set; }
    public List<AdresselisteMålgruppe> Målgrupper { get; set; } = [];
    public List<AdresselisteAbonnementsliste> Abonnementslister { get; set; } = [];
    public List<AdresselisteMottaker> Mottakere { get; set; } = [];
}

public enum AdresselisteStatus { Utkast, Klar, Låst }

// ── Abonnementsliste ─────────────────────────────────────────────────────────

public class Abonnementsliste
{
    public int Id { get; set; }
    public string Navn { get; set; } = "";
    public string? Beskrivelse { get; set; }
    public DateTime OpprettetAt { get; set; } = DateTime.UtcNow;
    public string? OpprettetAv { get; set; }
    public int? VirksomhetId { get; set; }
    public Virksomhet? Virksomhet { get; set; }
    public List<Abonnent> Abonnenter { get; set; } = [];
}

public class Abonnent
{
    public int Id { get; set; }
    public int AbonnementslisteId { get; set; }
    public Abonnementsliste Abonnementsliste { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string Epost { get; set; } = "";
    public DateTime LagtTilAt { get; set; } = DateTime.UtcNow;
    public AbonnentKilde Kilde { get; set; } = AbonnentKilde.Manuell;
}

public enum AbonnentKilde { Manuell, Api }

/// Kobling mellom Adresseliste og Målgruppe (mange-til-mange)
public class AdresselisteMålgruppe
{
    public int AdresselisteId { get; set; }
    public Adresseliste Adresseliste { get; set; } = null!;
    public int MålgruppeId { get; set; }
    public TargetGroup Målgruppe { get; set; } = null!;
    public int Rekkefølge { get; set; }
}

/// Kobling mellom Adresseliste og Abonnementsliste (mange-til-mange)
public class AdresselisteAbonnementsliste
{
    public int AdresselisteId { get; set; }
    public Adresseliste Adresseliste { get; set; } = null!;
    public int AbonnementslisteId { get; set; }
    public Abonnementsliste Abonnementsliste { get; set; } = null!;
    public int Rekkefølge { get; set; }
}

/// Fryst snapshot — fylles ut ved låsing, endres aldri etterpå
public class AdresselisteMottaker
{
    public int Id { get; set; }
    public int AdresselisteId { get; set; }
    public Adresseliste Adresseliste { get; set; } = null!;
    public int RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;
    public int? KildeMålgruppeId { get; set; }
    public int? KildeAbonnementslisteId { get; set; }
    /// Fryst fra TargetGroupMember.Visningsnavn ved låsing
    public string? Visningsnavn { get; set; }
    /// Fryst fra TargetGroupMember.CoAdresse ved låsing
    public string? CoAdresse { get; set; }
}
