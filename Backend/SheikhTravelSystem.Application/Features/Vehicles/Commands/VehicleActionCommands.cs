using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record ChangeVehicleStatusCommand(int Id, ChangeVehicleStatusRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ChangeStatus";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class ChangeVehicleStatusCommandValidator : AbstractValidator<ChangeVehicleStatusCommand>
{
    public ChangeVehicleStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.Status).IsInEnum();
    }
}

public class ChangeVehicleStatusCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ChangeVehicleStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Vehicle", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { Status = (int)request.Body.Status, UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle status updated.");
    }
}

public record AssignVehicleDriverCommand(int Id, AssignVehicleDriverRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class AssignVehicleDriverCommandValidator : AbstractValidator<AssignVehicleDriverCommand>
{
    public AssignVehicleDriverCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.DriverId).GreaterThan(0);
    }
}

public class AssignVehicleDriverCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignVehicleDriverCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AssignVehicleDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.Id);

        var driverExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { body.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!driverExists)
            throw new NotFoundException("Driver", body.DriverId);

        var driverRow = await connection.QuerySingleAsync<(bool IsActive, int Status, string VerificationStatus, DateTime LicenseExpiry)>(
            new CommandDefinition(
                "SELECT IsActive, Status, VerificationStatus, LicenseExpiryDate FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { body.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        DriverAssignmentGuard.EnsureAssignableForVehicleTrip(
            driverRow.IsActive,
            (DriverStatus)driverRow.Status,
            driverRow.VerificationStatus,
            driverRow.LicenseExpiry);

        await DriverAssignmentValidation.EnsureDriverNotOnActiveTripAsync(
            connection, tenantId, body.DriverId, cancellationToken);

        await DriverAssignmentValidation.CompleteActiveAssignmentsAsync(
            connection, tenantId, body.DriverId, request.Id, transaction: null, cancellationToken);

        if (body.BookingId is int bookingId)
        {
            var bookingStatus = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT Status FROM Bookings WHERE Id = @BookingId AND TenantId = @TenantId AND IsDeleted = 0",
                    new { BookingId = bookingId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (bookingStatus is null)
                throw new NotFoundException("Booking", bookingId);

            if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
                throw new ConflictException("Can only assign driver to pending or confirmed bookings.");

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Bookings SET DriverId = @DriverId, VehicleId = @VehicleId, UpdatedAt = @UpdatedAt
                      WHERE Id = @BookingId AND TenantId = @TenantId",
                    new { body.DriverId, VehicleId = request.Id, BookingId = bookingId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                    cancellationToken: cancellationToken));
        }

        var assignmentType = string.IsNullOrWhiteSpace(body.AssignmentType) ? "Trip" : body.AssignmentType.Trim();
        var createdBy = currentUser.UserId?.ToString() ?? "api";

        var assignmentId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO AssignmentHistory (TenantId, VehicleId, DriverId, BookingId, AssignmentType,
                  Status, StartAt, CreatedAt, CreatedBy, IsDeleted)
                  VALUES (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType,
                  'Active', @StartAt, @CreatedAt, @CreatedBy, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    TenantId = tenantId,
                    VehicleId = request.Id,
                    body.DriverId,
                    body.BookingId,
                    AssignmentType = assignmentType,
                    StartAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId AND Status = @Available",
                new
                {
                    Status = (int)VehicleStatus.OnTrip,
                    UpdatedAt = DateTime.UtcNow,
                    request.Id,
                    TenantId = tenantId,
                    Available = (int)VehicleStatus.Available
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(assignmentId, "Driver assigned to vehicle.");
    }
}

public record AssignVehicleGpsCommand(int Id, AssignVehicleGpsRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignGps";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class AssignVehicleGpsCommandValidator : AbstractValidator<AssignVehicleGpsCommand>
{
    public AssignVehicleGpsCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.GpsDeviceId).GreaterThan(0);
    }
}

public class AssignVehicleGpsCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<AssignVehicleGpsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignVehicleGpsCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var deviceId = request.Body.GpsDeviceId;

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.Id);

        // Allow claiming a Traccar-synced device (TenantId IS NULL = unowned) or one already owned by this tenant.
        var deviceExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM GpsDevices
                    WHERE Id = @DeviceId AND IsDeleted = 0
                      AND (TenantId = @TenantId OR TenantId IS NULL)
                ) THEN 1 ELSE 0 END
                """,
                new { DeviceId = deviceId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!deviceExists)
            throw new NotFoundException("GpsDevice", deviceId);

        // Unlink this device from any other vehicle in the same tenant.
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE GpsDevices SET VehicleId = NULL, UpdatedAt = @UpdatedAt WHERE VehicleId = @VehicleId AND Id <> @DeviceId AND (TenantId = @TenantId OR TenantId IS NULL)",
                new { VehicleId = request.Id, DeviceId = deviceId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET GpsDeviceId = @DeviceId, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { DeviceId = deviceId, UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        // Stamp TenantId on the device when it is claimed (covers Traccar-imported devices with NULL TenantId).
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE GpsDevices SET VehicleId = @VehicleId, TenantId = @TenantId, UpdatedAt = @UpdatedAt WHERE Id = @DeviceId",
                new { VehicleId = request.Id, DeviceId = deviceId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "GPS device assigned to vehicle.");
    }
}

public record PublishVehicleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Publish";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class PublishVehicleCommandValidator : AbstractValidator<PublishVehicleCommand>
{
    public PublishVehicleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class PublishVehicleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<PublishVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(PublishVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<(string Name, string RegistrationNumber, int SeatingCapacity, decimal FuelAverage, int Status)>(
            new CommandDefinition(
                @"SELECT Name, RegistrationNumber, SeatingCapacity, FuelAverage, Status
                  FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (row.Name is null)
            throw new NotFoundException("Vehicle", request.Id);

        if (string.IsNullOrWhiteSpace(row.Name))
            throw new ValidationException("Vehicle name is required before publishing.");
        if (string.IsNullOrWhiteSpace(row.RegistrationNumber) || row.RegistrationNumber.StartsWith("DRAFT-", StringComparison.Ordinal))
            throw new ValidationException("A valid license plate is required before publishing.");
        if (row.SeatingCapacity <= 0)
            throw new ValidationException("Seating capacity is required before publishing.");
        if (row.FuelAverage <= 0)
            throw new ValidationException("Fuel economy is required before publishing.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { Status = (int)VehicleStatus.Available, UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle published successfully.");
    }
}
