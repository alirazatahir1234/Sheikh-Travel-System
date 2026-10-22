using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Application.Features.Drivers.Queries;

namespace SheikhTravelSystem.Tests.Drivers;

public class GetDriversQueryTenantFilterTests
{
    [Fact]
    public async Task Handle_WithTenantId_UsesRequestedTenantAfterEnsureAccess()
    {
        var repo = new Mock<IDriverRepository>();
        var scope = new Mock<IPlatformScope>();
        var currentUser = new Mock<ICurrentUserService>();
        var dataScope = new Mock<IDataScopeEngine>();

        scope.Setup(s => s.TenantId).Returns(1);
        scope.Setup(s => s.EnsureTenantAccess(3));
        currentUser.SetupGet(c => c.UserId).Returns((int?)null);
        repo.Setup(r => r.GetPagedAsync(
                3, 1, 20, null, null, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverListQueryResult(Array.Empty<DriverListItemDto>(), 0, null));

        var handler = new GetDriversQueryHandler(
            repo.Object, scope.Object, currentUser.Object, dataScope.Object);

        var result = await handler.Handle(new GetDriversQuery(TenantId: 3), CancellationToken.None);

        result.Success.Should().BeTrue();
        scope.Verify(s => s.EnsureTenantAccess(3), Times.Once);
        repo.Verify(r => r.GetPagedAsync(
            3, 1, 20, null, null, null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
