using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class UpdateDriverRatingCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateDriverRatingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverRatingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET Rating = @Rating, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Rating = request.Rating, Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", request.DriverId);

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
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDriverViolationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverViolationCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverViolations (TenantId, DriverId, ViolationType, Severity, OccurredAt, Description, BookingId, GpsAlertId, Status, CreatedBy, CreatedAt)
                  VALUES (@TenantId, @DriverId, @ViolationType, @Severity, @OccurredAt, @Description, @BookingId, @GpsAlertId, N'Open', @CreatedBy, GETUTCDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    request.DriverId,
                    ViolationType = body.ViolationType.Trim(),
                    Severity = body.Severity.Trim(),
                    body.OccurredAt,
                    body.Description,
                    body.BookingId,
                    body.GpsAlertId,
                    CreatedBy = currentUser.UserId?.ToString() ?? "api"
                },
                cancellationToken: cancellationToken));

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

public class CreateDriverAttendanceCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateDriverAttendanceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverAttendanceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverAttendance (TenantId, DriverId, AttendanceDate, Status, CheckInAt, CheckOutAt, Notes, CreatedAt)
                  VALUES (@TenantId, @DriverId, @AttendanceDate, @Status, @CheckInAt, @CheckOutAt, @Notes, GETUTCDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    request.DriverId,
                    AttendanceDate = body.AttendanceDate.Date,
                    Status = body.Status.Trim(),
                    body.CheckInAt,
                    body.CheckOutAt,
                    body.Notes
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Attendance recorded.");
    }
}
