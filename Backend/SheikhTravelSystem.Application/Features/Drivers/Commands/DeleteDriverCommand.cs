using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record DeleteDriverCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => Id;
}

public class DeleteDriverCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage,
    ILogger<DeleteDriverCommandHandler> logger)
    : IRequestHandler<DeleteDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDriverCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var photoUrl = await driverRepository.SoftDeleteAsync(tenantId, request.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(photoUrl))
        {
            try
            {
                await fileStorage.DeleteAsync(photoUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete driver photo for driver {DriverId}", request.Id);
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Driver deleted successfully.");
    }
}
