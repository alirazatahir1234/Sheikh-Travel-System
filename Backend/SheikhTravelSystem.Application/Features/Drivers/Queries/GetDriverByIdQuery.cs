using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverByIdQuery(int Id) : IRequest<ApiResponse<DriverDto>>;

public class GetDriverByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDriverByIdQuery, ApiResponse<DriverDto>>
{
    public async Task<ApiResponse<DriverDto>> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var driver = await connection.QuerySingleOrDefaultAsync<DriverDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Phone, LicenseNumber, LicenseExpiryDate, CNIC, Address,
                  Status, IsActive, CreatedAt
                  FROM Drivers WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (driver is null)
            throw new NotFoundException("Driver", request.Id);

        return ApiResponse<DriverDto>.SuccessResponse(driver);
    }
}
