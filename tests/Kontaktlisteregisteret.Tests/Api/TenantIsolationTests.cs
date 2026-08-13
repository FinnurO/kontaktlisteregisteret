using System.Net;
using Kontaktlisteregisteret.Web.Data;
using Xunit;

namespace Kontaktlisteregisteret.Tests.Api;

/// <summary>
/// Verifiserer at API-endepunktene respekterer virksomhetsisolasjon:
/// én virksomhet skal ikke kunne lese data tilhørende en annen.
/// </summary>
public class TenantIsolationTests : IClassFixture<KontaktlisteFactory>
{
    private readonly HttpClient _client;
    private readonly KontaktlisteFactory _factory;

    public TenantIsolationTests(KontaktlisteFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdresselisterFraAnnenVirksomhet_Returnerer404()
    {
        using var db = _factory.CreateDbContext();

        var virksomhetA = new Virksomhet { Orgnr = "111111111", Navn = "Virksomhet A", Status = VirksomhetStatus.Aktiv };
        var virksomhetB = new Virksomhet { Orgnr = "222222222", Navn = "Virksomhet B", Status = VirksomhetStatus.Aktiv };
        db.Virksomheter.AddRange(virksomhetA, virksomhetB);
        await db.SaveChangesAsync();

        var liste = new Adresseliste
        {
            Tittel = "B sin liste",
            Status = AdresselisteStatus.Låst,
            LåstAt = DateTime.UtcNow,
            VirksomhetId = virksomhetB.Id
        };
        db.Adresselister.Add(liste);
        await db.SaveChangesAsync();

        // Virksomhet A forsøker å lese virksomhet B sin liste
        var response = await _client.GetAsync(
            $"/api/v1/virksomheter/{virksomhetA.Orgnr}/adresselister/{liste.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UkjentVirksomhet_Returnerer404()
    {
        var response = await _client.GetAsync("/api/v1/virksomheter/999999999/malgrupper");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
