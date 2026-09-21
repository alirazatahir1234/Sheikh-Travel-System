using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record VehicleDocumentDto(
    int Id, int VehicleId, string DocumentType, string? FileUrl, DateTime? ExpiryDate, string? Notes);

public record GetVehicleDocumentsQuery(int VehicleId) : IRequest<ApiResponse<IReadOnlyList<VehicleDocumentDto>>>;

public class GetVehicleDocumentsQueryHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetVehicleDocumentsQuery, ApiResponse<IReadOnlyList<VehicleDocumentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VehicleDocumentDto>>> Handle(
        GetVehicleDocumentsQuery request, CancellationToken cancellationToken)
    {
        var rows = (await vehicleRepository.GetDocumentsAsync(
                request.VehicleId, tenantContext.GetRequiredTenantId(), cancellationToken))
            .Select(row => string.IsNullOrWhiteSpace(row.FileUrl)
                ? row
                : row with { FileUrl = fileStorage.ResolveReadUrl(row.FileUrl) })
            .ToList();

        return ApiResponse<IReadOnlyList<VehicleDocumentDto>>.SuccessResponse(rows);
    }
}

public record CreateVehicleDocumentCommand(
    int VehicleId, string DocumentType, string? FileUrl, DateTime? ExpiryDate, string? Notes)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "VehicleDocument";
    public int? AuditEntityId => null;
}

public class CreateVehicleDocumentCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateVehicleDocumentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVehicleDocumentCommand request, CancellationToken cancellationToken)
    {
        var (id, _) = await vehicleRepository.UpsertDocumentAsync(
            request.VehicleId, tenantContext.GetRequiredTenantId(),
            request.DocumentType, request.FileUrl, request.ExpiryDate, request.Notes, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Document saved.");
    }
}
