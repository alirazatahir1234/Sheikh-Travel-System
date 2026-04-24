using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

/// <summary>
/// Soft-deletes a customer by identifier (sets IsDeleted = 1).
/// </summary>
public record DeleteCustomerCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteCustomerCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Customer", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Customers SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { request.Id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Customer deleted successfully.");
    }
}
