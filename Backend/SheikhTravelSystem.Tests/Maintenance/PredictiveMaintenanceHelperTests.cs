using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class PredictiveMaintenanceHelperTests
{
    private static readonly DateTime Today = new(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);

    private static PredictiveVehicleInput Base(
        decimal mileage = 10_000m,
        int? year = 2020,
        DateTime? purchase = null,
        decimal? lastOilMiles = 5_000m,
        DateTime? lastOilDate = null,
        decimal? tireInstall = 0m,
        DateTime? tireDate = null,
        decimal? avgDaily = 100m) =>
        new(
            VehicleId: 1,
            VehicleName: "Bus-01",
            VehicleRegistration: "ABC-123",
            CurrentMileage: mileage,
            Year: year,
            PurchaseDate: purchase ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OilIntervalKm: null,
            OilIntervalDays: null,
            LastOilChangeDate: lastOilDate ?? Today.AddDays(-100),
            LastOilChangeMileage: lastOilMiles,
            TireInstallDate: tireDate ?? Today.AddDays(-200),
            TireInstallMileage: tireInstall,
            TireExpectedLifeKm: null,
            EngineExpectedLifeKm: null,
            EngineExpectedLifeYears: null,
            AvgDailyKm: avgDaily);

    [Fact]
    public void Oil_Overdue_WhenPastIntervalKm()
    {
        var alert = PredictiveMaintenanceHelper.ComputeOil(
            Base(mileage: 10_200m, lastOilMiles: 5_000m), Today);
        Assert.NotNull(alert);
        Assert.Equal(PredictiveMaintenanceHelper.CategoryOil, alert!.Category);
        Assert.Equal("Overdue", alert.Status);
        Assert.Equal("Critical", alert.Severity);
    }

    [Fact]
    public void Oil_DueSoon_Within500Km()
    {
        var alert = PredictiveMaintenanceHelper.ComputeOil(
            Base(mileage: 9_700m, lastOilMiles: 5_000m, lastOilDate: Today.AddDays(-30)), Today);
        Assert.NotNull(alert);
        Assert.Equal("DueSoon", alert!.Status);
        Assert.Equal("Warning", alert.Severity);
        Assert.NotNull(alert.PredictedDueDate);
    }

    [Fact]
    public void Tire_PredictsReplacementDate()
    {
        // 38.5k worn of 40k life → 1.5k left (≤ TireDueSoonKm 2k)
        var alert = PredictiveMaintenanceHelper.ComputeTire(
            Base(mileage: 38_500m, tireInstall: 0m, avgDaily: 100m), Today);
        Assert.NotNull(alert);
        Assert.Equal(PredictiveMaintenanceHelper.CategoryTire, alert!.Category);
        Assert.Equal("DueSoon", alert.Status);
        Assert.NotNull(alert.PredictedDueDate);
        Assert.True(alert.RemainingKm is > 0 and <= 2_000m);
    }

    [Fact]
    public void Engine_WarnsAt85PercentMileage()
    {
        // Default life 300k → 85% = 255k
        var alert = PredictiveMaintenanceHelper.ComputeEngineLife(
            Base(mileage: 260_000m, year: 2022, purchase: Today.AddYears(-3)), Today);
        Assert.NotNull(alert);
        Assert.Equal(PredictiveMaintenanceHelper.CategoryEngine, alert!.Category);
        Assert.Equal("Warning", alert.Severity);
        Assert.True(alert.ProgressPercent >= 85);
    }

    [Fact]
    public void Engine_SilentBelowWarnThreshold()
    {
        var alert = PredictiveMaintenanceHelper.ComputeEngineLife(
            Base(mileage: 50_000m, year: 2023, purchase: Today.AddYears(-2)), Today);
        Assert.Null(alert);
    }

    [Fact]
    public void ComputeAll_OrdersCriticalFirst()
    {
        var overdueOil = Base(mileage: 12_000m, lastOilMiles: 5_000m);
        var young = Base(mileage: 1_000m, lastOilMiles: 0m, lastOilDate: Today.AddDays(-10),
            tireInstall: 0m, tireDate: Today.AddDays(-10));
        // young tire/oil may be filtered as far upcoming; still ensure oil overdue ranks top
        var all = PredictiveMaintenanceHelper.ComputeAll([overdueOil, young with { VehicleId = 2, VehicleName = "Van" }], Today);
        Assert.Contains(all, a => a.Category == PredictiveMaintenanceHelper.CategoryOil && a.Severity == "Critical");
        Assert.Equal("Critical", all[0].Severity);
    }

    [Fact]
    public void Defaults_ApplyWhenHistoryMissing()
    {
        var v = new PredictiveVehicleInput(
            9, "Sparse", null, 4_800m, null, null,
            null, null, null, null,
            null, null, null, null, null);
        var oil = PredictiveMaintenanceHelper.ComputeOil(v, Today);
        Assert.NotNull(oil);
        // With no history, last oil assumed current-interval → due soon near default 5k
        Assert.True(oil!.Status is "DueSoon" or "Upcoming" or "Overdue");
    }
}
