using Kontaktlisteregisteret.Web.Data;
using Kontaktlisteregisteret.Web.Services;
using Xunit;

namespace Kontaktlisteregisteret.Tests.Services;

/// <summary>
/// Tester dedup-logikken i AdresselisteService.GetLiveMottakere.
/// Nøkkel: (RecipientId, Visningsnavn, CoAdresse) — alle tre må være like for å anses som duplikat.
/// </summary>
public class DedupTests
{
    // ── Hjelpere ────────────────────────────────────────────────────────────

    private static Recipient Org(int id, string navn) => new()
    {
        Id = id,
        ExternalId = id.ToString().PadLeft(9, '0'),
        Name = navn,
        Type = RecipientType.Organization,
        IsActive = true
    };

    private static TargetGroupMember Member(Recipient r, string? visningsnavn = null, string? coAdresse = null) => new()
    {
        RecipientId = r.Id,
        Recipient = r,
        Visningsnavn = visningsnavn,
        CoAdresse = coAdresse
    };

    private static Adresseliste ListeMed(params (int rekkefølge, TargetGroup gruppe)[] grupper)
    {
        var liste = new Adresseliste { Tittel = "Test" };
        foreach (var (rekkefølge, gruppe) in grupper)
            liste.Målgrupper.Add(new AdresselisteMålgruppe
            {
                Rekkefølge = rekkefølge,
                MålgruppeId = gruppe.Id,
                Målgruppe = gruppe
            });
        return liste;
    }

    private static TargetGroup Gruppe(int id, params TargetGroupMember[] members)
    {
        var g = new TargetGroup { Id = id, Name = $"Gruppe {id}" };
        g.Members.AddRange(members);
        return g;
    }

    // ── Tester ──────────────────────────────────────────────────────────────

    [Fact]
    public void SammeOrg_IToGrupper_VisesBareEnGang()
    {
        var rødeKors = Org(1, "Norges Røde Kors");
        var liste = ListeMed(
            (1, Gruppe(1, Member(rødeKors))),
            (2, Gruppe(2, Member(rødeKors))));

        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Single(result);
    }

    [Fact]
    public void SammeOrg_UliktVisningsnavn_VisesBegge()
    {
        // c/o-mønster: Røde Kors lagt til to ganger med ulikt navn (forum-trick)
        var rødeKors = Org(1, "Norges Røde Kors");
        var gruppe = Gruppe(1,
            Member(rødeKors, visningsnavn: "Norges Røde Kors"),
            Member(rødeKors, visningsnavn: "Forum for Barnekonvensjonen"));

        var liste = ListeMed((1, gruppe));
        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Visningsnavn == "Norges Røde Kors");
        Assert.Contains(result, m => m.Visningsnavn == "Forum for Barnekonvensjonen");
    }

    [Fact]
    public void SammeOrg_UlikCoAdresse_VisesBegge()
    {
        var org = Org(1, "Stiftelsen");
        var gruppe = Gruppe(1,
            Member(org, coAdresse: "v/ Sentralt sekretariat"),
            Member(org, coAdresse: "v/ Avdeling Oslo"));

        var liste = ListeMed((1, gruppe));
        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EksaktDuplikat_VisningsnavnOgCoAdresseLike_VisesBareEnGang()
    {
        var org = Org(1, "Org");
        var gruppe = Gruppe(1,
            Member(org, visningsnavn: "Alias", coAdresse: "c/o Noen"),
            Member(org, visningsnavn: "Alias", coAdresse: "c/o Noen")); // nøyaktig samme

        var liste = ListeMed((1, gruppe));
        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Single(result);
    }

    [Fact]
    public void NullOgTomStringVisningsnavn_ErForskjellige()
    {
        // null og "" er ikke like som nøkkel — string-equality i HashSet
        var org = Org(1, "Org");
        var gruppe = Gruppe(1,
            Member(org, visningsnavn: null),
            Member(org, visningsnavn: ""));

        var liste = ListeMed((1, gruppe));
        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MangeForskelligeOrger_AlleVises()
    {
        var orger = Enumerable.Range(1, 10)
            .Select(i => Org(i, $"Org {i}"))
            .ToArray();
        var gruppe = Gruppe(1, orger.Select(o => Member(o)).ToArray());
        var liste = ListeMed((1, gruppe));

        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void TomListe_GirTomtResultat()
    {
        var liste = new Adresseliste { Tittel = "Tom" };

        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Empty(result);
    }

    [Fact]
    public void Rekkefølge_RespekteresVedDuplikat_FørsteGruppeVinner()
    {
        // Org finnes i gruppe 2 og 1 — gruppe 1 (lavere rekkefølge) skal vinne
        var org = Org(1, "Org");
        var gruppe1 = Gruppe(1, Member(org));
        var gruppe2 = Gruppe(2, Member(org));

        var liste = ListeMed((2, gruppe2), (1, gruppe1)); // bevisst feil rekkefølge i input
        var result = AdresselisteService.GetLiveMottakere(liste);

        Assert.Single(result);
        Assert.Equal(1, result[0].KildeMålgruppeId); // gruppe 1 vant
    }
}
