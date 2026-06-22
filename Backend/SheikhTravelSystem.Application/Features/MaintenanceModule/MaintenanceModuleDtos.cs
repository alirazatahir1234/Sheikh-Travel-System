namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record MaintenanceDashboardDto(
    MaintenanceKpiDto Kpis,
    IReadOnlyList<MaintenanceCostTrendPointDto> CostTrend,
    VehicleHealthDto VehicleHealth,
    IReadOnlyList<MaintenanceAlertDto> CriticalAlerts,
    IReadOnlyList<WorkOrderListItemDto> RecentWorkOrders,
    IReadOnlyList<UpcomingServiceDto> UpcomingServices,
    FuelMaintenanceSummaryDto? FuelSummary);

public record MaintenanceKpiDto(
    int TotalVehicles,
    int DueForService,
    int UnderMaintenance,
    int OverdueServices,
    decimal MonthlyMaintenanceCost,
    int ActiveWorkOrders,
    int PendingRequests);

public record MaintenanceCostTrendPointDto(
    string Label,
    decimal PreventiveCost,
    decimal CorrectiveCost,
    decimal BreakdownCost);

public record VehicleHealthDto(
    int Healthy,
    int ServiceDueSoon,
    int Overdue,
    int InWorkshop);

public record MaintenanceAlertDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    string AlertType,
    string Severity,
    string Title,
    string Message,
    DateTime CreatedAt);

public record UpcomingServiceDto(
    int? ScheduleId,
    int VehicleId,
    string VehicleName,
    string? VehicleRegistration,
    string ServiceType,
    DateTime? DueDate,
    decimal? DueMileage,
    string Priority);

public record MaintenanceRequestDto(
    int Id,
    string RequestNumber,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    int? DriverId,
    string? DriverName,
    DateTime RequestDate,
    string RequestType,
    string Priority,
    string IssueCategory,
    string Description,
    string? BreakdownLocation,
    string? DriverRemarks,
    string Status,
    int? WorkOrderId,
    DateTime CreatedAt,
    string? PhotosJson = null,
    string? DocumentsJson = null,
    string? BranchName = null,
    string? DepartmentName = null,
    string? RejectionReason = null,
    string? ApprovedBy = null,
    DateTime? ApprovedAt = null,
    string? RejectedBy = null,
    DateTime? RejectedAt = null);

public record MaintenanceRequestStatsDto(
    int Open,
    int Approved,
    int InProgress,
    int PendingApproval);

public record RejectMaintenanceRequestDto(string Reason);

public record MaintenanceSearchResultDto(
    string EntityType,
    int Id,
    string Title,
    string Subtitle,
    string? RouteHint);

public record ComplianceSummaryDto(
    int Expired,
    int Expiring7Days,
    int Expiring15Days,
    int Expiring30Days);

public record CreateMaintenanceRequestDto(
    int VehicleId,
    int? DriverId,
    string RequestType,
    string Priority,
    string IssueCategory,
    string Description,
    string? BreakdownLocation = null,
    string? DriverRemarks = null,
    string? PhotosJson = null,
    string? DocumentsJson = null);

public record UpdateMaintenanceRequestDto(
    string? Priority,
    string? IssueCategory,
    string? Description,
    string? BreakdownLocation,
    string? DriverRemarks,
    string? Status);

public record WorkOrderStatsDto(int Open, int InProgress, int Completed, int Cancelled);

public record WorkOrderListItemDto(
    int Id,
    string WorkOrderNumber,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    string? ServiceTypeName,
    string Status,
    string? Priority,
    string? MaintenanceType,
    decimal LaborCost,
    decimal PartsCost,
    decimal TotalCost,
    decimal EstimatedLaborCost,
    decimal EstimatedPartsCost,
    int? WorkshopId,
    string? WorkshopName,
    int? TechnicianId,
    string? TechnicianName,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate,
    DateTime? CompletedAt,
    DateTime CreatedAt);

public record WorkOrderPartUsageDto(
    int PartId,
    string PartName,
    int Quantity,
    decimal UnitCost,
    decimal TotalCost);

