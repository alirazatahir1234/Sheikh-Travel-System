using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers;

namespace SheikhTravelSystem.Application.Features.Assignments;

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateAssignmentCommand(CreateAssignmentRequest Body) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => null;
}

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0);
        RuleFor(x => x.Body.DriverId).GreaterThan(0);
        RuleFor(x => x.Body.AssignmentType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Body.StartDate).NotEmpty();
        RuleFor(x => x.Body.Reason).MaximumLength(300).When(x => x.Body.Reason != null);
        RuleFor(x => x.Body.Notes).MaximumLength(500).When(x => x.Body.Notes != null);
    }
}

public class CreateAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAssignmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;

        // Validate vehicle
        var vehicle = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, int Status)>(
            new CommandDefinition(
                "SELECT Id, Name, Status FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
                new { body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (vehicle.Id == 0) throw new NotFoundException("Vehicle", body.VehicleId);
        if (vehicle.Status == 5 /* Draft */) throw new ConflictException("Vehicle is not active and cannot be assigned.");

        // Validate driver
        var driver = await connection.QuerySingleOrDefaultAsync<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)>(
            new CommandDefinition(
                "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { body.DriverId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (driver.VerificationStatus is null) throw new NotFoundException("Driver", body.DriverId);

        DriverAssignmentGuard.EnsureAssignable(
            driver.IsActive, (Domain.Enums.DriverStatus)driver.Status, driver.VerificationStatus, driver.LicenseExpiry);

        await DriverAssignmentValidation.EnsureDriverNotOnActiveTripAsync(connection, tenantId, body.DriverId, cancellationToken);

        var vehicleAlreadyAssigned = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND Status = N'Active'
                      AND IsDeleted = 0 AND DriverId <> @DriverId
                  ) THEN 1 ELSE 0 END",
                new { body.VehicleId, TenantId = tenantId, body.DriverId }, cancellationToken: cancellationToken));
        if (vehicleAlreadyAssigned)
            throw new ConflictException("Vehicle is already assigned to another driver.");

        // Close any existing active assignment for this driver
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE()
              WHERE DriverId = @DriverId AND TenantId = @TenantId AND Status = N'Active' AND IsDeleted = 0",
            new { body.DriverId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var assignmentType = body.AssignmentType.Trim();
        var assignmentId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO AssignmentHistory
                (TenantId, VehicleId, DriverId, AssignmentType, Status, StartAt, EndAt,
                 Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
              VALUES
                (@TenantId, @VehicleId, @DriverId, @AssignmentType, N'Active', @StartAt, @EndAt,
                 @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                body.VehicleId,
                body.DriverId,
                AssignmentType = assignmentType,
                StartAt = body.StartDate.ToUniversalTime(),
                EndAt = body.EndDate?.ToUniversalTime(),
                Reason = NullIfEmpty(body.Reason),
                Notes = NullIfEmpty(body.Notes),
                CreatedBy = currentUser.UserId?.ToString() ?? "system"
            }, cancellationToken: cancellationToken));

        // Set assignment number
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
            new { No = $"ASN-{assignmentId:D6}", Id = assignmentId }, cancellationToken: cancellationToken));

        // Log changelog
        await WriteChangelogAsync(connection, tenantId, assignmentId, null, body.VehicleId, null, body.DriverId,
            "Created", body.Reason, currentUser.UserId?.ToString(), cancellationToken);

        return ApiResponse<int>.SuccessResponse(assignmentId, "Assignment created successfully.");
    }

    private static Task WriteChangelogAsync(System.Data.IDbConnection conn, int tenantId, int assignmentId,
        int? oldVehicleId, int? newVehicleId, int? oldDriverId, int? newDriverId,
        string action, string? reason, string? by, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, NewVehicleId,
              OldDriverId, NewDriverId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @OldVehicleId, @NewVehicleId,
              @OldDriverId, @NewDriverId, @ActionType, @Reason, @CreatedBy, GETUTCDATE())",
            new { TenantId = tenantId, AssignmentId = assignmentId, OldVehicleId = oldVehicleId,
                  NewVehicleId = newVehicleId, OldDriverId = oldDriverId, NewDriverId = newDriverId,
                  ActionType = action, Reason = NullIfEmpty(reason), CreatedBy = by }, cancellationToken: ct));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ── Transfer ──────────────────────────────────────────────────────────────────

