using Kontaktlisteregisteret.Web.Data;

namespace Kontaktlisteregisteret.Web.Services;

/// Scoped — én instans per Blazor Server-circuit.
/// Cacher siste oppslåtte virksomhet for å unngå gjentatte DB-kall ved navigasjon innen samme circuit.
public class VirksomhetContext(VirksomhetService svc)
{
    public Virksomhet? Current { get; private set; }
    public bool IsLoaded { get; private set; }

    /// Sant når en orgnr er slått opp og ikke funnet (eller virksomheten er inaktiv).
    public bool IsUnknown => IsLoaded && Current is null;

    private string? _lastOrgnr;

    public async Task LoadAsync(string orgnr)
    {
        // Bruk cachet svar hvis samme orgnr som sist
        if (_lastOrgnr == orgnr && IsLoaded) return;
        _lastOrgnr = orgnr;
        Current = await svc.GetAktivByOrgnrAsync(orgnr);
        IsLoaded = true;
    }

    public void Reset()
    {
        Current = null;
        IsLoaded = false;
        _lastOrgnr = null;
    }

    /// Utleder orgnr fra relativ URL-sti.
    /// Eks: "/991825827/adresselister" → "991825827", "/admin/virksomheter" → null.
    public static string? ExtractOrgnrFromPath(string relativePath)
    {
        var path = relativePath.TrimStart('/');
        var slash = path.IndexOf('/');
        var first = slash >= 0 ? path[..slash] : path;
        return first.Length == 9 && first.All(char.IsDigit) ? first : null;
    }
}