public record WorkOrderDetailDto(
    int Id,
    string WorkOrderNumber,
    int? RequestId,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    int? DriverId,
    string? DriverName,
    int? BranchId,
    string? BranchName,
    int? WorkshopId,
    string? WorkshopName,
    int? TechnicianId,
    string? TechnicianName,
    int? ServiceTypeId,
    string? ServiceTypeName,
    string? MaintenanceType,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate,
    DateTime? CompletedAt,
    decimal LaborCost,
    decimal PartsCost,
    decimal TotalCost,
    decimal EstimatedLaborCost,
    decimal EstimatedPartsCost,
    string Status,
    string? Priority,
    string? Notes,
    string? TechnicianNotes,
    DateTime CreatedAt,
    IReadOnlyList<WorkOrderPartUsageDto> PartsUsage);

public record CreateWorkOrderDto(
    int VehicleId,
    int? RequestId,
    int? ScheduleId,
    int? WorkshopId,
    int? TechnicianId,
    int? ServiceTypeId,
    string? ServiceTypeName,
    string? MaintenanceType,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate,
    decimal LaborCost,
    decimal PartsCost,
    string? Priority,
    string? Notes);

public record UpdateWorkOrderStatusDto(string Status, string? TechnicianNotes);

public record UpdateWorkOrderDto(
    int? WorkshopId,
    int? TechnicianId,
    string? Status);

public record TechnicianListItemDto(int Id, string FullName, int? WorkshopId, string? WorkshopName);

public record WorkshopDto(
    int Id,
    string Name,
    string WorkshopType,
    string? Location,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    int? Capacity,
    string? VendorType,
    string? ContractDetails,
    string? SLA,
    decimal? Rating,
    bool IsActive,
    int ActiveTechnicians);

public record CreateWorkshopDto(
    string Name,
    string WorkshopType,
    string? Location,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    int? Capacity,
    string? VendorType,
    string? ContractDetails,
    string? SLA,
    decimal? Rating);

public record UpdateWorkshopDto(
    string? Name,
    string? WorkshopType,
    string? Location,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    int? Capacity,
    string? VendorType,
    string? ContractDetails,
    string? SLA,
    decimal? Rating,
    bool? IsActive);

public record VendorDto(
    int Id,
    string Name,
    string Category,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    IReadOnlyList<string> Products,
    decimal? Rating,
    bool IsPreferred,
    bool IsActive);

public record CreateVendorDto(
    string Name,
    string Category,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    IReadOnlyList<string>? Products,
    decimal? Rating,
    bool IsPreferred = false);

public record UpdateVendorDto(
    string? Name,
    string? Category,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    IReadOnlyList<string>? Products,
    decimal? Rating,
    bool? IsPreferred,
    bool? IsActive);

public record WorkshopVendorStatsDto(
    int TotalWorkshops,
    int ActiveWorkshops,
    int TotalVendors,
    int PreferredVendors);

public record ServiceTypeDto(int Id, string Code, string Name, bool IsPreventive);

public record MaintenanceScheduleDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    string ServiceTypeName,
    string IntervalType,
    int IntervalValue,
    DateTime? LastServiceDate,
    decimal? LastServiceMileage,
    DateTime? NextDueDate,
    decimal? NextDueMileage,
    string Priority,
    bool IsActive);

public record MaintenanceScheduleListItemDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    decimal CurrentMileage,
    decimal? NextServiceMileage,
    DateTime? DueDate,
    string ServiceTypeName,
    string IntervalType,
    int IntervalValue,
    string Status,
    string Priority,
    bool IsActive,
    decimal? CurrentEngineHours,
    decimal? NextDueEngineHours,
    decimal? LastServiceMileage,
    DateTime? LastServiceDate,
    decimal? LastServiceEngineHours);

public record MaintenanceScheduleCalendarItemDto(
    int ScheduleId,
    int VehicleId,
    string VehicleName,
    string ServiceTypeName,
    DateTime? DueDate,
    string Status,
    string IntervalType,
    decimal? NextServiceMileage,
    decimal? NextDueEngineHours);

public record RescheduleMaintenanceScheduleDto(
    DateTime? LastServiceDate,
    decimal? LastServiceMileage,
    decimal? LastServiceEngineHours,
    string? IntervalType,
    int? IntervalValue);

public record UpdateMaintenanceScheduleDto(
    string? ServiceTypeName,
    int? ServiceTypeId,
    string? IntervalType,
    int? IntervalValue,
    string? Priority,
    bool? IsActive);

