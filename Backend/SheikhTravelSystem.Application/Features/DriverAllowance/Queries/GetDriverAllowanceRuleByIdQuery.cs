using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

public record GetDriverAllowanceRuleByIdQuery(int Id) : IRequest<ApiResponse<DriverAllowanceRuleDto>>;

public class GetDriverAllowanceRuleByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDriverAllowanceRuleByIdQuery, ApiResponse<DriverAllowanceRuleDto>>
{
    public async Task<ApiResponse<DriverAllowanceRuleDto>> Handle(
        GetDriverAllowanceRuleByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var rule = await connection.QuerySingleOrDefaultAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (rule is null)
            throw new NotFoundException("DriverAllowanceRule", request.Id);

        return ApiResponse<DriverAllowanceRuleDto>.SuccessResponse(rule);
    }
}
