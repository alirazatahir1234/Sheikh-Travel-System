namespace SheikhTravelSystem.Application.Common;

public static class FinancePermissions
{
    public const string PaymentView = "Payment.View";
    public const string InvoiceView = "Invoice.View";
    public const string FuelView = "Fuel.View";

    public static readonly string[] All =
    [
        PaymentView,
        InvoiceView,
        FuelView
    ];
}
