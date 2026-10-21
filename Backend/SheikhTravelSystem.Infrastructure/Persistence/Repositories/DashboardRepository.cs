using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(IDbConnectionFactory dbFactory) : IDashboardRepository
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var conn = dbFactory.CreateConnection();
        var row = await conn.QuerySingleAsync<SummaryRow>(
            new CommandDefinition(
                """
                SELECT
                  (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId) AS TotalVehicles,
                  (SELECT COUNT(*) FROM Bookings WHERE Status = @Started AND IsDeleted = 0 AND TenantId = @TenantId) AS ActiveTrips,
                  (SELECT ISNULL(SUM(Amount), 0) FROM Payments WHERE Status = @Paid AND IsDeleted = 0 AND TenantId = @TenantId) AS TotalRevenue,
                  (SELECT COUNT(*) FROM Bookings WHERE Status = @Pending AND IsDeleted = 0 AND TenantId = @TenantId) AS PendingBookings,
                  (SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs WHERE IsDeleted = 0 AND TenantId = @TenantId) AS FuelExpense,
                  (SELECT ISNULL(SUM(Cost), 0) FROM Maintenance WHERE IsDeleted = 0 AND TenantId = @TenantId) AS MaintenanceExpense
                """,
                new
                {
                    TenantId = tenantId,
                    Started = (int)BookingStatus.Started,
                    Paid = (int)PaymentStatus.Paid,
                    Pending = (int)BookingStatus.Pending
                },
                cancellationToken: cancellationToken));

        var netProfit = row.TotalRevenue - row.FuelExpense - row.MaintenanceExpense;
        return new DashboardSummaryDto(
            row.TotalVehicles,
            row.ActiveTrips,
            row.TotalRevenue,
            row.PendingBookings,
            row.FuelExpense,
            netProfit);
    }

    private sealed class SummaryRow
    {
        public int TotalVehicles { get; init; }
        public int ActiveTrips { get; init; }
        public decimal TotalRevenue { get; init; }
        public int PendingBookings { get; init; }
        public decimal FuelExpense { get; init; }
        public decimal MaintenanceExpense { get; init; }
    }
}
