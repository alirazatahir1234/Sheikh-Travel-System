using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings;

/// <summary>Airport-local ↔ UTC conversion for flight times (flights are printed in local time at each airport).</summary>
public static class TravelFlightTimes
{
    /// <summary>Resolves an IANA zone on any host (ICU on Linux/macOS; Windows falls back to the mapped Windows id).</summary>
    public static TimeZoneInfo? FindZone(string ianaId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId)
                ? TimeZoneInfo.FindSystemTimeZoneById(windowsId)
                : null;
        }
    }

    /// <summary>
    /// Converts a local wall-clock time to UTC. Returns null for a time that does not exist (skipped by a DST change).
    /// An ambiguous time (clocks going back) resolves to the earlier instant (the daylight offset).
    /// </summary>
    public static DateTime? ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified)) return null;
        if (zone.IsAmbiguousTime(unspecified))
        {
            var offset = zone.GetAmbiguousTimeOffsets(unspecified).Max();
            return DateTime.SpecifyKind(unspecified - offset, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }
}

public static class TravelBookingRules
{
    /// <summary>Penalties and refunds must be non-negative, in the service currency, and within what was sold / bought.</summary>
    public static string? ValidateCancellation(CancelTravelServiceDto c, decimal? sell, decimal? cost, string? serviceCurrency)
    {
        if (c.SupplierPenalty < 0 || c.CustomerPenalty < 0 || c.RefundFromSupplier < 0 || c.RefundToCustomer < 0)
            return "Penalties and refunds cannot be negative.";
        var anyAmount = c.SupplierPenalty > 0 || c.CustomerPenalty > 0 || c.RefundFromSupplier > 0 || c.RefundToCustomer > 0;
        if (anyAmount && serviceCurrency is null) return "Record the service's price and currency before entering penalties or refunds.";
        if (sell is decimal s && c.RefundToCustomer + c.CustomerPenalty > s)
            return $"Refund to customer plus customer penalty cannot exceed the sell amount ({s:0.##} {serviceCurrency}).";
        if (cost is decimal k && c.RefundFromSupplier + c.SupplierPenalty > k)
            return $"Refund from supplier plus supplier penalty cannot exceed the cost amount ({k:0.##} {serviceCurrency}).";
        if (sell is null && (c.RefundToCustomer > 0 || c.CustomerPenalty > 0)) return "The service has no sell amount to refund against.";
        if (cost is null && (c.RefundFromSupplier > 0 || c.SupplierPenalty > 0)) return "The service has no cost amount to refund against.";
        return null;
    }

    /// <summary>Amounts need a currency, and are never negative.</summary>
    public static string? ValidateAmounts(decimal? sell, decimal? cost, string? currency)
    {
        if (sell < 0 || cost < 0) return "Amounts cannot be negative.";
        if ((sell is not null || cost is not null) && !Common.TravelValues.IsCurrencyCode(currency)) return "Amounts need a 3-letter currency code.";
        return null;
    }

    /// <summary>Sell and cost of non-cancelled services, per currency.</summary>
    public static IReadOnlyList<TravelBookingTotalDto> Totals(IEnumerable<TravelFlightBookingDto> flights, IEnumerable<TravelHotelBookingDto> hotels)
    {
        var rows = flights.Where(f => f.Status != "Cancelled").Select(f => (f.CurrencyCode, f.SellAmount, f.CostAmount))
            .Concat(hotels.Where(h => h.Status != "Cancelled").Select(h => (h.CurrencyCode, h.SellAmount, h.CostAmount)))
            .Where(r => r.CurrencyCode is not null && (r.SellAmount is not null || r.CostAmount is not null));
        return rows.GroupBy(r => r.CurrencyCode!)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TravelBookingTotalDto { CurrencyCode = g.Key, Sell = g.Sum(r => r.SellAmount ?? 0), Cost = g.Sum(r => r.CostAmount ?? 0) })
            .ToList();
    }

    /// <summary>
    /// A pilgrim is covered when they hold an issued ticket on a ticketed round trip, or on both an outbound and a return flight.
    /// Returns the traveler ids that are not covered.
    /// </summary>
    public static IReadOnlyList<int> UncoveredTravelers(IEnumerable<int> travelerIds, IEnumerable<TravelGroupTicketFact> tickets)
    {
        var byTraveler = tickets.GroupBy(t => t.TravelerId).ToDictionary(g => g.Key, g => g.Select(t => t.Direction).ToHashSet());
        return travelerIds.Where(id => !byTraveler.TryGetValue(id, out var d)
            || !(d.Contains("RoundTrip") || (d.Contains("Outbound") && d.Contains("Return")))).ToList();
    }

    /// <summary>The file can complete when nothing is left to fly or stay and the last service is in the past.</summary>
    public static string? CompleteBlocker(TravelBookingServiceFacts f, DateTime utcNow)
    {
        if (f.UnfinishedServices > 0) return $"{f.UnfinishedServices} service(s) are not finished (ticketed, checked out or cancelled).";
        if (f.LastServiceUtc is DateTime last && last > utcNow) return "The trip has not ended yet.";
        return null;
    }

    public static string? CancelBlocker(TravelBookingServiceFacts f)
        => f.OpenFlights + f.OpenHotels > 0 ? "Cancel every flight and hotel booking first (penalties and refunds are recorded per service)." : null;
}
