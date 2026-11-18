using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

namespace SheikhTravelSystem.Tests.CustomerPortal;

public class PortalTenantScopeTests
{
    [Fact]
    public async Task GetPortalBookingsByPhone_resolves_customers_for_current_tenant_only()
    {
        var repo = new Mock<ICustomerPortalRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(3);
        repo.Setup(r => r.ResolvePortalCustomerIdsAsync(3, "03001234567", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 10 });
        repo.Setup(r => r.GetBookingCardsAsync(3, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetPortalBookingsByPhoneQueryHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(
            new GetPortalBookingsByPhoneQuery("03001234567"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.ResolvePortalCustomerIdsAsync(3, "03001234567", null, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.ResolvePortalCustomerIdsAsync(
            It.Is<int>(id => id != 3), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.GetBookingCardsAsync(3, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPortalBookingDetail_rejects_when_phone_not_in_tenant()
    {
        var repo = new Mock<ICustomerPortalRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(9);
        repo.Setup(r => r.ResolvePortalCustomerIdsAsync(9, "03009999999", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        var handler = new GetPortalBookingDetailQueryHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(
            new GetPortalBookingDetailQuery(42, "03009999999", 5),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.GetBookingDetailHeadAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPortalRoutes_passes_current_tenant_to_repository()
    {
        var repo = new Mock<ICustomerPortalRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(7);
        repo.Setup(r => r.GetRoutesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PortalRouteDto>());

        var handler = new GetPortalRoutesQueryHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new GetPortalRoutesQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.GetRoutesAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetRoutesAsync(It.Is<int>(id => id != 7), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPortalVehicles_passes_current_tenant_to_repository()
    {
        var repo = new Mock<ICustomerPortalRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(11);
        repo.Setup(r => r.GetVehiclesAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PortalVehicleDto>());

        var handler = new GetPortalVehiclesQueryHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new GetPortalVehiclesQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.GetVehiclesAsync(11, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CustomerPortalRepository_phone_resolve_filters_TenantId()
    {
        var root = FindBackendRoot();
        var path = Directory.EnumerateFiles(root, "CustomerPortalRepository.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(path);
        src.Should().Contain("AND TenantId = @TenantId AND (");
        src.Should().Contain("b.TenantId = @TenantId AND b.CustomerId IN @CustomerIds");
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(backend))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend root.");
    }
}
