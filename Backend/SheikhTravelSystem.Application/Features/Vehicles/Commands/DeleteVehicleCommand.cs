using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record DeleteVehicleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class DeleteVehicleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage,
    ILogger<DeleteVehicleCommandHandler> logger)
    : IRequestHandler<DeleteVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Vehicle", request.Id);

        var fileUrls = (await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT FileUrl FROM VehicleDocuments
                  WHERE VehicleId = @Id AND TenantId = @TenantId
                    AND IsDeleted = 0 AND FileUrl IS NOT NULL AND FileUrl <> ''",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE VehicleDocuments SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE VehicleId = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        foreach (var url in fileUrls)
        {
            try
            {
                await fileStorage.DeleteAsync(url, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete stored file for vehicle {VehicleId}: {FileUrl}", request.Id, url);
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle deleted successfully.");
    }
}
