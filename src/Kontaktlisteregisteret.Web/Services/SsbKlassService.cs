using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Kontaktlisteregisteret.Web.Services;

public record KlassKode(string Code, string Name, string Level, string? ParentCode);

public class SsbKlassService(HttpClient http, IMemoryCache cache)
{
    private const string SektorkodeKey  = "ssb_klass_sektorkoder";
    private const string NaeringskodeKey = "ssb_klass_naeringskoder";

    public Task<List<KlassKode>> GetSektorkodeAsync()
        => FetchAsync(SektorkodeKey, "/api/klass/v1/classifications/39/codes?from=2024-01-01&language=nb");

    public Task<List<KlassKode>> GetNaeringskodeAsync()
        => FetchAsync(NaeringskodeKey, "/api/klass/v1/classifications/6/codes?from=2025-09-01&language=nb");

    private async Task<List<KlassKode>> FetchAsync(string cacheKey, string path)
    {
        if (cache.TryGetValue(cacheKey, out List<KlassKode>? cached))
            return cached!;

        try
        {
            var json = await http.GetStringAsync(path);
            using var doc = JsonDocument.Parse(json);
            var codes = doc.RootElement.GetProperty("codes")
                .EnumerateArray()
                .Select(e => new KlassKode(
                    Code: e.GetProperty("code").GetString() ?? "",
                    Name: e.GetProperty("name").GetString() ?? "",
                    Level: e.GetProperty("level").GetString() ?? "",
                    ParentCode: e.TryGetProperty("parentCode", out var pc) && pc.ValueKind != JsonValueKind.Null
                        ? pc.GetString() : null))
                .ToList();

            cache.Set(cacheKey, codes, TimeSpan.FromHours(24));
            return codes;
        }
        catch
        {
            // Fallback: returner tom liste dersom API er utilgjengelig
            return [];
        }
    }
}
