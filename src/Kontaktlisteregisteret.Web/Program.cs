using Microsoft.EntityFrameworkCore;
using Kontaktlisteregisteret.Web.Data;
using Kontaktlisteregisteret.Web.Services;
using Kontaktlisteregisteret.Web.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Bytt til PostgreSQL: sett Database:Provider=postgresql og ConnectionStrings:Postgresql i appsettings
// For ny migrasjon mot PostgreSQL: dotnet ef migrations add <Namn> -- om nødvendig med --output-dir Migrations/Pg
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "sqlite";
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (dbProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
    {
        var pgConn = builder.Configuration.GetConnectionString("Postgresql")
            ?? "Host=localhost;Database=kontaktliste;Username=postgres;Password=postgres";
        opt.UseNpgsql(pgConn);
    }
    else
    {
        opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=kontaktliste.db");
    }
});

if (builder.Configuration.GetValue<bool>("Tenor:Enabled"))
{
    builder.Services.AddHttpClient<IBrregService, TenorBrregService>(c =>
    {
        c.BaseAddress = new Uri("https://tenor.test.brreg.no");
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.Timeout = TimeSpan.FromSeconds(15);
    });
}
else
{
    builder.Services.AddHttpClient<IBrregService, BrregService>(c =>
    {
        c.BaseAddress = new Uri("https://data.brreg.no");
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.Timeout = TimeSpan.FromSeconds(15);
    });
}

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<SsbKlassService>(c =>
{
    c.BaseAddress = new Uri("https://data.ssb.no");
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<TargetGroupService>();
builder.Services.AddScoped<AdresselisteService>();
builder.Services.AddScoped<AbonnementslisteService>();
builder.Services.AddScoped<VirksomhetService>();
builder.Services.AddScoped<VirksomhetContext>();
builder.Services.AddScoped<VarslingsService>();

var app = builder.Build();

// ── Database setup + seed ────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Kjør ventende migrasjoner — oppretter databasen om den ikke finnes ennå
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Ikke slett en eksisterende database dersom migrering feiler. En feil her kan
        // skyldes blant annet feil tilkoblingsstreng, manglende rettigheter eller en
        // uforenlig migrasjon og må håndteres eksplisitt av operatøren.
        app.Logger.LogCritical(ex, "Kunne ikke migrere databasen. Oppstart avbrytes uten å endre eksisterende data.");
        throw;
    }
    if (!app.Configuration.GetValue<bool>("SkipSeed"))
        await SeedData.SeedAsync(db);
}

// ── API v1 ────────────────────────────────────────────────────────────────────

app.MapApiV1();

// ── Web ───────────────────────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// Eksponerer Program-klassen for WebApplicationFactory i testprosjektet
public partial class Program { }
