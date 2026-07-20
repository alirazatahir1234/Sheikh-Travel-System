using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record SetDriverStatusCommand(string Status) : IRequest<ApiResponse<DriverStatusDto>>;
public record GetDriverStatusQuery : IRequest<ApiResponse<DriverStatusDto>>;

public class SetDriverStatusCommandValidator : AbstractValidator<SetDriverStatusCommand>
{
    public SetDriverStatusCommandValidator()
    {
        RuleFor(x => x.Status).NotEmpty();
    }
}

public class GetDriverStatusQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverStatusQuery, ApiResponse<DriverStatusDto>>
{
    public async Task<ApiResponse<DriverStatusDto>> Handle(GetDriverStatusQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverStatusDto>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Drivers WHERE Id=@DriverId AND TenantId=@TenantId AND IsDeleted=0",
            new { DriverId = driverId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (!status.HasValue) return ApiResponse<DriverStatusDto>.FailResponse("Driver not found.");
        return ApiResponse<DriverStatusDto>.SuccessResponse(new DriverStatusDto(status.Value, Name(status.Value)));
    }

    internal static string Name(int status) => status switch
    {
        1 => "Online",
        2 => "On Trip",
        3 => "Break",
        4 => "Suspended",
        5 => "Unavailable",
        _ => "Unknown"
    };
}

public class SetDriverStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<SetDriverStatusCommand, ApiResponse<DriverStatusDto>>
{
    public async Task<ApiResponse<DriverStatusDto>> Handle(SetDriverStatusCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverStatusDto>.FailResponse("Driver identity required.");
        var normalized = request.Status.Trim().ToLowerInvariant();
        var target = normalized switch
        {
            "online" or "available" => 1,
            "busy" or "ontrip" or "on trip" => 2,
            "break" or "offline" or "off duty" => 3,
            "unavailable" => 5,
            _ => -1
        };
        if (target < 0) return ApiResponse<DriverStatusDto>.FailResponse("Unsupported status.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Drivers
              SET Status = @Status, UpdatedAt = GETUTCDATE()
              WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
            new { Status = target, DriverId = driverId.Value, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (rows <= 0) return ApiResponse<DriverStatusDto>.FailResponse("Driver not found.");

        return ApiResponse<DriverStatusDto>.SuccessResponse(new DriverStatusDto(target, GetDriverStatusQueryHandler.Name(target)));
    }
}
