using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record VehicleDocumentDto(
    int Id,
    int VehicleId,
    string DocumentType,
    string? FileUrl,
    DateTime? ExpiryDate,
    string? Notes);

public record GetVehicleDocumentsQuery(int VehicleId) : IRequest<ApiResponse<IReadOnlyList<VehicleDocumentDto>>>;

public class GetVehicleDocumentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleDocumentsQuery, ApiResponse<IReadOnlyList<VehicleDocumentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VehicleDocumentDto>>> Handle(
        GetVehicleDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<VehicleDocumentDto>(new CommandDefinition(
            @"SELECT Id, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes
              FROM (
                SELECT Id, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt,
                  ROW_NUMBER() OVER (
                    PARTITION BY DocumentType
                    ORDER BY
                      CASE WHEN NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL THEN 0 ELSE 1 END,
                      CreatedAt DESC
                  ) AS rn
                FROM VehicleDocuments
                WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
              ) ranked
              WHERE DocumentType = N'VehicleImage' OR rn = 1
              ORDER BY CreatedAt DESC",
            new { request.VehicleId, TenantId = tenantContext.GetRequiredTenantId() },
            cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<VehicleDocumentDto>>.SuccessResponse(rows.ToList());
    }
}

public record CreateVehicleDocumentCommand(
    int VehicleId,
    string DocumentType,
    string? FileUrl,
    DateTime? ExpiryDate,
    string? Notes) : IRequest<ApiResponse<int>>;

public class CreateVehicleDocumentCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateVehicleDocumentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVehicleDocumentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existingId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            @"SELECT TOP 1 Id
              FROM VehicleDocuments
              WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                AND DocumentType = @DocumentType AND IsDeleted = 0
              ORDER BY
                CASE WHEN NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL THEN 0 ELSE 1 END,
                CreatedAt DESC",
            new
            {
                TenantId = tenantId,
                request.VehicleId,
                request.DocumentType
            },
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET FileUrl = COALESCE(@FileUrl, FileUrl),
                      ExpiryDate = COALESCE(@ExpiryDate, ExpiryDate),
                      Notes = COALESCE(@Notes, Notes),
                      UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId",
                new
                {
                    Id = existingId.Value,
                    TenantId = tenantId,
                    request.FileUrl,
                    request.ExpiryDate,
                    request.Notes
                },
                cancellationToken: cancellationToken));

            return ApiResponse<int>.SuccessResponse(existingId.Value, "Document updated.");
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO VehicleDocuments (TenantId, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt, IsDeleted)
              VALUES (@TenantId, @VehicleId, @DocumentType, @FileUrl, @ExpiryDate, @Notes, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantContext.GetRequiredTenantId(),
                request.VehicleId,
                request.DocumentType,
                request.FileUrl,
                request.ExpiryDate,
                request.Notes
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Document saved.");
    }
}
