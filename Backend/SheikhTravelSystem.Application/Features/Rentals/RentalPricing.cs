using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Application.Features.Rentals;

public static class RentalPricing
{
    public static decimal CalculateTotal(string rateType, decimal rateAmount, DateTime startAt, DateTime endAt)
    {
        if (rateAmount < 0) rateAmount = 0;
        if (endAt <= startAt) return rateAmount;

        var span = endAt - startAt;
        var normalized = rateType.Trim();

        if (normalized.Equals(RentalRateTypes.Hourly, StringComparison.OrdinalIgnoreCase))
        {
            var hours = Math.Max(1, (int)Math.Ceiling(span.TotalHours));
            return Math.Round(hours * rateAmount, 2);
        }

        if (normalized.Equals(RentalRateTypes.Weekly, StringComparison.OrdinalIgnoreCase))
        {
            var weeks = Math.Max(1, (int)Math.Ceiling(span.TotalDays / 7d));
            return Math.Round(weeks * rateAmount, 2);
        }

        if (normalized.Equals(RentalRateTypes.Monthly, StringComparison.OrdinalIgnoreCase))
        {
            var months = Math.Max(1, (int)Math.Ceiling(span.TotalDays / 30d));
            return Math.Round(months * rateAmount, 2);
        }

        // Daily (default)
        var days = Math.Max(1, (int)Math.Ceiling(span.TotalDays));
        return Math.Round(days * rateAmount, 2);
    }

    public static int BillableUnits(string rateType, DateTime startAt, DateTime endAt)
    {
        if (endAt <= startAt) return 1;
        var span = endAt - startAt;
        var normalized = rateType.Trim();
        if (normalized.Equals(RentalRateTypes.Hourly, StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, (int)Math.Ceiling(span.TotalHours));
        if (normalized.Equals(RentalRateTypes.Weekly, StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, (int)Math.Ceiling(span.TotalDays / 7d));
        if (normalized.Equals(RentalRateTypes.Monthly, StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, (int)Math.Ceiling(span.TotalDays / 30d));
        return Math.Max(1, (int)Math.Ceiling(span.TotalDays));
    }

    public static string DefaultAgreementText(string rentalNumber, string vehicleLabel, string customerName)
        => $"""
           VEHICLE HIRE AGREEMENT

           Rental: {rentalNumber}
           Hirer: {customerName}
           Vehicle: {vehicleLabel}

           1. The hirer agrees to return the vehicle in the same condition, subject to fair wear and tear.
           2. A security deposit may be held and applied against damage, excess mileage, or unpaid charges.
           3. The hirer is responsible for traffic fines and fuel during the hire period unless otherwise agreed.
           4. Early return does not automatically entitle a refund unless approved by the operator.
           5. Damage discovered on return may be charged at repair cost plus administration.

           By signing, the hirer confirms they have read and accept these terms.
           """;
}
