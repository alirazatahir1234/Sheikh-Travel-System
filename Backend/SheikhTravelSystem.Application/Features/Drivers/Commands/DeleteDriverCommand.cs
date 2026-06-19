using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record DeleteDriverCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => Id;
}

public class DeleteDriverCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage,
    ILogger<DeleteDriverCommandHandler> logger)
    : IRequestHandler<DeleteDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.Id);

        var photoUrl = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT PhotoUrl FROM Drivers WHERE Id = @Id AND TenantId = @TenantId",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

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
