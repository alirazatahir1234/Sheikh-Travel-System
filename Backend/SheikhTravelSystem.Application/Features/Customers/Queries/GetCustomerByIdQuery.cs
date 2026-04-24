using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

/// <summary>
/// Fetches a single customer (non-deleted) by identifier.
/// </summary>
public record GetCustomerByIdQuery(int Id) : IRequest<ApiResponse<CustomerDto>>;

public class GetCustomerByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetCustomerByIdQuery, ApiResponse<CustomerDto>>
{
    public async Task<ApiResponse<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var customer = await connection.QuerySingleOrDefaultAsync<CustomerDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt
                  FROM Customers WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (customer is null)
            throw new NotFoundException("Customer", request.Id);

        return ApiResponse<CustomerDto>.SuccessResponse(customer);
    }
}
