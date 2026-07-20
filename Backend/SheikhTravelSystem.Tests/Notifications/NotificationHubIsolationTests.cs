using FluentAssertions;
using SheikhTravelSystem.Infrastructure.SignalR;

namespace SheikhTravelSystem.Tests.Notifications;

public class NotificationHubIsolationTests
{
    [Fact]
    public void UserGroup_Includes_Tenant_And_User()
    {
        var group = NotificationHub.UserGroup(7, 42);

        group.Should().Be("tenant_7:user_42");
    }

    [Fact]
    public void UserGroup_Differs_For_Same_User_In_Different_Tenants()
    {
        var tenant1 = NotificationHub.UserGroup(1, 99);
        var tenant2 = NotificationHub.UserGroup(2, 99);

        tenant1.Should().NotBe(tenant2);
    }
}
