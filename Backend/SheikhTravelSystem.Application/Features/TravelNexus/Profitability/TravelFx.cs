namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

/// <summary>
/// Converts amounts into the reporting currency with the rate in force on a date (latest rate with EffectiveFrom ≤ date).
/// A missing rate yields null and is recorded — amounts are never converted with a guessed rate.
/// </summary>
public sealed class TravelFx
{
    private readonly Dictionary<string, (DateTime From, decimal Rate)[]> _rates;
    private readonly Dictionary<string, (DateTime Earliest, int Count)> _missing = new(StringComparer.Ordinal);

    public TravelFx(string reportingCurrency, IEnumerable<(string Currency, DateTime From, decimal Rate)> rates)
    {
        ReportingCurrency = reportingCurrency;
        _rates = rates.GroupBy(r => r.Currency, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.From).Select(r => (r.From.Date, r.Rate)).ToArray(), StringComparer.Ordinal);
    }

    public string ReportingCurrency { get; }

    public decimal? Rate(string currency, DateTime date)
    {
        if (currency == ReportingCurrency) return 1m;
        if (!_rates.TryGetValue(currency, out var list)) return null;
        decimal? found = null;
        foreach (var (from, rate) in list)
        {
            if (from > date.Date) break;
            found = rate;
        }
        return found;
    }

    /// <summary>Unrounded conversion (rounding happens once, on totals).</summary>
    public decimal? Convert(decimal amount, string currency, DateTime date)
    {
        if (amount == 0) return 0;
        var rate = Rate(currency, date);
        if (rate is null)
        {
            _missing[currency] = _missing.TryGetValue(currency, out var m) ? (date < m.Earliest ? date : m.Earliest, m.Count + 1) : (date, 1);
            return null;
        }
        return amount * rate.Value;
    }

    public IReadOnlyList<(string Currency, DateTime Earliest, int Count)> Missing
        => _missing.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => (m.Key, m.Value.Earliest, m.Value.Count)).ToList();
}
