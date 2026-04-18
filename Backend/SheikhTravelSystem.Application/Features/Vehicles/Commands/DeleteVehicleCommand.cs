using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record DeleteVehicleCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteVehicleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { request.Id });

        if (!exists)
            throw new NotFoundException("Vehicle", request.Id);

        await connection.ExecuteAsync(
            "UPDATE Vehicles SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
            new { UpdatedAt = DateTime.UtcNow, request.Id });

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle deleted successfully.");
    }
}
