using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUserByIdQuery(int Id) : IRequest<ApiResponse<UserDto>>;

public class GetUserByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetUserByIdQuery, ApiResponse<UserDto>>
{
    public async Task<ApiResponse<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<UserDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt
                  FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (user is null)
            throw new NotFoundException("User", request.Id);

        return ApiResponse<UserDto>.SuccessResponse(user);
    }
}
