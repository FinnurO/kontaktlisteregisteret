using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kontaktlisteregisteret.Web.Services;

public class BrregService(HttpClient http, ILogger<BrregService> logger)
{
    private const string BaseUrl = "https://data.brreg.no/enhetsregisteret/api";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Last error message surfaced to the UI
    public string? LastError { get; private set; }

    private async Task<(T? Value, string? Error)> FetchAsync<T>(string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"HTTP {(int)response.StatusCode} fra Brreg ({url}): {body[..Math.Min(300, body.Length)]}";
                logger.LogWarning(msg);
                return (default, msg);
            }

            var result = JsonSerializer.Deserialize<T>(body, JsonOpts);
            return (result, null);
        }
        catch (TaskCanceledException)
        {
            var msg = $"Tidsavbrudd ved kall til Brreg ({url})";
            logger.LogWarning(msg);
            return (default, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Uventet feil mot Brreg: {ex.Message}";
            logger.LogError(ex, "Brreg call failed: {Url}", url);
            return (default, msg);
        }
    }

    public int LastTotalElements { get; private set; }
    public int LastTotalPages { get; private set; }

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
        var all = new List<BrregEnhet>();
        int page = 0, totalPages = 1;

        do
        {
            var url = $"{BaseUrl}/enheter?size=200&page={page}&{BuildParams("", orgform, nacePrefix, sektorKode, aktivitet)}";
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

        // Prøv hovedenhet først
        var (enhet, enheterError) = await FetchAsync<BrregEnhet>($"{BaseUrl}/enheter/{clean}");
        if (enhet is not null) return enhet;

        // Hvis ikke funnet som enhet, prøv underenhet (driftsenhet/avdeling)
        var (under, underError) = await FetchAsync<BrregEnhet>($"{BaseUrl}/underenheter/{clean}");
        if (under is not null) return under;

        // Begge feilet — vis en brukervennlig melding uten interne URL-detaljer
        LastError = "Fant ikke organisasjonen i Enhetsregisteret.";
        logger.LogWarning("Orgnr {Orgnr} ikke funnet: enheter={E}, underenheter={U}", clean, enheterError, underError);
        return null;
    }

    public async Task<List<BrregEnhet>> GetChildrenAsync(string orgnr)
    {
        LastError = null;
        var all = new List<BrregEnhet>();

        // Enheter (overordnet enhet er registrert som mor)
        var (enheterResult, _) = await FetchAsync<BrregSearchResult>(
            $"{BaseUrl}/enheter?overordnetEnhet={orgnr}&size=200");
        if (enheterResult?.Embedded?.enheter is { } e) all.AddRange(e);

        // Underenheter (driftsenheter/avdelinger)
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

// --- Result types ---

public record OrgnrValidationResult(string Orgnr, BrregEnhet? Enhet, ValidationStatus Status);
public enum ValidationStatus { Ok, NotFound, Deleted, InvalidFormat }

public class DynamicCriteria
{
    public string? OrgForm { get; set; }
    public string? NacePrefix { get; set; }
    public string? Municipality { get; set; }
    public string? SektorKode { get; set; }
    // "aktive" | "konkurs" | "avvikling" | "" (alle)
    public string Aktivitet { get; set; } = "aktive";
    // Klient-side filter på aktivitet-feltet fra Brreg (f.eks. "Skole", "Barnehage")
    public string? AktivitetFilter { get; set; }
    public bool IncludeSubUnits { get; set; }
    public List<string> ExcludedOrgnrs { get; set; } = [];
}

// --- Brreg DTOs ---

public class BrregSearchResult
{
    [JsonPropertyName("_embedded")]
    public BrregEmbedded? Embedded { get; set; }
    public BrregPage? page { get; set; }
}

public class BrregEmbedded
{
    public List<BrregEnhet> enheter { get; set; } = [];
}

public class BrregUnderenhetResult
{
    [JsonPropertyName("_embedded")]
    public BrregUnderenhetEmbedded? Embedded { get; set; }
    public BrregPage? page { get; set; }
}

public class BrregUnderenhetEmbedded
{
    public List<BrregEnhet> underenheter { get; set; } = [];
}

public class BrregPage
{
    public int totalElements { get; set; }
    public int totalPages { get; set; }
}

public class BrregEnhet
{
    public string organisasjonsnummer { get; set; } = "";
    public string navn { get; set; } = "";
    public BrregOrganisasjonsform? organisasjonsform { get; set; }
    public List<string>? aktivitet { get; set; }
    public BrregNaeringskode? naeringskode1 { get; set; }
    public BrregNaeringskode? naeringskode2 { get; set; }
    public BrregNaeringskode? naeringskode3 { get; set; }
    public BrregSektorkode? institusjonellSektorkode { get; set; }
    public BrregAdresse? postadresse { get; set; }
    public BrregAdresse? forretningsadresse { get; set; }
    public int? antallAnsatte { get; set; }
    public string? stiftelsesdato { get; set; }
    public string? registreringsdatoEnhetsregisteret { get; set; }
    public string? slettedato { get; set; }
    public bool? konkurs { get; set; }
    public bool? underAvvikling { get; set; }
    /// Orgnr for overordnet enhet (Brreg returnerer kun orgnr som streng, ikke et objekt)
    public string? overordnetEnhet { get; set; }

    public string AktivitetDisplay =>
        aktivitet is { Count: > 0 } ? string.Join(", ", aktivitet) : "";

    public string DisplayAddress =>
        postadresse?.adresse is { Count: > 0 } a
            ? $"{string.Join(", ", a)}, {postadresse.postnummer} {postadresse.poststed}"
            : forretningsadresse?.adresse is { Count: > 0 } b
                ? $"{string.Join(", ", b)}, {forretningsadresse.postnummer} {forretningsadresse.poststed}"
                : "";

    public bool ErAktiv => slettedato is null && konkurs != true && underAvvikling != true;
}

public class BrregOrganisasjonsform
{
    public string kode { get; set; } = "";
    public string beskrivelse { get; set; } = "";
}

public class BrregNaeringskode
{
    public string kode { get; set; } = "";
    public string beskrivelse { get; set; } = "";
}

public class BrregSektorkode
{
    public string kode { get; set; } = "";
    public string beskrivelse { get; set; } = "";
}

public class BrregAdresse
{
    public List<string>? adresse { get; set; }
    public string? postnummer { get; set; }
    public string? poststed { get; set; }
    public string? kommunenummer { get; set; }
    public string? kommune { get; set; }
    public string? landkode { get; set; }
}

