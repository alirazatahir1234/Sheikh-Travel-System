using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record SetPrimaryVehicleImageCommand(int VehicleId, int DocumentId) : IRequest<ApiResponse<bool>>;

public class SetPrimaryVehicleImageCommandValidator : AbstractValidator<SetPrimaryVehicleImageCommand>
{
    public SetPrimaryVehicleImageCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DocumentId).GreaterThan(0);
    }
}

public class SetPrimaryVehicleImageCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<SetPrimaryVehicleImageCommand, ApiResponse<bool>>
{
    private sealed class VehicleImageDocumentRow
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public async Task<ApiResponse<bool>> Handle(
        SetPrimaryVehicleImageCommand request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var doc = await connection.QuerySingleOrDefaultAsync<VehicleImageDocumentRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, Notes
                  FROM VehicleDocuments
                  WHERE Id = @DocumentId AND VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DocumentId, request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (doc is null)
            throw new NotFoundException("Vehicle document", request.DocumentId);

        if (!string.Equals(doc.DocumentType, "VehicleImage", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Only vehicle images can be set as the display photo.");

        var existing = await connection.QueryAsync<VehicleImageDocumentRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, Notes
                  FROM VehicleDocuments
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = N'VehicleImage' AND IsDeleted = 0",
                new { request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        foreach (var row in existing)
        {
            var cleared = VehicleImageNotes.ClearPrimary(row.Notes);
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET Notes = @Notes, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId",
                new { Notes = string.IsNullOrWhiteSpace(cleared) ? null : cleared, row.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        var primaryNotes = VehicleImageNotes.SetPrimary(doc.Notes);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE VehicleDocuments
              SET Notes = @Notes, UpdatedAt = GETUTCDATE()
              WHERE Id = @DocumentId AND TenantId = @TenantId",
            new { Notes = primaryNotes, request.DocumentId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Display photo updated.");
    }
}
