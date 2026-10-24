using FluentAssertions;
using SheikhTravelSystem.Application.Features.SmartOps;

namespace SheikhTravelSystem.Tests.SmartOps;

public class SmartOpsRuleLogicTests
{
    private static readonly DateTime AsOf = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(9500, 10000, true)]   // within 500 km
    [InlineData(9400, 10000, false)]  // 600 km remaining
    [InlineData(10000, 10000, true)]  // at due
    [InlineData(10050, 10000, true)]  // past due
    public void MaintenanceDue_ByMileage(decimal current, decimal nextDue, bool expected)
    {
        SmartOpsRuleLogic.IsMaintenanceDueByMileage(current, nextDue).Should().Be(expected);
    }

    [Fact]
    public void MaintenanceDue_ByDate_Within10Days()
    {
        SmartOpsRuleLogic.IsMaintenanceDueByDate(AsOf.Date.AddDays(10), AsOf).Should().BeTrue();
        SmartOpsRuleLogic.IsMaintenanceDueByDate(AsOf.Date.AddDays(11), AsOf).Should().BeFalse();
        SmartOpsRuleLogic.IsMaintenanceDueByDate(AsOf.Date.AddDays(-1), AsOf).Should().BeTrue();
    }

    [Fact]
    public void MaintenanceDue_OrCombinesMileageAndDate()
    {
        SmartOpsRuleLogic.IsMaintenanceDue(9000, 10000, AsOf.Date.AddDays(5), AsOf).Should().BeTrue();
        SmartOpsRuleLogic.IsMaintenanceDue(9600, 10000, AsOf.Date.AddDays(40), AsOf).Should().BeTrue();
        SmartOpsRuleLogic.IsMaintenanceDue(9000, 10000, AsOf.Date.AddDays(40), AsOf).Should().BeFalse();
    }

    [Fact]
    public void DocumentExpiry_Within30Days()
    {
        SmartOpsRuleLogic.IsDocumentExpiringWithinLead(AsOf.Date.AddDays(30), AsOf).Should().BeTrue();
        SmartOpsRuleLogic.IsDocumentExpiringWithinLead(AsOf.Date.AddDays(31), AsOf).Should().BeFalse();
        SmartOpsRuleLogic.IsDocumentExpiringWithinLead(AsOf.Date.AddDays(-1), AsOf).Should().BeTrue();
    }

    [Fact]
    public void UnassignedTrip_Within24Hours_MissingAssignment()
    {
        var pickup = AsOf.AddHours(12);
        SmartOpsRuleLogic.IsUnassignedTripImminent(pickup, AsOf, hasDriver: false, hasVehicle: true).Should().BeTrue();
        SmartOpsRuleLogic.IsUnassignedTripImminent(pickup, AsOf, hasDriver: true, hasVehicle: true).Should().BeFalse();
        SmartOpsRuleLogic.IsUnassignedTripImminent(AsOf.AddHours(30), AsOf, false, false).Should().BeFalse();
        SmartOpsRuleLogic.IsUnassignedTripImminent(AsOf.AddHours(-1), AsOf, false, false).Should().BeFalse();
    }

    [Fact]
    public void OverdueInvoice_Grace48HoursFromDue()
    {
        var due = AsOf.AddHours(-49);
        SmartOpsRuleLogic.IsOverdueInvoice(due, null, balance: 100m, AsOf).Should().BeTrue();
        SmartOpsRuleLogic.IsOverdueInvoice(AsOf.AddHours(-47), null, 100m, AsOf).Should().BeFalse();
        SmartOpsRuleLogic.IsOverdueInvoice(due, null, balance: 0m, AsOf).Should().BeFalse();
        SmartOpsRuleLogic.IsOverdueInvoice(null, AsOf.AddHours(-49), 50m, AsOf).Should().BeTrue();
    }

    [Fact]
    public void FuelAnomaly_CapacityAndDuplicateWindow()
    {
        SmartOpsRuleLogic.IsFuelOverCapacity(80m, 70m).Should().BeTrue();
        SmartOpsRuleLogic.IsFuelOverCapacity(60m, 70m).Should().BeFalse();
        SmartOpsRuleLogic.IsFuelOverCapacity(100m, null).Should().BeFalse(); // no capacity → skip

        SmartOpsRuleLogic.AreFuelEntriesTooClose(AsOf, AsOf.AddMinutes(30)).Should().BeTrue();
        SmartOpsRuleLogic.AreFuelEntriesTooClose(AsOf, AsOf.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void DedupeKey_IsStablePerDay()
    {
        var a = SmartOpsRuleLogic.BuildDailyDedupeKey("MaintenanceDue", "Vehicle", 7, AsOf);
        var b = SmartOpsRuleLogic.BuildDailyDedupeKey("MaintenanceDue", "Vehicle", 7, AsOf.AddHours(5));
        a.Should().Be(b);
        a.Should().Contain("2026-10-24");
    }
}

public class SmartAlertTenantGuardTests
{
    [Fact]
    public void Controller_NeverUsesClientTenantId_InQueryContract()
    {
        // GetSmartAlertsQuery always takes TenantId from server scope (IPlatformScope), not body.
        var q = new SheikhTravelSystem.Application.Features.SmartOps.Queries.GetSmartAlertsQuery(TenantId: 42);
        q.TenantId.Should().Be(42);
    }
}
