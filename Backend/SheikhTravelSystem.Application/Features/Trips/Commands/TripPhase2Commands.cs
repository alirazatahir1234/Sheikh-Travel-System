using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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

public class AddTripExpenseCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AddTripExpenseCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddTripExpenseCommand request, CancellationToken cancellationToken)
    {
        var id = await tripRepository.AddExpenseAsync(request.TripId, request.Expense, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Expense added.");
    }
}

public record DeleteTripExpenseCommand(int TripId, int ExpenseId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripExpense";
    public int? AuditEntityId => ExpenseId;
}

public class DeleteTripExpenseCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DeleteTripExpenseCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripExpenseCommand request, CancellationToken cancellationToken)
    {
        await tripRepository.SoftDeleteExpenseAsync(request.TripId, request.ExpenseId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Expense deleted.");
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

public class AddTripPassengerCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AddTripPassengerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddTripPassengerCommand request, CancellationToken cancellationToken)
    {
        var id = await tripRepository.AddPassengerAsync(request.TripId, request.Passenger, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Passenger added.");
    }
}

public record UpdateTripPassengerCommand(int TripId, int PassengerId, UpdateTripPassengerDto Passenger) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TripPassenger";
    public int? AuditEntityId => PassengerId;
}

public class UpdateTripPassengerCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<UpdateTripPassengerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripPassengerCommand request, CancellationToken cancellationToken)
    {
        await tripRepository.UpdatePassengerAsync(request.TripId, request.PassengerId, request.Passenger, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Passenger updated.");
    }
}

public record DeleteTripPassengerCommand(int TripId, int PassengerId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripPassenger";
    public int? AuditEntityId => PassengerId;
}

public class DeleteTripPassengerCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DeleteTripPassengerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripPassengerCommand request, CancellationToken cancellationToken)
    {
        await tripRepository.SoftDeletePassengerAsync(request.TripId, request.PassengerId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Passenger removed.");
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
    ITripRepository tripRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadTripDocumentCommand, ApiResponse<TripDocumentDto>>
{
    public async Task<ApiResponse<TripDocumentDto>> Handle(UploadTripDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await tripRepository.EnsureTripExistsAsync(request.TripId, cancellationToken);

        await using var bounded = new MaxLengthReadStream(request.FileStream, 10 * 1024 * 1024);
        var stored = await fileStorage.SaveAsync(
            bounded,
            request.FileName,
            request.ContentType,
            $"trips/{tenantId}/{request.TripId}",
            cancellationToken);

        var uploadedBy = currentUser.UserId?.ToString();
        var id = await tripRepository.AddDocumentAsync(
            request.TripId,
            request.DocumentType,
            request.FileName,
            stored.StorageKey,
            uploadedBy,
            cancellationToken);

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

public class DeleteTripDocumentCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DeleteTripDocumentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripDocumentCommand request, CancellationToken cancellationToken)
    {
        await tripRepository.SoftDeleteDocumentAsync(request.TripId, request.DocumentId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Document deleted.");
    }
}
