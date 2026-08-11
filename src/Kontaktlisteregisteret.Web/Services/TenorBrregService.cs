using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kontaktlisteregisteret.Web.Services;

/// <summary>
/// Implementasjon av <see cref="IBrregService"/> mot Tenor — Digdirs syntetiske testdata-API.
/// Brukes når konfigurasjonsnøkkelen <c>Tenor:Enabled</c> er <c>true</c>.
/// </summary>
public class TenorBrregService(HttpClient http, ILogger<TenorBrregService> logger) : IBrregService
{
    private const string BaseUrl = "https://tenor.test.brreg.no/enhetsregisteret/api";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string? LastError { get; private set; }
    public int LastTotalElements { get; private set; }
    public int LastTotalPages { get; private set; }

    private async Task<(T? Value, string? Error)> FetchAsync<T>(string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"HTTP {(int)response.StatusCode} fra Tenor ({url}): {body[..Math.Min(300, body.Length)]}";
                logger.LogWarning(msg);
                return (default, msg);
            }

            var result = JsonSerializer.Deserialize<T>(body, JsonOpts);
            return (result, null);
        }
        catch (TaskCanceledException)
        {
            var msg = $"Tidsavbrudd ved kall til Tenor ({url})";
            logger.LogWarning(msg);
            return (default, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Uventet feil mot Tenor: {ex.Message}";
            logger.LogError(ex, "Tenor call failed: {Url}", url);
            return (default, msg);
        }
    }

    public async Task<List<BrregEnhet>> SearchAsync(string query, string? orgform = null,
        string? nacePrefix = null, string? sektorKode = null, int size = 20, int page = 0)
    {
        LastError = null;
        LastTotalElements = 0;
        LastTotalPages = 1;
        var url = $"{BaseUrl}/enheter?size={size}&page={page}&{BuildParams(query, orgform, nacePrefix, sektorKode)}";
        var (result, error) = await FetchAsync<BrregSearchResult>(url);
        LastError = error;
        LastTotalElements = result?.page?.totalElements ?? 0;
        LastTotalPages = result?.page?.totalPages ?? 1;
        return result?.Embedded?.enheter ?? [];
    }

    public async Task<List<BrregEnhet>> SearchAllPagesAsync(string? orgform = null,
        string? nacePrefix = null, string? sektorKode = null, string? aktivitet = null)
    {
        LastError = null;
        const int size = 200;
        const int brregLimit = 10_000;
        var all = new List<BrregEnhet>();
        int page = 0, totalPages = 1;

        do
        {
            if (size * (page + 1) > brregLimit)
            {
                LastError = $"Tenor begrenser søk til {brregLimit:N0} treff. Returnerer de første {all.Count} enhetene.";
                break;
            }

            var url = $"{BaseUrl}/enheter?size={size}&page={page}&{BuildParams("", orgform, nacePrefix, sektorKode, aktivitet)}";
            var (result, error) = await FetchAsync<BrregSearchResult>(url);
            if (error is not null) { LastError = error; break; }
            if (result?.Embedded?.enheter is { } batch) all.AddRange(batch);
            totalPages = result?.page?.totalPages ?? 1;
            page++;
        } while (page < totalPages);

        return all;
    }

    public async Task<BrregEnhet?> GetByOrgnrAsync(string orgnr)
    {
        LastError = null;
        var clean = orgnr.Replace(" ", "");

        var (enhet, enheterError) = await FetchAsync<BrregEnhet>($"{BaseUrl}/enheter/{clean}");
        if (enhet is not null) return enhet;

        var (under, underError) = await FetchAsync<BrregEnhet>($"{BaseUrl}/underenheter/{clean}");
        if (under is not null) return under;

        LastError = "Fant ikke organisasjonen i Tenor testregister.";
        logger.LogWarning("Orgnr {Orgnr} ikke funnet i Tenor: enheter={E}, underenheter={U}", clean, enheterError, underError);
        return null;
    }

    public async Task<List<BrregEnhet>> GetChildrenAsync(string orgnr)
    {
        LastError = null;
        var all = new List<BrregEnhet>();

        var (enheterResult, _) = await FetchAsync<BrregSearchResult>(
            $"{BaseUrl}/enheter?overordnetEnhet={orgnr}&size=200");
        if (enheterResult?.Embedded?.enheter is { } e) all.AddRange(e);

        var (underResult, error) = await FetchAsync<BrregUnderenhetResult>(
            $"{BaseUrl}/underenheter?overordnetEnhet={orgnr}&size=200");
        if (underResult?.Embedded?.underenheter is { } u) all.AddRange(u);
        if (error is not null) LastError = error;

        return all.DistinctBy(x => x.organisasjonsnummer).ToList();
    }

    public async Task<List<OrgnrValidationResult>> ValidateOrgnrListAsync(IEnumerable<string> orgnrs)
    {
        var results = new List<OrgnrValidationResult>();
        foreach (var raw in orgnrs)
        {
            var orgnr = raw.Trim().Replace(" ", "");
            if (orgnr.Length != 9 || !orgnr.All(char.IsDigit))
            {
                results.Add(new(orgnr, null, ValidationStatus.InvalidFormat));
                continue;
            }
            var enhet = await GetByOrgnrAsync(orgnr);
            if (enhet is null)
                results.Add(new(orgnr, null, ValidationStatus.NotFound));
            else if (enhet.slettedato is not null)
                results.Add(new(orgnr, enhet, ValidationStatus.Deleted));
            else
                results.Add(new(orgnr, enhet, ValidationStatus.Ok));
        }
        return results;
    }

    public async Task<List<BrregEnhet>> EvaluateDynamicCriteriaAsync(DynamicCriteria criteria)
    {
        LastError = null;
        var top = await SearchAllPagesAsync(criteria.OrgForm, criteria.NacePrefix, criteria.SektorKode, criteria.Aktivitet);

        if (criteria.IncludeSubUnits)
        {
            var all = new List<BrregEnhet>(top);
            foreach (var enhet in top)
            {
                var children = await GetChildrenAsync(enhet.organisasjonsnummer);
                all.AddRange(children);
            }
            top = all.DistinctBy(e => e.organisasjonsnummer).ToList();
        }

        if (!string.IsNullOrWhiteSpace(criteria.AktivitetFilter))
        {
            var af = criteria.AktivitetFilter;
            top = top.Where(e =>
                e.aktivitet?.Any(a => a.Contains(af, StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();
        }

        if (criteria.ExcludedOrgnrs.Count > 0)
        {
            var excluded = criteria.ExcludedOrgnrs.ToHashSet();
            top = top.Where(e => !excluded.Contains(e.organisasjonsnummer)).ToList();
        }

        return top;
    }

    private static string BuildParams(string query, string? orgform, string? nacePrefix,
        string? sektorKode = null, string? aktivitet = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Replace(" ", "");
            parts.Add(q.Length == 9 && q.All(char.IsDigit)
                ? $"organisasjonsnummer={q}"
                : $"navn={Uri.EscapeDataString(query)}");
        }
        if (!string.IsNullOrWhiteSpace(orgform)) parts.Add($"organisasjonsform={orgform}");
        if (!string.IsNullOrWhiteSpace(nacePrefix)) parts.Add($"naeringskode={Uri.EscapeDataString(nacePrefix)}");
        if (!string.IsNullOrWhiteSpace(sektorKode)) parts.Add($"institusjonellSektorkode={sektorKode}");
        switch (aktivitet)
        {
            case "aktive":
                parts.Add("konkurs=false");
                parts.Add("underAvvikling=false");
                break;
            case "konkurs":
                parts.Add("konkurs=true");
                break;
            case "avvikling":
                parts.Add("underAvvikling=true");
                break;
        }
        return string.Join("&", parts);
    }
}
