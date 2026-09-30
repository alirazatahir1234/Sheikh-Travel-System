using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Application.Features.Maintenance.Commands;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;
using SheikhTravelSystem.Application.Features.Routes.Commands;
using SheikhTravelSystem.Application.Features.Routes.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Security;

public class TenantScopingBatch2Tests
{
    [Fact]
    public async Task CreateFuelLog_passes_tenantId_into_repository()
    {
        var repo = new Mock<IFuelLogRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(11);
        repo.Setup(r => r.CreateAsync(11, It.IsAny<CreateFuelLogDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);

        var handler = new CreateFuelLogCommandHandler(repo.Object, tenant.Object);
        var dto = new CreateFuelLogDto(1, null, 10m, 280m, 1000m, FuelType.Petrol, DateTime.UtcNow, null);
        var result = await handler.Handle(new CreateFuelLogCommand(dto), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.CreateAsync(11, It.IsAny<CreateFuelLogDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoute_passes_tenantId_into_repository()
    {
        var repo = new Mock<IRouteRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(22);
        repo.Setup(r => r.CreateAsync(22, It.IsAny<CreateRouteDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(88);

        var handler = new CreateRouteCommandHandler(repo.Object, tenant.Object);
        var dto = new CreateRouteDto("R1", "A", "B", 100m, 60, 5000m);
        var result = await handler.Handle(new CreateRouteCommand(dto), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.CreateAsync(22, It.IsAny<CreateRouteDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMaintenance_passes_tenantId_into_repository()
    {
        var repo = new Mock<IMaintenanceRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(33);
        repo.Setup(r => r.CreateAsync(33, It.IsAny<CreateMaintenanceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(77);

        var handler = new CreateMaintenanceCommandHandler(repo.Object, tenant.Object);
        var dto = new CreateMaintenanceDto(1, "Oil change", 5000m, DateTime.UtcNow, null, null);
        var result = await handler.Handle(new CreateMaintenanceCommand(dto), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.CreateAsync(33, It.IsAny<CreateMaintenanceDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculatePrice_passes_tenantId_into_repository()
    {
        var repo = new Mock<IPricingRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(44);
        repo.Setup(r => r.GetRoutePricingAsync(44, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutePricingInfo(100m, 5000m));
        repo.Setup(r => r.GetVehicleFuelAverageAsync(44, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);

        var handler = new CalculatePriceCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(
            new CalculatePriceCommand(new CalculatePriceRequest(1, 2, 300m, 0m, 0m, 0m)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.GetRoutePricingAsync(44, 1, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetVehicleFuelAverageAsync(44, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Batch2_repository_sources_contain_TenantId_protections()
    {
        var root = FindRepoRoot();
        AssertContains(root, "FuelLogRepository.cs", "INSERT INTO FuelLogs (TenantId,");
        AssertContains(root, "FuelLogRepository.cs", "WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "MaintenanceRepository.cs", "INSERT INTO Maintenance (TenantId,");
        AssertContains(root, "MaintenanceRepository.cs", "WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "RouteRepository.cs", "INSERT INTO Routes (TenantId,");
        AssertContains(root, "RouteRepository.cs", "WHERE IsDeleted = 0 AND TenantId = @TenantId");
        AssertContains(root, "DriverAllowanceRepository.cs", "(TenantId, Name, CalculationType, Value, Priority");
        AssertContains(root, "DriverAllowanceRepository.cs", "WHERE IsDeleted = 0 AND TenantId = @TenantId");
        AssertContains(root, "DriverAllowanceRepository.cs", "FROM Routes WHERE Id = @Id AND TenantId = @TenantId");
        AssertContains(root, "DriverAllowanceRepository.cs", "FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId");
        AssertContains(root, "PricingRepository.cs", "FROM Routes WHERE Id = @Id AND TenantId = @TenantId");
        AssertContains(root, "PricingRepository.cs", "FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId");
    }

    [Fact]
    public void Batch2_create_dtos_do_not_accept_client_TenantId()
    {
        var root = FindRepoRoot();
        AssertDoesNotContain(root, "FuelLogDtos.cs", "TenantId");
        AssertDoesNotContain(root, "MaintenanceDtos.cs", "TenantId");
        AssertDoesNotContain(root, "RouteDtos.cs", "TenantId");
        AssertDoesNotContain(root, "PricingDtos.cs", "TenantId");
        AssertDoesNotContain(root, "DriverAllowanceRuleDtos.cs", "TenantId");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(backend))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend root from test base directory.");
    }

    private static void AssertContains(string backendRoot, string fileName, string snippet)
    {
        var path = Directory.EnumerateFiles(backendRoot, fileName, SearchOption.AllDirectories).First();
        File.ReadAllText(path).Should().Contain(snippet, because: $"{fileName} must include tenant protection");
    }

    private static void AssertDoesNotContain(string backendRoot, string fileName, string snippet)
    {
        var path = Directory.EnumerateFiles(backendRoot, fileName, SearchOption.AllDirectories).First();
        File.ReadAllText(path).Should().NotContain(snippet, because: $"{fileName} must not expose client TenantId");
    }
}
