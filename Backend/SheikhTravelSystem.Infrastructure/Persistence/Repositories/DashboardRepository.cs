using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(IDbConnectionFactory dbFactory) : IDashboardRepository
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        async Task<T> Scalar<T>(string sql, object? param = null)
        {
            using var conn = dbFactory.CreateConnection();
            return await conn.ExecuteScalarAsync<T>(
                new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        }

        var totalVehiclesTask = Scalar<int>("SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0");
        var activeTripsTask = Scalar<int>(
            "SELECT COUNT(*) FROM Bookings WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)BookingStatus.Started });
        var totalRevenueTask = Scalar<decimal>(
            "SELECT ISNULL(SUM(Amount), 0) FROM Payments WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)PaymentStatus.Paid });
        var pendingBookingsTask = Scalar<int>(
            "SELECT COUNT(*) FROM Bookings WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)BookingStatus.Pending });
        var fuelExpenseTask = Scalar<decimal>("SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs WHERE IsDeleted = 0");
        var maintenanceExpenseTask = Scalar<decimal>("SELECT ISNULL(SUM(Cost), 0) FROM Maintenance WHERE IsDeleted = 0");

        await Task.WhenAll(
            totalVehiclesTask, activeTripsTask, totalRevenueTask,
            pendingBookingsTask, fuelExpenseTask, maintenanceExpenseTask);

        var totalRevenue = totalRevenueTask.Result;
        var fuelExpense = fuelExpenseTask.Result;
        var maintenanceExpense = maintenanceExpenseTask.Result;
        var netProfit = totalRevenue - fuelExpense - maintenanceExpense;

        return new DashboardSummaryDto(
            totalVehiclesTask.Result,
            activeTripsTask.Result,
            totalRevenue,
            pendingBookingsTask.Result,
            fuelExpense,
            netProfit);
    }
}
