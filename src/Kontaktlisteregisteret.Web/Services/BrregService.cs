using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Kontaktlisteregisteret.Web.Services;

public class BrregService(HttpClient http, ILogger<BrregService> logger)
{
    private const string BaseUrl = "https://data.brreg.no/enhetsregisteret/api";

    public async Task<List<BrregEnhet>> SearchAsync(string query, string? orgform = null, string? nacePrefix = null, int size = 20)
    {
        var url = $"{BaseUrl}/enheter?size={size}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (query.Replace(" ", "").All(char.IsDigit) && query.Replace(" ", "").Length == 9)
                url += $"&organisasjonsnummer={query.Replace(" ", "")}";
            else
                url += $"&navn={Uri.EscapeDataString(query)}";
        }
        if (!string.IsNullOrWhiteSpace(orgform)) url += $"&organisasjonsform={orgform}";
        if (!string.IsNullOrWhiteSpace(nacePrefix)) url += $"&naeringskode={Uri.EscapeDataString(nacePrefix)}";

        try
        {
            var result = await http.GetFromJsonAsync<BrregSearchResult>(url);
            return result?._embedded?.enheter ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Brreg search failed for query: {Query}", query);
            return [];
        }
    }

    public async Task<BrregEnhet?> GetByOrgnrAsync(string orgnr)
    {
        try
        {
            return await http.GetFromJsonAsync<BrregEnhet>($"{BaseUrl}/enheter/{orgnr.Replace(" ", "")}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Brreg lookup failed for orgnr: {Orgnr}", orgnr);
            return null;
        }
    }

    public async Task<List<BrregEnhet>> GetChildrenAsync(string orgnr)
    {
        try
        {
            var result = await http.GetFromJsonAsync<BrregSearchResult>(
                $"{BaseUrl}/enheter?overordnetEnhet={orgnr}&size=50");
            return result?._embedded?.enheter ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Brreg children lookup failed for orgnr: {Orgnr}", orgnr);
            return [];
        }
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
            else if (enhet.Slettedato is not null)
                results.Add(new(orgnr, enhet, ValidationStatus.Deleted));
            else
                results.Add(new(orgnr, enhet, ValidationStatus.Ok));
        }
        return results;
    }

    public async Task<List<BrregEnhet>> EvaluateDynamicCriteriaAsync(DynamicCriteria criteria)
    {
        return await SearchAsync("", criteria.OrgForm, criteria.NacePrefix, size: 100);
    }
}

public record OrgnrValidationResult(string Orgnr, BrregEnhet? Enhet, ValidationStatus Status);
public enum ValidationStatus { Ok, NotFound, Deleted, InvalidFormat }

public class DynamicCriteria
{
    public string? OrgForm { get; set; }
    public string? NacePrefix { get; set; }
    public string? Municipality { get; set; }
}

public class BrregSearchResult
{
    public BrregEmbedded? _embedded { get; set; }
    public BrregPage? page { get; set; }
}

public class BrregEmbedded
{
    public List<BrregEnhet> enheter { get; set; } = [];
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
    public BrregNaeringskode? naeringskode1 { get; set; }
    public BrregAdresse? postadresse { get; set; }
    public BrregAdresse? forretningsadresse { get; set; }
    public int? antallAnsatte { get; set; }
    public string? slettedato { get; set; }
    public string? Slettedato => slettedato;
    public BrregOverordnetEnhet? overordnetEnhet { get; set; }

    public string DisplayAddress =>
        postadresse?.adresse is { Count: > 0 } a
            ? $"{string.Join(", ", a)}, {postadresse.postnummer} {postadresse.poststed}"
            : "";
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

public class BrregAdresse
{
    public List<string>? adresse { get; set; }
    public string? postnummer { get; set; }
    public string? poststed { get; set; }
    public string? kommunenummer { get; set; }
    public string? kommune { get; set; }
    public string? landkode { get; set; }
}

public class BrregOverordnetEnhet
{
    public string organisasjonsnummer { get; set; } = "";
    public string navn { get; set; } = "";
}
