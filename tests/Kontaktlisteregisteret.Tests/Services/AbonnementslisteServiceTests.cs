using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;
using Kontaktlisteregisteret.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kontaktlisteregisteret.Tests.Services;

public class AbonnementslisteServiceTests
{
    private static AppDbContext LagDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static async Task<(AbonnementslisteService svc, int listeId)> OppsettAsync()
    {
        var db = LagDb();
        var liste = new Abonnementsliste { Navn = "Testliste" };
        db.Abonnementslister.Add(liste);
        await db.SaveChangesAsync();

        var svc = new AbonnementslisteService(db);
        return (svc, liste.Id);
    }

    [Fact]
    public async Task GyldigEpost_LeggesInn_ReturnererOk()
    {
        var (svc, listeId) = await OppsettAsync();

        var (ok, error, abonnent) = await svc.LeggTilAsync(listeId, "ola@example.com", AbonnentKilde.Api);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(abonnent);
        Assert.Equal("ola@example.com", abonnent!.Epost);
    }

    [Theory]
    [InlineData("ikke-en-epost")]      // ingen @
    [InlineData("@ingenlokal.no")]     // mangler lokal-del
    [InlineData("")]                   // tom streng
    [InlineData("   ")]                // bare whitespace
    // Merk: "mangler@domene" godtas av MailAddress.TryCreate (gyldig per RFC 5321)
    public async Task UgyldigEpost_ReturnererFeil(string ugyldigEpost)
    {
        var (svc, listeId) = await OppsettAsync();

        var (ok, error, _) = await svc.LeggTilAsync(listeId, ugyldigEpost, AbonnentKilde.Api);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task DuplikatEpost_ReturnererFeil()
    {
        var (svc, listeId) = await OppsettAsync();
        await svc.LeggTilAsync(listeId, "ola@example.com", AbonnentKilde.Api);

        var (ok, error, _) = await svc.LeggTilAsync(listeId, "ola@example.com", AbonnentKilde.Api);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task SammeEpost_UlisteListeId_ErTillatt()
    {
        var db = LagDb();
        var liste1 = new Abonnementsliste { Navn = "Liste 1" };
        var liste2 = new Abonnementsliste { Navn = "Liste 2" };
        db.Abonnementslister.AddRange(liste1, liste2);
        await db.SaveChangesAsync();
        var svc = new AbonnementslisteService(db);

        var (ok1, _, _) = await svc.LeggTilAsync(liste1.Id, "ola@example.com", AbonnentKilde.Api);
        var (ok2, _, _) = await svc.LeggTilAsync(liste2.Id, "ola@example.com", AbonnentKilde.Api);

        Assert.True(ok1);
        Assert.True(ok2);
    }

    [Fact]
    public async Task ListeFinnesIkke_ReturnererFeil()
    {
        var (svc, _) = await OppsettAsync();

        var (ok, error, _) = await svc.LeggTilAsync(9999, "ola@example.com", AbonnentKilde.Api);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task SlettAbonnent_SomFinnes_ReturnererTrue()
    {
        var (svc, listeId) = await OppsettAsync();
        var (_, _, abonnent) = await svc.LeggTilAsync(listeId, "ola@example.com", AbonnentKilde.Api);

        var ok = await svc.SlettAbonnentAsync(abonnent!.Id);

        Assert.True(ok);
    }

    [Fact]
    public async Task SlettAbonnent_SomIkkeFinnes_ReturnererFalse()
    {
        var (svc, _) = await OppsettAsync();

        var ok = await svc.SlettAbonnentAsync(9999);

        Assert.False(ok);
    }
}
