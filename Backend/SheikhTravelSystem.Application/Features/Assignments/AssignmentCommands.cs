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

        var validation = await AssignmentValidation.ValidateAsync(connection, tenantId,
            new ValidateAssignmentRequest(body.VehicleId, body.DriverId, body.StartDate, body.AssignmentType),
            cancellationToken);

        if (!validation.CanProceed)
            throw new ConflictException(string.Join(" ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message)));

        DriverAssignmentValidation.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        try
        {
            await DriverAssignmentValidation.CompleteActiveAssignmentsAsync(
                connection, tenantId, body.DriverId, body.VehicleId, transaction, cancellationToken);

            var startAt = body.StartDate.ToUniversalTime();
            var status = AssignmentValidation.ResolveInitialStatus(startAt, body.AssignmentType.Trim());
            var assignmentType = body.AssignmentType.Trim();
            var purpose = string.IsNullOrWhiteSpace(body.Purpose) ? assignmentType : body.Purpose.Trim();

            var assignmentId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO AssignmentHistory
                    (TenantId, VehicleId, DriverId, BookingId, AssignmentType, Status, StartAt, EndAt,
                     Purpose, PickupLocation, DropLocation, OdometerStart, Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
                  VALUES
                    (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType, @Status, @StartAt, @EndAt,
                     @Purpose, @PickupLocation, @DropLocation, @OdometerStart, @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    body.VehicleId,
                    body.DriverId,
                    body.BookingId,
                    AssignmentType = assignmentType,
                    Status = status,
                    StartAt = startAt,
                    EndAt = body.EndDate?.ToUniversalTime(),
                    Purpose = purpose,
                    PickupLocation = NullIfEmpty(body.PickupLocation),
                    DropLocation = NullIfEmpty(body.DropLocation),
                    body.OdometerStart,
                    Reason = NullIfEmpty(body.Reason),
                    Notes = NullIfEmpty(body.Notes),
                    CreatedBy = currentUser.UserId?.ToString() ?? "system"
                }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
                new { No = $"ASN-{assignmentId:D6}", Id = assignmentId },
                transaction: transaction, cancellationToken: cancellationToken));

            await AssignmentChangelogWriter.WriteAsync(connection, tenantId, assignmentId,
                null, body.VehicleId, null, body.DriverId,
                status == "PendingApproval" ? "Submitted" : "Created",
                body.Reason, currentUser.UserId?.ToString(), cancellationToken, transaction);

            transaction.Commit();
            return ApiResponse<int>.SuccessResponse(assignmentId, "Assignment created successfully.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

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
        RuleFor(x => x.Body.TransferType).NotEmpty();
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
        var body = request.Body;
        var transferType = body.TransferType.Trim();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status, string AssignmentType)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status, a.AssignmentType
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (!AssignmentValidation.IsOpenStatus(existing.Status))
            throw new ConflictException("Only open assignments can be transferred.");

        var newVehicleId = body.NewVehicleId ?? existing.VehicleId;
        var newDriverId = body.NewDriverId ?? existing.DriverId
            ?? throw new ConflictException("Assignment has no driver to transfer.");

        if (transferType.Equals("Vehicle", StringComparison.OrdinalIgnoreCase) && !body.NewVehicleId.HasValue)
            throw new ConflictException("New vehicle is required for vehicle transfer.");
        if (transferType.Equals("Driver", StringComparison.OrdinalIgnoreCase) && !body.NewDriverId.HasValue)
            throw new ConflictException("New driver is required for driver transfer.");

        var skipSoft = transferType.Equals("Emergency", StringComparison.OrdinalIgnoreCase);
        var validation = await AssignmentValidation.ValidateAsync(connection, tenantId,
            new ValidateAssignmentRequest(newVehicleId, newDriverId, DateTime.UtcNow, existing.AssignmentType, skipSoft),
            cancellationToken, request.AssignmentId);

        if (!validation.CanProceed)
            throw new ConflictException(string.Join(" ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message)));

        DriverAssignmentValidation.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
                  WHERE Id = @Id",
                new { Id = request.AssignmentId, ModifiedBy = currentUser.UserId?.ToString() },
                transaction: transaction, cancellationToken: cancellationToken));

            var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO AssignmentHistory
                    (TenantId, VehicleId, DriverId, AssignmentType, Status, StartAt, TransferType,
                     Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
                  VALUES
                    (@TenantId, @VehicleId, @DriverId, @AssignmentType, N'Active', GETUTCDATE(), @TransferType,
                     @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    VehicleId = newVehicleId,
                    DriverId = newDriverId,
                    AssignmentType = transferType.Equals("Temporary", StringComparison.OrdinalIgnoreCase) ? "Temporary" : "Transfer",
                    TransferType = transferType,
                    Reason = NullIfEmpty(body.Reason),
                    Notes = NullIfEmpty(body.Notes),
                    CreatedBy = currentUser.UserId?.ToString() ?? "system"
                }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
                new { No = $"ASN-{newId:D6}", Id = newId },
                transaction: transaction, cancellationToken: cancellationToken));

            await AssignmentChangelogWriter.WriteAsync(connection, tenantId, newId,
                existing.VehicleId, newVehicleId, existing.DriverId, newDriverId,
                "Transferred", body.Reason, currentUser.UserId?.ToString(), cancellationToken, transaction);

            transaction.Commit();
            return ApiResponse<int>.SuccessResponse(newId, "Assignment transferred successfully.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

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
        if (!AssignmentValidation.IsOpenStatus(existing.Status))
            throw new ConflictException("Only open assignments can be completed.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(),
              OdometerEnd = ISNULL(@OdometerEnd, OdometerEnd),
              Reason = ISNULL(@Reason, Reason), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new
            {
                Id = request.AssignmentId,
                request.Body.OdometerEnd,
                Reason = NullIfEmpty(request.Body.Reason),
                ModifiedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        await AssignmentChangelogWriter.WriteAsync(connection, tenantId, request.AssignmentId,
            existing.VehicleId, null, existing.DriverId, null,
            "Completed", request.Body.Reason, currentUser.UserId?.ToString(), cancellationToken);

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

        await AssignmentChangelogWriter.WriteAsync(connection, tenantId, request.AssignmentId,
            existing.VehicleId, null, null, null,
            "Cancelled", request.Body.Reason, currentUser.UserId?.ToString(), cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Assignment cancelled.");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

internal static class AssignmentChangelogWriter
{
    public static Task WriteAsync(System.Data.IDbConnection conn, int tenantId, int assignmentId,
        int? oldVehicleId, int? newVehicleId, int? oldDriverId, int? newDriverId,
        string action, string? reason, string? by, CancellationToken ct,
        System.Data.IDbTransaction? transaction = null)
        => conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, NewVehicleId,
              OldDriverId, NewDriverId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @OldVehicleId, @NewVehicleId,
              @OldDriverId, @NewDriverId, @ActionType, @Reason, @CreatedBy, GETUTCDATE())",
            new
            {
                TenantId = tenantId,
                AssignmentId = assignmentId,
                OldVehicleId = oldVehicleId,
                NewVehicleId = newVehicleId,
                OldDriverId = oldDriverId,
                NewDriverId = newDriverId,
                ActionType = action,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreatedBy = by
            }, transaction: transaction, cancellationToken: ct));
}
