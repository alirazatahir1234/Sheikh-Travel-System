using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.IO;
using SheikhTravelSystem.Application.Features.Trips.DTOs;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record AddTripExpenseCommand(int TripId, CreateTripExpenseDto Expense) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TripExpense";
    public int? AuditEntityId => null;
}

public class AddTripExpenseCommandValidator : AbstractValidator<AddTripExpenseCommand>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { "Fuel", "Toll", "Parking", "Food", "Hotel", "Other" };

    public AddTripExpenseCommandValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.Expense.ExpenseType).NotEmpty().Must(Allowed.Contains)
            .WithMessage("Expense type must be Fuel, Toll, Parking, Food, Hotel, or Other.");
        RuleFor(x => x.Expense.Amount).GreaterThan(0);
    }
}

public class AddTripExpenseCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AddTripExpenseCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddTripExpenseCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        await EnsureTripAsync(connection, request.TripId, tenantId, cancellationToken);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripExpenses (TripId, ExpenseType, Amount, Description, ExpenseDate, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@TripId, @ExpenseType, @Amount, @Description, @ExpenseDate, GETUTCDATE(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                request.TripId,
                request.Expense.ExpenseType,
                request.Expense.Amount,
                request.Expense.Description,
                ExpenseDate = request.Expense.ExpenseDate ?? DateTime.UtcNow,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Expense added.");
    }

    internal static async Task EnsureTripAsync(System.Data.IDbConnection connection, int tripId, int tenantId, CancellationToken ct)
    {
        var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = tripId, TenantId = tenantId },
            cancellationToken: ct));
        if (!ok) throw new NotFoundException("Trip", tripId);
    }
}

public record DeleteTripExpenseCommand(int TripId, int ExpenseId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripExpense";
    public int? AuditEntityId => ExpenseId;
}

public class DeleteTripExpenseCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteTripExpenseCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripExpenseCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantContext.GetRequiredTenantId(), cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripExpenses SET IsDeleted = 1 WHERE Id = @ExpenseId AND TripId = @TripId AND IsDeleted = 0",
            new { request.ExpenseId, request.TripId },
            cancellationToken: cancellationToken));
        return ApiResponse<bool>.SuccessResponse(rows > 0, "Expense deleted.");
    }
}

public record AddTripPassengerCommand(int TripId, CreateTripPassengerDto Passenger) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TripPassenger";
    public int? AuditEntityId => null;
}

public class AddTripPassengerCommandValidator : AbstractValidator<AddTripPassengerCommand>
{
    public AddTripPassengerCommandValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.Passenger.FullName).NotEmpty().MaximumLength(200);
    }
}

public class AddTripPassengerCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<AddTripPassengerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddTripPassengerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantId, cancellationToken);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripPassengers (TripId, FullName, Phone, BoardingStatus, DropStatus, Notes, CreatedAt, IsDeleted)
            VALUES (@TripId, @FullName, @Phone, N'Pending', N'Pending', @Notes, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                request.TripId,
                request.Passenger.FullName,
                request.Passenger.Phone,
                request.Passenger.Notes
            },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET PassengerCount = (
                SELECT COUNT(*) FROM TripPassengers WHERE TripId = @TripId AND IsDeleted = 0
            ), UpdatedAt = GETUTCDATE()
            WHERE Id = @TripId
            """, new { request.TripId }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Passenger added.");
    }
}

public record UpdateTripPassengerCommand(int TripId, int PassengerId, UpdateTripPassengerDto Passenger) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TripPassenger";
    public int? AuditEntityId => PassengerId;
}

public class UpdateTripPassengerCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateTripPassengerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripPassengerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantContext.GetRequiredTenantId(), cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripPassengers SET
                FullName = @FullName, Phone = @Phone,
                BoardingStatus = @BoardingStatus, DropStatus = @DropStatus,
                Notes = @Notes, UpdatedAt = GETUTCDATE()
            WHERE Id = @PassengerId AND TripId = @TripId AND IsDeleted = 0
            """,
            new
            {
                request.PassengerId,
                request.TripId,
                request.Passenger.FullName,
                request.Passenger.Phone,
                request.Passenger.BoardingStatus,
                request.Passenger.DropStatus,
                request.Passenger.Notes
            },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(rows > 0, "Passenger updated.");
    }
}

public record DeleteTripPassengerCommand(int TripId, int PassengerId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripPassenger";
    public int? AuditEntityId => PassengerId;
}

public class DeleteTripPassengerCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteTripPassengerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripPassengerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantContext.GetRequiredTenantId(), cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripPassengers SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @PassengerId AND TripId = @TripId AND IsDeleted = 0",
            new { request.PassengerId, request.TripId },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET PassengerCount = (
                SELECT COUNT(*) FROM TripPassengers WHERE TripId = @TripId AND IsDeleted = 0
            ), UpdatedAt = GETUTCDATE()
            WHERE Id = @TripId
            """, new { request.TripId }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(rows > 0, "Passenger removed.");
    }
}

public record UploadTripDocumentCommand(
    int TripId,
    Stream FileStream,
    string FileName,
    string ContentType,
    string DocumentType,
    long FileLength) : IRequest<ApiResponse<TripDocumentDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TripDocument";
    public int? AuditEntityId => null;
}

public class UploadTripDocumentCommandValidator : AbstractValidator<UploadTripDocumentCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "TripSheet", "Invoice", "DeliveryNote", "CustomerSignature", "VehiclePhoto", "Other" };

    public UploadTripDocumentCommandValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.DocumentType).NotEmpty().Must(AllowedTypes.Contains);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileLength).GreaterThan(0).LessThanOrEqualTo(10 * 1024 * 1024);
    }
}

public class UploadTripDocumentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadTripDocumentCommand, ApiResponse<TripDocumentDto>>
{
    public async Task<ApiResponse<TripDocumentDto>> Handle(UploadTripDocumentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantId, cancellationToken);

        await using var bounded = new MaxLengthReadStream(request.FileStream, 10 * 1024 * 1024);
        var stored = await fileStorage.SaveAsync(
            bounded,
            request.FileName,
            request.ContentType,
            $"trips/{tenantId}/{request.TripId}",
            cancellationToken);

        var uploadedBy = currentUser.UserId?.ToString();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripDocuments (TripId, DocumentType, FileName, StorageKey, UploadedBy, CreatedAt, IsDeleted)
            VALUES (@TripId, @DocumentType, @FileName, @StorageKey, @UploadedBy, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                request.TripId,
                request.DocumentType,
                request.FileName,
                StorageKey = stored.StorageKey,
                UploadedBy = uploadedBy
            },
            cancellationToken: cancellationToken));

        return ApiResponse<TripDocumentDto>.SuccessResponse(new TripDocumentDto(
            id, request.DocumentType, request.FileName, stored.ReadUrl, uploadedBy, DateTime.UtcNow),
            "Document uploaded.");
    }
}

public record DeleteTripDocumentCommand(int TripId, int DocumentId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripDocument";
    public int? AuditEntityId => DocumentId;
}

public class DeleteTripDocumentCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteTripDocumentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripDocumentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await AddTripExpenseCommandHandler.EnsureTripAsync(connection, request.TripId, tenantContext.GetRequiredTenantId(), cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripDocuments SET IsDeleted = 1 WHERE Id = @DocumentId AND TripId = @TripId AND IsDeleted = 0",
            new { request.DocumentId, request.TripId },
            cancellationToken: cancellationToken));
        return ApiResponse<bool>.SuccessResponse(rows > 0, "Document deleted.");
    }
}
