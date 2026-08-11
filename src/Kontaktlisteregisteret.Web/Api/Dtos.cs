using Kontaktlisteregisteret.Web.Services;

namespace Kontaktlisteregisteret.Web.Api;

/// Request-body for PUT /api/v1/malgrupper/{id}/kriterier
/// og som kriterier-del av POST /api/v1/malgrupper.
///
/// Alle felt er valgfrie. Intern DynamicCriteria.Municipality er utelatt —
/// feltet er deklarert i domeneklassen men aldri brukt i EvaluateDynamicCriteriaAsync.
///
/// Navneskifte ift. intern klasse:
///   OrgForm            → OrgForm (1:1)
///   NaceKode           → NacePrefix  (navn forenklet for konsumenter)
///   SektorKode         → SektorKode (1:1)
///   Virksomhetsstatus  → Aktivitet  (semantisk klarere navn)
///   AktivitetFilter    → AktivitetFilter (1:1)
///   InkluderUnderenheter → IncludeSubUnits (norsk)
///   EkskludertFraGruppe  → ExcludedOrgnrs (distinkt fra adresseliste-nivå ekskludering)
public record DynamicCriteriaDto(
    string? OrgForm,
    string? NaceKode,
    string? SektorKode,
    string? Virksomhetsstatus,
    string? AktivitetFilter,
    bool? InkluderUnderenheter,
    List<string>? EkskludertFraGruppe)
{
    /// Konverterer til intern DynamicCriteria med defaults for manglende felt.
    public DynamicCriteria TilIntern() => new()
    {
        OrgForm         = OrgForm,
        NacePrefix      = NaceKode,
        SektorKode      = SektorKode,
        Aktivitet       = Virksomhetsstatus ?? "aktive",
        AktivitetFilter = AktivitetFilter,
        IncludeSubUnits = InkluderUnderenheter ?? false,
        ExcludedOrgnrs  = EkskludertFraGruppe ?? []
    };
}

/// Request-body for POST /api/v1/malgrupper
public record OpprettMålgruppeRequest(
    /// "Statisk" eller "Dynamisk"
    string Type,
    string Navn,
    /// "Privat" | "Delt" — default "Delt" hvis utelatt
    string? Scope,
    /// Orgnr-liste — påkrevd og eneste relevante felt for type "Statisk"
    List<string>? Orgnr,
    /// Filterregler — kun relevant for type "Dynamisk"
    DynamicCriteriaDto? Kriterier);

/// Request-body for PATCH /api/v1/malgrupper/{id}
public record NavnEndreRequest(string Navn);
