using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class PlatformGpsAlertQueriesTests
{
    [Fact]
    public async Task GetPlatformEvents_NonSuperAdmin_IsRejected()
    {
        var repo = new Mock<IGpsTrackingRepository>();
        var scope = new Mock<IPlatformScope>();
        scope.SetupGet(s => s.IsSuperAdmin).Returns(false);

        var handler = new GetPlatformGpsAlertEventsQueryHandler(repo.Object, scope.Object);
        var result = await handler.Handle(
            new GetPlatformGpsAlertEventsQuery(null, null, null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Platform operator");
        repo.Verify(
            r => r.GetPlatformGpsAlertEventsAsync(It.IsAny<GetPlatformGpsAlertEventsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPlatformEvents_SuperAdmin_CallsRepository()
    {
        var repo = new Mock<IGpsTrackingRepository>();
        var scope = new Mock<IPlatformScope>();
        scope.SetupGet(s => s.IsSuperAdmin).Returns(true);
        repo.Setup(r => r.GetPlatformGpsAlertEventsAsync(It.IsAny<GetPlatformGpsAlertEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<GpsAlertEventDto>>.SuccessResponse([]));

        var handler = new GetPlatformGpsAlertEventsQueryHandler(repo.Object, scope.Object);
        var result = await handler.Handle(
            new GetPlatformGpsAlertEventsQuery(TenantId: 42, null, null, Status: "active"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(
            r => r.GetPlatformGpsAlertEventsAsync(
                It.Is<GetPlatformGpsAlertEventsQuery>(q => q.TenantId == 42 && q.Status == "active"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPlatformStats_NonSuperAdmin_IsRejected()
    {
        var repo = new Mock<IGpsTrackingRepository>();
        var scope = new Mock<IPlatformScope>();
        scope.SetupGet(s => s.IsSuperAdmin).Returns(false);

        var handler = new GetPlatformGpsAlertStatsQueryHandler(repo.Object, scope.Object);
        var result = await handler.Handle(new GetPlatformGpsAlertStatsQuery(), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(
            r => r.GetPlatformGpsAlertStatsAsync(It.IsAny<GetPlatformGpsAlertStatsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
