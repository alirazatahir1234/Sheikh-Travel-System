using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;
using SheikhTravelSystem.Application.Features.Tracking.Queries;

namespace SheikhTravelSystem.Tests.Security;

public class TenantScopingBatch3Tests
{
    [Fact]
    public async Task UpdateLocation_passes_tenantId_and_fails_when_vehicle_not_owned()
    {
        var repo = new Mock<ITrackingRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(9);
        repo.Setup(r => r.InsertLocationAsync(9, 1, null, null, 1.0, 2.0, 0m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new UpdateLocationCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(
            new UpdateLocationCommand(new UpdateLocationDto(1, null, null, 1.0, 2.0, 0m)),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.InsertLocationAsync(9, 1, null, null, 1.0, 2.0, 0m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLiveTracking_passes_tenantId_into_repository()
    {
        var repo = new Mock<ITrackingRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(5);
        repo.Setup(r => r.GetLiveAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TrackingDto>());

        var handler = new GetLiveTrackingQueryHandler(repo.Object, tenant.Object);
        await handler.Handle(new GetLiveTrackingQuery(), CancellationToken.None);

        repo.Verify(r => r.GetLiveAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrackingHistory_passes_tenantId_into_repository()
    {
        var repo = new Mock<ITrackingRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(8);
        repo.Setup(r => r.GetHistoryAsync(8, 3, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TrackingDto>());

        var handler = new GetTrackingHistoryQueryHandler(repo.Object, tenant.Object);
        await handler.Handle(new GetTrackingHistoryQuery(3, null, null), CancellationToken.None);

        repo.Verify(r => r.GetHistoryAsync(8, 3, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Batch3_repository_sources_contain_TenantId_protections()
    {
        var root = FindRepoRoot();
        AssertContains(root, "TrackingRepository.cs", "INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.TenantId = @TenantId");
        AssertContains(root, "TrackingRepository.cs", "WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "CustomerRepository.cs", "INSERT INTO Customers (TenantId,");
        AssertContains(root, "CustomerRepository.cs", "WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "CustomerRepository.cs", "WHERE CNIC = @CNIC AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "GpsDeviceRepository.cs", "INSERT INTO GpsDevices (TenantId,");
        AssertContains(root, "GpsDeviceRepository.cs", "TrackerTenantSql.DeviceScopeFilter");
        AssertContains(root, "GpsTrackingRepository.cs", "INSERT INTO Geofences (TenantId,");
        AssertContains(root, "GpsTrackingRepository.cs", "INSERT INTO GpsAlertRules (TenantId,");
        AssertContains(root, "GpsTrackingRepository.cs", "WHERE g.IsDeleted = 0 AND g.TenantId = @TenantId");
        AssertContains(root, "GpsTrackingRepository.cs", "INNER JOIN Vehicles v ON v.Id = p.VehicleId AND v.TenantId = @TenantId");
        AssertContains(root, "GpsPositionIngestionHelper.cs", "(TenantId, VehicleId, GpsDeviceId");
        AssertContains(root, "AuditLogRepository.cs", "a.TenantId = @TenantId");
        AssertContains(root, "PublicLeadRepository.cs", "(1, @FirstName");
        AssertContains(root, "PublicLeadRepository.cs", "(1, @Name");
    }

    [Fact]
    public void Batch3_create_dtos_do_not_accept_client_TenantId()
    {
        var root = FindRepoRoot();
        AssertDoesNotContain(root, "CustomerDtos.cs", "TenantId");
        var trackingDto = Directory.EnumerateFiles(root, "*Tracking*Dto*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "TrackingDtos.cs", SearchOption.AllDirectories))
            .Distinct()
            .ToList();
        trackingDto.Should().NotBeEmpty();
        foreach (var path in trackingDto)
            File.ReadAllText(path).Should().NotContain("TenantId");
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
