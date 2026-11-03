using SheikhTravelSystem.Application.Features.Assignments;

namespace SheikhTravelSystem.Tests.Assignments;

public class AssignmentValidationTests
{
    [Theory]
    [InlineData("Permanent", "Scheduled")]
    [InlineData("Temporary", "PendingApproval")]
    [InlineData("Emergency", "PendingApproval")]
    public void ResolveInitialStatus_FutureStart_UsesTypeAndScheduleRules(string type, string expected)
    {
        var future = DateTime.UtcNow.AddDays(2);
        Assert.Equal(expected, AssignmentValidation.ResolveInitialStatus(future, type));
    }

    [Theory]
    [InlineData("Permanent", "Active")]
    [InlineData("Temporary", "PendingApproval")]
    [InlineData("Emergency", "PendingApproval")]
    public void ResolveInitialStatus_PastStart_UsesTypeRules(string type, string expected)
    {
        var past = DateTime.UtcNow.AddHours(-1);
        Assert.Equal(expected, AssignmentValidation.ResolveInitialStatus(past, type));
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Scheduled", true)]
    [InlineData("PendingApproval", true)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    public void IsOpenStatus_MatchesExpected(string status, bool open)
    {
        Assert.Equal(open, AssignmentValidation.IsOpenStatus(status));
    }
}
