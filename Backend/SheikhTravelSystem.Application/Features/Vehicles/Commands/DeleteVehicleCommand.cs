using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record DeleteVehicleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class DeleteVehicleCommandHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage,
    ILogger<DeleteVehicleCommandHandler> logger)
    : IRequestHandler<DeleteVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var fileUrls = await vehicleRepository.SoftDeleteAsync(
            request.Id, tenantContext.GetRequiredTenantId(), cancellationToken);

        foreach (var url in fileUrls)
        {
            try { await fileStorage.DeleteAsync(url, cancellationToken); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete stored file for vehicle {VehicleId}: {FileUrl}", request.Id, url);
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle deleted successfully.");
    }
}
