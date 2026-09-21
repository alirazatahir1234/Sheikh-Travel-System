using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UpdateDriverRatingCommand(int DriverId, decimal Rating) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateRating";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class UpdateDriverRatingCommandValidator : AbstractValidator<UpdateDriverRatingCommand>
{
    public UpdateDriverRatingCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Rating).InclusiveBetween(0, 5);
    }
}

public class UpdateDriverRatingCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateDriverRatingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverRatingCommand request, CancellationToken cancellationToken)
    {
        await driverRepository.UpdateRatingAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Rating, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Driver rating updated.");
    }
}

public record CreateDriverViolationCommand(int DriverId, CreateDriverViolationRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateViolation";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class CreateDriverViolationCommandValidator : AbstractValidator<CreateDriverViolationCommand>
{
    public CreateDriverViolationCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Body.ViolationType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Body.Severity).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Body.OccurredAt).NotEmpty();
    }
}

public class CreateDriverViolationCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDriverViolationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverViolationCommand request, CancellationToken cancellationToken)
    {
        var id = await driverRepository.CreateViolationAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Body,
            currentUser.UserId?.ToString() ?? "api", cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Violation logged.");
    }
}

public record CreateDriverAttendanceCommand(int DriverId, CreateDriverAttendanceRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateAttendance";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class CreateDriverAttendanceCommandValidator : AbstractValidator<CreateDriverAttendanceCommand>
{
    public CreateDriverAttendanceCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Body.AttendanceDate).NotEmpty();
        RuleFor(x => x.Body.Status).NotEmpty().MaximumLength(20);
    }
}

public class CreateDriverAttendanceCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateDriverAttendanceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverAttendanceCommand request, CancellationToken cancellationToken)
    {
        var id = await driverRepository.CreateAttendanceAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Body, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Attendance recorded.");
    }
}
