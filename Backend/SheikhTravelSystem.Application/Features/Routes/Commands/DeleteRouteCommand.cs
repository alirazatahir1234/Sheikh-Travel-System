using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Routes.Commands;

public record DeleteRouteCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteRouteCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteRouteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Route", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Routes SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Route deleted successfully.");
    }
}
