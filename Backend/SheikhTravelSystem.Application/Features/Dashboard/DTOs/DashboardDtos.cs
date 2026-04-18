namespace SheikhTravelSystem.Application.Features.Dashboard.DTOs;

public record DashboardSummaryDto(
    int TotalVehicles,
    int ActiveTrips,
    decimal TotalRevenue,
    int PendingBookings,
    decimal FuelExpense,
    decimal NetProfit);
