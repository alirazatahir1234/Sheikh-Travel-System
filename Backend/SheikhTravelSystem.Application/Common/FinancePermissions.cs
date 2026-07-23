namespace SheikhTravelSystem.Application.Common;

public static class FinancePermissions
{
    public const string PaymentView = "Payment.View";
    public const string PaymentCreate = "Payment.Create";
    public const string PaymentUpdate = "Payment.Update";
    public const string InvoiceView = "Invoice.View";
    public const string FuelView = "Fuel.View";
    public const string FuelCreate = "Fuel.Create";
    public const string FuelUpdate = "Fuel.Update";

    public static readonly string[] All =
    [
        PaymentView,
        PaymentCreate,
        PaymentUpdate,
        InvoiceView,
        FuelView,
        FuelCreate,
        FuelUpdate
    ];
}
