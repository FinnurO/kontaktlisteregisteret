using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;
using Xunit;

namespace Kontaktlisteregisteret.Tests.Api;

/// <summary>
/// Integrasjonstester for de eksternt eksponerte API-endepunktene.
/// Bruker WebApplicationFactory med SQLite-testdatabase — ingen seed-data.
/// Alle kall er scopet til virksomhetsorgnr "991825827".
/// </summary>
public class ApiContractTests : IClassFixture<KontaktlisteFactory>
{
    private readonly HttpClient _client;
    private readonly KontaktlisteFactory _factory;

    private const string TestOrgnr = "991825827";
    private const string BaseUrl = $"/api/v1/virksomheter/{TestOrgnr}";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiContractTests(KontaktlisteFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// Oppretter eller henter demo-virksomheten for testorgnr.
    private async Task<int> HentEllerOpprettVirksomhetAsync()
    {
        using var db = _factory.CreateDbContext();
        var eksisterende = await db.Virksomheter.FirstOrDefaultAsync(v => v.Orgnr == TestOrgnr);
        if (eksisterende is not null) return eksisterende.Id;

        var v = new Virksomhet
        {
            Orgnr = TestOrgnr,
            Navn = "Testvirksomheten",
            Status = VirksomhetStatus.Aktiv
        };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    // ── GET /api/v1/virksomheter/{orgnr}/adresselister ──────────────────────────

    [Fact]
    public async Task GetAdresselister_Returnerer200OgArray()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lister = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts);
        Assert.NotNull(lister); // Alltid et array, aldri null
    }

    [Fact]
    public async Task GetAdresselister_KunUtkast_ReturnererIkkeUtkast()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        // Legg til en Utkast-liste — skal IKKE dukke opp i API-svaret
        using var db = _factory.CreateDbContext();
        db.Adresselister.Add(new Adresseliste
        {
            Tittel = "Skjult utkast",
            Status = AdresselisteStatus.Utkast,
            VirksomhetId = virksomhetId
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lister = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts);
        // Utkast-listen skal ikke være med — andre tester kan ha lagt til låste lister
        Assert.DoesNotContain(lister!, l => l.GetProperty("tittel").GetString() == "Skjult utkast");
    }

    [Fact]
    public async Task GetAdresselister_MedLåstListe_ReturnererDen()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        db.Adresselister.Add(new Adresseliste
        {
            Tittel = "Høring 2026",
            Status = AdresselisteStatus.Låst,
            LåstAt = DateTime.UtcNow,
            VirksomhetId = virksomhetId
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lister = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts);
        Assert.Contains(lister!, l => l.GetProperty("tittel").GetString() == "Høring 2026");
    }

    // ── GET /api/v1/virksomheter/{orgnr}/adresselister/{id} ─────────────────────

