using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class MaintenanceScheduleHelperTests
{
    private static readonly DateTime Today = new(2025, 6, 21, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(14_800, 15_000, MaintenanceScheduleHelper.StatusDueSoon)]
    [InlineData(15_100, 15_000, MaintenanceScheduleHelper.StatusOverdue)]
    [InlineData(10_000, 15_000, MaintenanceScheduleHelper.StatusUpcoming)]
    public void MileageStatus_UsesThresholds(decimal current, decimal nextDue, string expected)
    {
        Assert.Equal(expected, MaintenanceScheduleHelper.MileageStatus(current, nextDue));
    }

    [Theory]
    [InlineData(10, MaintenanceScheduleHelper.StatusDueSoon)]
    [InlineData(-1, MaintenanceScheduleHelper.StatusOverdue)]
    [InlineData(30, MaintenanceScheduleHelper.StatusUpcoming)]
    public void DateStatus_UsesThresholds(int daysFromToday, string expected)
    {
        var due = Today.AddDays(daysFromToday);
        Assert.Equal(expected, MaintenanceScheduleHelper.DateStatus(due, Today));
    }

    [Theory]
    [InlineData(1_450, 1_500, MaintenanceScheduleHelper.StatusDueSoon)]
    [InlineData(1_510, 1_500, MaintenanceScheduleHelper.StatusOverdue)]
    [InlineData(1_000, 1_500, MaintenanceScheduleHelper.StatusUpcoming)]
    public void EngineHoursStatus_UsesThresholds(decimal current, decimal nextDue, string expected)
    {
        Assert.Equal(expected, MaintenanceScheduleHelper.EngineHoursStatus(current, nextDue));
    }

    [Fact]
    public void RecomputeNextDue_Mileage_SetsNextMileage()
    {
        var (mileage, hours, date) = MaintenanceScheduleHelper.RecomputeNextDue(
            "Mileage", 5000, null, 10_000m, null);

        Assert.Equal(15_000m, mileage);
        Assert.Null(hours);
        Assert.Null(date);
    }

    [Fact]
    public void RecomputeNextDue_EngineHours_SetsNextHours()
    {
        var (mileage, hours, date) = MaintenanceScheduleHelper.RecomputeNextDue(
            "EngineHours", 250, null, null, 1_000m);

        Assert.Null(mileage);
        Assert.Equal(1_250m, hours);
        Assert.Null(date);
    }

    [Fact]
    public void RecomputeNextDue_Months_SetsNextDate()
    {
        var last = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var (mileage, hours, date) = MaintenanceScheduleHelper.RecomputeNextDue(
            "Months", 3, last, null, null);

        Assert.Null(mileage);
        Assert.Null(hours);
        Assert.Equal(new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc), date);
    }

    [Fact]
    public void ComputeStatus_WorstOfTriggers_Wins()
    {
        var status = MaintenanceScheduleHelper.ComputeStatus(
            "Mileage",
            nextDueDate: Today.AddDays(30),
            nextDueMileage: 15_000m,
            nextDueEngineHours: null,
            currentMileage: 15_200m,
            currentEngineHours: null,
            Today);

        Assert.Equal(MaintenanceScheduleHelper.StatusOverdue, status);
    }
}