public record CreateMaintenanceScheduleDto(
    int VehicleId,
    int? ServiceTypeId,
    string ServiceTypeName,
    string IntervalType,
    int IntervalValue,
    DateTime? LastServiceDate,
    decimal? LastServiceMileage,
    decimal? LastServiceEngineHours,
    string Priority);

public record PartDto(
    int Id,
    string PartNumber,
    string PartName,
    string? Category,
    string? Brand,
    string? Supplier,
    decimal UnitCost,
    int MinStockLevel,
    int StockQuantity,
    bool IsLowStock,
    IReadOnlyList<string> VehicleCompatibility,
    string StockStatus,
    bool IsOutOfStock,
    string? Location);

public record CreatePartDto(
    string PartNumber,
    string PartName,
    string? Category,
    string? Brand,
    string? Supplier,
    decimal UnitCost,
    int MinStockLevel,
    int InitialStock,
    IReadOnlyList<string>? VehicleCompatibility,
    string? Location);

public record PartsInventoryStatsDto(
    int TotalParts,
    int LowStock,
    int OutOfStock,
    decimal InventoryValue);

public record AddPartStockDto(int Quantity, string? Location, string? Notes);

public record IssuePartDto(int VehicleId, int Quantity, int? WorkOrderId, string? Notes);

public record TransferPartStockDto(int Quantity, string FromLocation, string ToLocation, string? Notes);

public record PartUsageDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    int? WorkOrderId,
    string? WorkOrderNumber,
    int PartId,
    string PartName,
    int Quantity,
    decimal UnitCost,
    decimal TotalCost,
    DateTime UsedAt);

public record VehicleBreakdownDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    int? DriverId,
    string? DriverName,
    string? BreakdownLocation,
    string FaultReport,
    string Status,
    decimal RepairCost,
    DateTime ReportedAt);

public record MaintenanceHistoryItemDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    string Description,
    string? MaintenanceType,
    string? Category,
    decimal Cost,
    decimal LaborCost,
    decimal PartsCost,
    DateTime MaintenanceDate,
    DateTime? NextDueDate,
    string Status,
    string? ServiceProvider,
    string? WorkshopName);

public record VehicleServiceHistoryItemDto(
    int Id,
    string Source,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    string ServiceType,
    DateTime ServiceDate,
    string? WorkshopName,
    string? TechnicianName,
    decimal TotalCost,
    decimal LaborCost,
    decimal PartsCost,
    string? InvoiceUrl,
    string? Notes,
    string Status);

public record MaintenanceReportColumnDto(string Key, string Label, string Format);

public record MaintenanceReportRowDto(
    string Key,
    string Label,
    int Count,
    decimal TotalCost,
    IReadOnlyDictionary<string, object?> Fields);

public record MaintenanceReportDto(
    string ReportType,
    string Title,
    IReadOnlyList<MaintenanceReportColumnDto> Columns,
    IReadOnlyList<MaintenanceReportRowDto> Rows,
    decimal TotalCost,
    IReadOnlyDictionary<string, object?> Summary);

public record MaintenanceReportFiltersDto(
    int? VehicleId,
    int? BranchId,
    string? From,
    string? To,
    string? Status);

public record MaintenanceReportScheduleDto(
    int Id,
    string ReportType,
    MaintenanceReportFiltersDto Filters,
    string Frequency,
    string Recipients,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    bool IsActive);

public record CreateMaintenanceReportScheduleDto(
    string ReportType,
    MaintenanceReportFiltersDto Filters,
    string Frequency,
    string Recipients);

public record UpdateMaintenanceReportScheduleDto(
    string? Frequency,
    string? Recipients,
    bool? IsActive,
    MaintenanceReportFiltersDto? Filters);

public record FuelMaintenanceSummaryDto(
    IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> FuelCosts,
    IReadOnlyList<decimal> MaintenanceCosts,
    IReadOnlyList<HighCostVehicleDto> HighCostVehicles);

public record HighCostVehicleDto(
    int VehicleId,
    string VehicleName,
    decimal FuelCost,
    decimal MaintenanceCost);

public record ConvertRequestToWorkOrderDto(
    int? WorkshopId,
    int? TechnicianId,
    int? ServiceTypeId,
    string? ServiceTypeName,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate);
