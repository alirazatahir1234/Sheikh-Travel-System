using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class MaintenanceValidationTests
{
    [Theory]
    [InlineData("Draft", "Open", true)]
    [InlineData("Draft", "Cancelled", true)]
    [InlineData("Draft", "Completed", false)]
    [InlineData("Open", "InProgress", true)]
    [InlineData("InProgress", "WaitingParts", true)]
    [InlineData("InProgress", "Completed", true)]
    [InlineData("Completed", "Open", false)]
    [InlineData("Completed", "Closed", true)]
    [InlineData("Closed", "Open", false)]
    [InlineData("Cancelled", "Open", false)]
    public void CanTransition_MatchesRules(string current, string next, bool expected)
    {
        Assert.Equal(expected, MaintenanceValidation.CanTransition(current, next));
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("InProgress", true)]
    [InlineData("WaitingParts", true)]
    [InlineData("Completed", false)]
    [InlineData("Closed", false)]
    [InlineData("Cancelled", false)]
    public void IsOpenWorkOrderStatus_MatchesExpected(string status, bool open)
    {
        Assert.Equal(open, MaintenanceValidation.IsOpenWorkOrderStatus(status));
    }

    [Theory]
    [InlineData("InProgress", true)]
    [InlineData("WaitingParts", true)]
    [InlineData("Open", false)]
    public void ShouldSetVehicleMaintenance_MatchesExpected(string status, bool expected)
    {
        Assert.Equal(expected, MaintenanceValidation.ShouldSetVehicleMaintenance(status));
    }

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Closed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("InProgress", false)]
    public void IsTerminalWorkOrderStatus_MatchesExpected(string status, bool terminal)
    {
        Assert.Equal(terminal, MaintenanceValidation.IsTerminalWorkOrderStatus(status));
    }

    [Fact]
    public void ComputeNextDueDate_Months_AddsMonths()
    {
        var last = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var next = MaintenanceValidation.ComputeNextDueDate("Months", 3, last);
        Assert.Equal(new DateTime(2024, 4, 15, 0, 0, 0, DateTimeKind.Utc), next);
    }

    [Theory]
    [InlineData("Open", true)]
    [InlineData("Approved", false)]
    [InlineData("Rejected", false)]
    public void CanApprove_MatchesExpected(string status, bool expected)
    {
        Assert.Equal(expected, MaintenanceRequestValidation.CanApprove(status));
    }

    [Theory]
    [InlineData("Open", true)]
    [InlineData("Approved", true)]
    [InlineData("Rejected", false)]
    public void CanConvert_MatchesExpected(string status, bool expected)
    {
        Assert.Equal(expected, MaintenanceRequestValidation.CanConvert(status));
    }
}
