namespace SheikhTravelSystem.Application.Features.Reports.DTOs;

public record BookingReportDto(int TotalBookings, int Completed, int Cancelled, int Pending, int Active);

public record RevenueReportDto(decimal TotalRevenue, decimal FuelExpense, decimal MaintenanceCost, decimal NetProfit);

public record VehicleProfitDto(int VehicleId, string VehicleName, decimal Revenue, decimal FuelCost, decimal MaintenanceCost, decimal Profit);

public record DriverPerformanceDto(int DriverId, string DriverName, int TotalTrips, int CompletedTrips, decimal TotalRevenue);
