using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for maintenance module. SQL lives in Infrastructure.
/// </summary>
public interface IMaintenanceModuleRepository
{
    Task<ApiResponse<MaintenanceRequestStatsDto>> GetMaintenanceRequestStatsAsync(GetMaintenanceRequestStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ApproveMaintenanceRequestAsync(ApproveMaintenanceRequestCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RejectMaintenanceRequestAsync(RejectMaintenanceRequestCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>> SearchMaintenanceAsync(SearchMaintenanceQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DismissMaintenanceAlertAsync(DismissMaintenanceAlertCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplianceSummaryDto>> GetMaintenanceComplianceSummaryAsync(GetMaintenanceComplianceSummaryQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<string>> UploadMaintenanceRequestAttachmentAsync(UploadMaintenanceRequestAttachmentCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MaintenanceDashboardDto>> GetMaintenanceDashboardAsync(GetMaintenanceDashboardQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>> GetMaintenanceAlertsAsync(GetMaintenanceAlertsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<VendorDto>>> ListVendorsAsync(ListVendorsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<VendorDto>> GetVendorByIdAsync(GetVendorByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateMaintenanceScheduleAsync(CreateMaintenanceScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<WorkshopDto>>> ListWorkshopsAsync(ListWorkshopsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateWorkshopAsync(CreateWorkshopCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<ServiceTypeDto>>> ListServiceTypesAsync(ListServiceTypesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateVendorAsync(CreateVendorCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateVendorAsync(UpdateVendorCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> SetVendorActiveAsync(SetVendorActiveCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateWorkOrderAsync(CreateWorkOrderCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateWorkOrderStatusAsync(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateWorkOrderAsync(UpdateWorkOrderCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WorkshopDto>> GetWorkshopByIdAsync(GetWorkshopByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateWorkshopAsync(UpdateWorkshopCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> SetWorkshopActiveAsync(SetWorkshopActiveCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateMaintenanceRequestAsync(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateMaintenanceRequestAsync(UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> ConvertMaintenanceRequestAsync(ConvertMaintenanceRequestCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<PartDto>>> ListPartsAsync(ListPartsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreatePartAsync(CreatePartCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AddPartStockAsync(AddPartStockCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> IssuePartAsync(IssuePartCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> TransferPartStockAsync(TransferPartStockCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> RecordPartUsageAsync(RecordPartUsageCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResult<MaintenanceRequestDto>>> ListMaintenanceRequestsAsync(ListMaintenanceRequestsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MaintenanceRequestDto>> GetMaintenanceRequestByIdAsync(GetMaintenanceRequestByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MaintenanceReportDto>> GetMaintenanceReportAsync(GetMaintenanceReportQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WorkshopVendorStatsDto>> GetWorkshopVendorStatsAsync(GetWorkshopVendorStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>> GetMaintenanceHistoryAsync(GetMaintenanceHistoryQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>> ListMaintenanceReportSchedulesAsync(ListMaintenanceReportSchedulesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateMaintenanceReportScheduleAsync(CreateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateMaintenanceReportScheduleAsync(UpdateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteMaintenanceReportScheduleAsync(DeleteMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WorkOrderStatsDto>> GetWorkOrderStatsAsync(GetWorkOrderStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> ListWorkOrdersAsync(ListWorkOrdersQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WorkOrderDetailDto>> GetWorkOrderByIdAsync(GetWorkOrderByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<TechnicianListItemDto>>> ListTechniciansAsync(ListTechniciansQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>> ListMaintenanceSchedulesAsync(ListMaintenanceSchedulesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>> GetMaintenanceScheduleCalendarAsync(GetMaintenanceScheduleCalendarQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>> ListSchedulableMaintenanceVehiclesAsync(ListSchedulableMaintenanceVehiclesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RescheduleMaintenanceScheduleAsync(RescheduleMaintenanceScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateMaintenanceScheduleAsync(UpdateMaintenanceScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateWorkOrderFromScheduleAsync(CreateWorkOrderFromScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PartsInventoryStatsDto>> GetPartsInventoryStatsAsync(GetPartsInventoryStatsQuery request, CancellationToken cancellationToken = default);
}