public record TransferAssignmentCommand(int AssignmentId, TransferAssignmentRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Transfer";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class TransferAssignmentCommandValidator : AbstractValidator<TransferAssignmentCommand>
{
    public TransferAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).GreaterThan(0);
        RuleFor(x => x.Body.NewVehicleId).GreaterThan(0);
    }
}

public class TransferAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<TransferAssignmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(TransferAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (existing.Status != "Active") throw new ConflictException("Only active assignments can be transferred.");

        var newVehicleId = request.Body.NewVehicleId;
        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { VehicleId = newVehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (!vehicleExists) throw new NotFoundException("Vehicle", newVehicleId);

        var vehicleAlreadyAssigned2 = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND Status = N'Active'
                      AND IsDeleted = 0 AND Id <> @CurrentId
                  ) THEN 1 ELSE 0 END",
                new { VehicleId = newVehicleId, TenantId = tenantId, CurrentId = request.AssignmentId },
                cancellationToken: cancellationToken));
        if (vehicleAlreadyAssigned2)
            throw new ConflictException("Vehicle is already assigned to another driver.");

        // Close current, open new
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = request.AssignmentId }, cancellationToken: cancellationToken));

        var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO AssignmentHistory
                (TenantId, VehicleId, DriverId, AssignmentType, Status, StartAt,
                 Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
              VALUES (@TenantId, @NewVehicleId, @DriverId, N'Transfer', N'Active', GETUTCDATE(),
                @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                NewVehicleId = newVehicleId,
                existing.DriverId,
                Reason = NullIfEmpty(request.Body.Reason),
                Notes = NullIfEmpty(request.Body.Notes),
                CreatedBy = currentUser.UserId?.ToString() ?? "system"
            }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
            new { No = $"ASN-{newId:D6}", Id = newId }, cancellationToken: cancellationToken));

        await WriteChangelogAsync(connection, tenantId, newId,
            existing.VehicleId, newVehicleId, existing.DriverId, existing.DriverId,
            "Transferred", request.Body.Reason, currentUser.UserId?.ToString(), cancellationToken);

        return ApiResponse<int>.SuccessResponse(newId, "Assignment transferred successfully.");
    }

    private static Task WriteChangelogAsync(System.Data.IDbConnection conn, int tenantId, int assignmentId,
        int? oldVehicleId, int? newVehicleId, int? oldDriverId, int? newDriverId,
        string action, string? reason, string? by, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, NewVehicleId,
              OldDriverId, NewDriverId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @OldVehicleId, @NewVehicleId,
              @OldDriverId, @NewDriverId, @ActionType, @Reason, @CreatedBy, GETUTCDATE())",
            new { TenantId = tenantId, AssignmentId = assignmentId, OldVehicleId = oldVehicleId,
                  NewVehicleId = newVehicleId, OldDriverId = oldDriverId, NewDriverId = newDriverId,
                  ActionType = action, Reason = NullIfEmpty(reason), CreatedBy = by }, cancellationToken: ct));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ── Complete ──────────────────────────────────────────────────────────────────

public record CompleteAssignmentCommand(int AssignmentId, CompleteAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Complete";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class CompleteAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CompleteAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CompleteAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (existing.Status != "Active") throw new ConflictException("Only active assignments can be completed.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(),
              Reason = ISNULL(@Reason, Reason), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = request.AssignmentId, Reason = NullIfEmpty(request.Body.Reason), ModifiedBy = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @VehicleId, N'Completed', @Reason, @CreatedBy, GETUTCDATE())",
            new { TenantId = tenantId, AssignmentId = request.AssignmentId, existing.VehicleId,
                  Reason = NullIfEmpty(request.Body.Reason), CreatedBy = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Assignment completed.");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ── Cancel ────────────────────────────────────────────────────────────────────

public record CancelAssignmentCommand(int AssignmentId, CancelAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Cancel";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class CancelAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (existing.Status is "Completed" or "Cancelled")
            throw new ConflictException("Assignment is already closed.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Cancelled', EndAt = GETUTCDATE(),
              Reason = ISNULL(@Reason, Reason), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = request.AssignmentId, Reason = NullIfEmpty(request.Body.Reason), ModifiedBy = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @VehicleId, N'Cancelled', @Reason, @CreatedBy, GETUTCDATE())",
            new { TenantId = tenantId, AssignmentId = request.AssignmentId, existing.VehicleId,
                  Reason = NullIfEmpty(request.Body.Reason), CreatedBy = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Assignment cancelled.");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
