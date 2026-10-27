namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Product policy for driver in-app wallet cash-outs (PKR).
/// Tenant-specific overrides can be added later via settings; defaults are approved by product.
/// </summary>
public static class DriverWalletPolicy
{
    public const string CurrencyCode = "PKR";

    /// <summary>Minimum gross amount a driver may request per cash-out.</summary>
    public const decimal MinimumCashOutAmountPkr = 500m;

    /// <summary>Processing fee rate deducted from gross payout (2%).</summary>
    public const decimal CashOutProcessingFeeRate = 0.02m;

    public static CashOutFeeBreakdown CalculateFeeBreakdown(decimal grossAmountPkr)
    {
        var gross = Math.Round(grossAmountPkr, 2, MidpointRounding.AwayFromZero);
        var fee = Math.Round(gross * CashOutProcessingFeeRate, 2, MidpointRounding.AwayFromZero);
        var net = gross - fee;
        return new CashOutFeeBreakdown(gross, fee, net, CashOutProcessingFeeRate);
    }

    public static bool MeetsMinimumCashOut(decimal grossAmountPkr)
        => grossAmountPkr >= MinimumCashOutAmountPkr;
}

public record CashOutFeeBreakdown(
    decimal GrossAmount,
    decimal ProcessingFee,
    decimal NetPayout,
    decimal ProcessingFeeRate);
