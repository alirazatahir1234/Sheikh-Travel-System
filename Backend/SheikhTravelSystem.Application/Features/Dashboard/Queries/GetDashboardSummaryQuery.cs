using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Dashboard.Queries;

public record GetDashboardSummaryQuery : IRequest<ApiResponse<DashboardSummaryDto>>;

public class GetDashboardSummaryQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDashboardSummaryQuery, ApiResponse<DashboardSummaryDto>>
{
    public async Task<ApiResponse<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var totalVehicles = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0");

        var activeTrips = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Bookings WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)BookingStatus.Started });

        var totalRevenue = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(Amount), 0) FROM Payments WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)PaymentStatus.Paid });

        var pendingBookings = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Bookings WHERE Status = @Status AND IsDeleted = 0",
            new { Status = (int)BookingStatus.Pending });

        var fuelExpense = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs WHERE IsDeleted = 0");

        var maintenanceExpense = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(Cost), 0) FROM Maintenance WHERE IsDeleted = 0");

        var netProfit = totalRevenue - fuelExpense - maintenanceExpense;

        var summary = new DashboardSummaryDto(totalVehicles, activeTrips, totalRevenue, pendingBookings, fuelExpense, netProfit);
        return ApiResponse<DashboardSummaryDto>.SuccessResponse(summary);
    }
}
