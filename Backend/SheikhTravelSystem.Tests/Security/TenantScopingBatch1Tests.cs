using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;
using SheikhTravelSystem.Application.Features.Dashboard.Queries;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Security;

public class TenantScopingBatch1Tests
{
    [Fact]
    public async Task Dashboard_summary_passes_tenantId_and_uses_tenant_cache_key()
    {
        var repo = new Mock<IDashboardRepository>();
        var tenant = new Mock<ITenantContext>();
        var cache = new RecordingCache();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(42);
        repo.Setup(r => r.GetSummaryAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardSummaryDto(1, 0, 0, 0, 0, 0));

        var handler = new GetDashboardSummaryQueryHandler(repo.Object, tenant.Object, cache);
        await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        cache.LastKey.Should().Be("dashboard:summary:42");
        repo.Verify(r => r.GetSummaryAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_passes_tenantId_into_repository()
    {
        var repo = new Mock<IPaymentRepository>();
        var tenant = new Mock<ITenantContext>();
        var decisions = new Mock<INotificationDecisionEngine>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(7);
        repo.Setup(r => r.GetBookingForPaymentAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentBookingInfo(1000m, (int)BookingStatus.Confirmed, "BK-1"));
        repo.Setup(r => r.GetTotalPaidForBookingAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        repo.Setup(r => r.CreateAsync(7, It.IsAny<CreatePaymentDto>(), It.IsAny<PaymentStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        decisions.Setup(d => d.DispatchIfAllowedAsync(It.IsAny<NotificationDecisionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreatePaymentCommandHandler(repo.Object, tenant.Object, decisions.Object);
        var result = await handler.Handle(
            new CreatePaymentCommand(new CreatePaymentDto(10, 100, "Cash", null, null, null)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.CreateAsync(
            7,
            It.Is<CreatePaymentDto>(d => d.BookingId == 10 && d.Amount == 100),
            PaymentStatus.PartiallyPaid,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Batch1_repository_sources_contain_TenantId_protections()
    {
        var root = FindRepoRoot();
        AssertContains(root, "DashboardRepository.cs", "AND TenantId = @TenantId");
        AssertContains(root, "BookingRepository.cs", "INSERT INTO Bookings (TenantId,");
        AssertContains(root, "BookingRepository.cs", "WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        AssertContains(root, "PaymentRepository.cs", "INSERT INTO Payments (TenantId,");
        AssertContains(root, "PaymentRepository.cs", "WHERE Id = @Id AND TenantId = @TenantId");
        AssertDoesNotContain(root, "DriverAppRepository.cs", "SyncLinkedBookingStatusAsync");
        AssertContains(root, "DriverTripLifecycleCommands.cs", "bookingRepository.UpdateStatusAsync");
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
        File.ReadAllText(path).Should().NotContain(snippet, because: $"{fileName} must not retain unsafe path");
    }

    private sealed class RecordingCache : IAppCache
    {
        public string? LastKey { get; private set; }

        public Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
        {
            LastKey = key;
            return factory(cancellationToken);
        }

        public void Remove(string key) { }
        public void RemoveByPrefix(string prefix) { }
    }
}