    [Fact]
    public async Task GetAdresselisteById_FinnesIkke_Returnerer404()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAdresselisteById_ErUtkast_Returnerer404()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var liste = new Adresseliste
        {
            Tittel = "Utkast",
            Status = AdresselisteStatus.Utkast,
            VirksomhetId = virksomhetId
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/{liste.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAdresselisteById_Låst_Returnerer200MedFelter()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var liste = new Adresseliste
        {
            Tittel = "Låst liste",
            Beskrivelse = "Testbeskrivelse",
            Status = AdresselisteStatus.Låst,
            LåstAt = DateTime.UtcNow,
            VirksomhetId = virksomhetId
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/{liste.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Låst liste", body.GetProperty("tittel").GetString());
        Assert.Equal("Testbeskrivelse", body.GetProperty("beskrivelse").GetString());
        Assert.Equal(0, body.GetProperty("antallMottakere").GetInt32());
    }

    // ── GET /api/v1/virksomheter/{orgnr}/adresselister/{id}/mottakere ───────────

    [Fact]
    public async Task GetMottakere_ListeFinnesIkke_Returnerer404()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/99999/mottakere");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMottakere_LåstListeMedMottaker_ReturnererKorrektefelter()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var recipient = new Recipient
        {
            ExternalId = "964967725",
            Name = "Stavanger kommune",
            Type = RecipientType.Organization,
            OrgForm = "KOMM"
        };
        db.Recipients.Add(recipient);
        await db.SaveChangesAsync();

        var liste = new Adresseliste
        {
            Tittel = "Med mottaker",
            Status = AdresselisteStatus.Låst,
            VirksomhetId = virksomhetId
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();

        db.AdresselisteMottakere.Add(new AdresselisteMottaker
        {
            AdresselisteId = liste.Id,
            RecipientId = recipient.Id,
            Visningsnavn = null,
            CoAdresse = null
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/{liste.Id}/mottakere");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mottakere = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts);
        Assert.Single(mottakere!);
        var m = mottakere![0];
        Assert.Equal("964967725", m.GetProperty("organisasjonsnummer").GetString());
        Assert.Equal("Stavanger kommune", m.GetProperty("navn").GetString());
        Assert.Equal("organization", m.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetMottakere_MedVisningsnavn_ReturnererBrregNavnOgVisningsnavn()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var recipient = new Recipient
        {
            ExternalId = "864139442",
            Name = "Norges Røde Kors",
            Type = RecipientType.Organization
        };
        db.Recipients.Add(recipient);
        await db.SaveChangesAsync();

        var liste = new Adresseliste
        {
            Tittel = "c/o test",
            Status = AdresselisteStatus.Låst,
            VirksomhetId = virksomhetId
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();

        db.AdresselisteMottakere.Add(new AdresselisteMottaker
        {
            AdresselisteId = liste.Id,
            RecipientId = recipient.Id,
            Visningsnavn = "Forum for Barnekonvensjonen",
            CoAdresse = "c/o Redd Barna"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"{BaseUrl}/adresselister/{liste.Id}/mottakere");
        var mottakere = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts);
        var m = mottakere![0];

        Assert.Equal("Forum for Barnekonvensjonen", m.GetProperty("navn").GetString());
        Assert.Equal("Norges Røde Kors", m.GetProperty("brregNavn").GetString());
        Assert.Equal("c/o Redd Barna", m.GetProperty("coAdresse").GetString());
    }

    // ── GET /api/v1/virksomheter/{orgnr}/abonnementslister ──────────────────────

    [Fact]
    public async Task GetAbonnementslister_Returnerer200()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.GetAsync($"{BaseUrl}/abonnementslister");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── POST /api/v1/virksomheter/{orgnr}/abonnementslister/{id}/abonnenter ─────

    [Fact]
    public async Task PostAbonnent_GyldigEpost_Returnerer201()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var liste = new Abonnementsliste { Navn = "Liste", VirksomhetId = virksomhetId };
        db.Abonnementslister.Add(liste);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/abonnementslister/{liste.Id}/abonnenter",
            new { epost = "test@example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task PostAbonnent_DuplikatEpost_Returnerer409()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var liste = new Abonnementsliste { Navn = "Liste", VirksomhetId = virksomhetId };
        db.Abonnementslister.Add(liste);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync(
            $"{BaseUrl}/abonnementslister/{liste.Id}/abonnenter",
            new { epost = "duplikat@example.com" });

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/abonnementslister/{liste.Id}/abonnenter",
            new { epost = "duplikat@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostAbonnent_ListeFinnesIkke_Returnerer404EllerConflict()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/abonnementslister/99999/abonnenter",
            new { epost = "test@example.com" });

        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    // ── DELETE /api/v1/virksomheter/{orgnr}/abonnenter/{id} ─────────────────────

    [Fact]
    public async Task DeleteAbonnent_SomFinnes_Returnerer204()
    {
        var virksomhetId = await HentEllerOpprettVirksomhetAsync();

        using var db = _factory.CreateDbContext();
        var liste = new Abonnementsliste { Navn = "Liste", VirksomhetId = virksomhetId };
        db.Abonnementslister.Add(liste);
        await db.SaveChangesAsync();

        var postResponse = await _client.PostAsJsonAsync(
            $"{BaseUrl}/abonnementslister/{liste.Id}/abonnenter",
            new { epost = "slett@example.com" });
        var body = await postResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var id = body.GetProperty("id").GetInt32();

        var deleteResponse = await _client.DeleteAsync($"{BaseUrl}/abonnenter/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAbonnent_FinnesIkke_Returnerer404()
    {
        await HentEllerOpprettVirksomhetAsync();

        var response = await _client.DeleteAsync($"{BaseUrl}/abonnenter/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
