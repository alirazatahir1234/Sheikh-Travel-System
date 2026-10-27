namespace SheikhTravelSystem.Application.Common;

public static class FinancePermissions
{
    public const string PaymentView = "Payment.View";
    public const string PaymentCreate = "Payment.Create";
    public const string PaymentUpdate = "Payment.Update";
    public const string InvoiceView = "Invoice.View";
    public const string InvoiceManage = "Invoice.Manage";
    public const string SettlementView = "Settlement.View";
    public const string SettlementManage = "Settlement.Manage";
    public const string FuelView = "Fuel.View";
    public const string FuelCreate = "Fuel.Create";
    public const string FuelUpdate = "Fuel.Update";
    public const string DriverWalletView = "DriverWallet.View";
    public const string DriverWalletManage = "DriverWallet.Manage";

    public static readonly string[] All =
    [
        PaymentView,
        PaymentCreate,
        PaymentUpdate,
        InvoiceView,
        InvoiceManage,
        SettlementView,
        SettlementManage,
        DriverWalletView,
        DriverWalletManage,
        FuelView,
        FuelCreate,
        FuelUpdate
    ];
}
