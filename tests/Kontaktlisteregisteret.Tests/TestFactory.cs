using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Tests;

/// <summary>
/// WebApplicationFactory med isolert SQLite-tempfil per factory-instans.
/// Bruker SQLite (ikke InMemory) for å unngå EF Core-providerkonflikten.
/// Seed hoppes over via SkipSeed-konfigurasjonsflagg.
/// </summary>
public class KontaktlisteFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"kontaktliste_test_{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Unik SQLite-fil per factory — unngår kollisjoner mellom testkjøringer
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                // Hopper over seed-data slik at testene starter med tom DB
                ["SkipSeed"] = "true"
            });
        });
    }

    /// Oppretter en scope og returnerer en fersk DbContext for test-oppsett.
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch (IOException) { /* best effort — filen kan være i bruk ved rask kjøring */ }
        }
    }
}
