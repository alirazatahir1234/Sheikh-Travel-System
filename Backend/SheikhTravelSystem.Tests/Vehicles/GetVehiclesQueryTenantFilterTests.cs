using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles.Queries;

namespace SheikhTravelSystem.Tests.Vehicles;

public class GetVehiclesQueryTenantFilterTests
{
    [Fact]
    public async Task Handle_WithTenantId_UsesRequestedTenantAfterEnsureAccess()
    {
        var repo = new Mock<IVehicleRepository>();
        var scope = new Mock<IPlatformScope>();
        var currentUser = new Mock<ICurrentUserService>();
        var dataScope = new Mock<IDataScopeEngine>();
        var files = new Mock<IFileStorageService>();

        scope.Setup(s => s.TenantId).Returns(1);
        scope.Setup(s => s.EnsureTenantAccess(3));
        currentUser.SetupGet(c => c.UserId).Returns((int?)null);
        repo.Setup(r => r.GetPagedAsync(3, 1, 20, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<VehicleListItemDto>(), 0));

        var handler = new GetVehiclesQueryHandler(
            repo.Object, scope.Object, currentUser.Object, dataScope.Object, files.Object);

        var result = await handler.Handle(new GetVehiclesQuery(TenantId: 3), CancellationToken.None);

        result.Success.Should().BeTrue();
        scope.Verify(s => s.EnsureTenantAccess(3), Times.Once);
        repo.Verify(r => r.GetPagedAsync(3, 1, 20, false, null, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DataScopeResult?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutTenantId_UsesPlatformScopeTenant()
    {
        var repo = new Mock<IVehicleRepository>();
        var scope = new Mock<IPlatformScope>();
        var currentUser = new Mock<ICurrentUserService>();
        var dataScope = new Mock<IDataScopeEngine>();
        var files = new Mock<IFileStorageService>();

        scope.Setup(s => s.TenantId).Returns(7);
        currentUser.SetupGet(c => c.UserId).Returns((int?)null);
        repo.Setup(r => r.GetPagedAsync(7, 1, 20, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<VehicleListItemDto>(), 0));

        var handler = new GetVehiclesQueryHandler(
            repo.Object, scope.Object, currentUser.Object, dataScope.Object, files.Object);

        var result = await handler.Handle(new GetVehiclesQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        scope.Verify(s => s.EnsureTenantAccess(It.IsAny<int>()), Times.Never);
        repo.Verify(r => r.GetPagedAsync(7, 1, 20, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
