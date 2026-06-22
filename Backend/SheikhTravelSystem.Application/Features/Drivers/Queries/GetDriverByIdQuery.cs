using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverByIdQuery(int Id) : IRequest<ApiResponse<DriverDto>>;

public class GetDriverByIdQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetDriverByIdQuery, ApiResponse<DriverDto>>
{
    public async Task<ApiResponse<DriverDto>> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var driver = await connection.QuerySingleOrDefaultAsync<DriverDto>(
            new CommandDefinition(
                $@"SELECT {DriverSql.DetailColumns}
                  {DriverSql.DetailFrom}
                  WHERE d.Id = @Id AND d.TenantId = @TenantId AND d.IsDeleted = 0",
                new
                {
                    request.Id,
                    TenantId = tenantId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));

        if (driver is null)
            throw new NotFoundException("Driver", request.Id);

        if (!string.IsNullOrWhiteSpace(driver.PhotoUrl))
        {
            driver = driver with { PhotoUrl = fileStorage.ResolveReadUrl(driver.PhotoUrl) };
        }

        return ApiResponse<DriverDto>.SuccessResponse(driver);
    }
}
