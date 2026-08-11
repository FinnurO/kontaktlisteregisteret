namespace Kontaktlisteregisteret.Web.Services;

public interface IBrregService
{
    /// Siste feilmelding fra Brreg, satt av siste kall. Null betyr suksess.
    string? LastError { get; }

    /// Totalt antall treff fra siste søk.
    int LastTotalElements { get; }

    /// Totalt antall sider fra siste søk.
    int LastTotalPages { get; }

    Task<List<BrregEnhet>> SearchAsync(string query, string? orgform = null,
        string? nacePrefix = null, string? sektorKode = null, int size = 20, int page = 0);

    Task<List<BrregEnhet>> SearchAllPagesAsync(string? orgform = null,
        string? nacePrefix = null, string? sektorKode = null, string? aktivitet = null);

    Task<BrregEnhet?> GetByOrgnrAsync(string orgnr);

    Task<List<BrregEnhet>> GetChildrenAsync(string orgnr);

    Task<List<OrgnrValidationResult>> ValidateOrgnrListAsync(IEnumerable<string> orgnrs);

    Task<List<BrregEnhet>> EvaluateDynamicCriteriaAsync(DynamicCriteria criteria);
}
