using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Commands;

/// <summary>Creates (Id null) or updates a supplier profile.</summary>
public record SaveTravelSupplierCommand(int? Id, SaveTravelSupplierDto Supplier) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => Id;
}

public class SaveTravelSupplierCommandValidator : AbstractValidator<SaveTravelSupplierCommand>
{
    public SaveTravelSupplierCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.Supplier).NotNull();
        RuleFor(x => x.Supplier.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Supplier.SupplierType).Must(t => TravelValues.IsOneOf(t, TravelValues.SupplierTypes)).WithMessage("Unknown supplier type.");
        RuleFor(x => x.Supplier.IataCode).Matches("^[A-Z0-9]{2,3}$").When(x => !string.IsNullOrEmpty(x.Supplier.IataCode))
            .WithMessage("IATA code must be 2 (airline) or 3 characters.");
        RuleFor(x => x.Supplier.CurrencyCode).Must(TravelValues.IsCurrencyCode).When(x => !string.IsNullOrEmpty(x.Supplier.CurrencyCode))
            .WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Supplier.ContactName).MaximumLength(150);
        RuleFor(x => x.Supplier.Phone).MaximumLength(30);
        RuleFor(x => x.Supplier.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Supplier.Email));
        RuleFor(x => x.Supplier.Notes).MaximumLength(1000);
    }
}

public class SaveTravelSupplierCommandHandler(ITravelSupplierRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelSupplierCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelSupplierCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        var dto = request.Supplier with
        {
            Name = request.Supplier.Name.Trim(), IataCode = Clean(request.Supplier.IataCode)?.ToUpperInvariant(),
            CurrencyCode = Clean(request.Supplier.CurrencyCode)?.ToUpperInvariant(), ContactName = Clean(request.Supplier.ContactName),
            Phone = Clean(request.Supplier.Phone), Email = Clean(request.Supplier.Email), Notes = Clean(request.Supplier.Notes)
        };
        if (await repository.NameExistsAsync(tenantId, dto.Name, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A supplier with this name already exists.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateAsync(id, tenantId, dto, cancellationToken)) throw new NotFoundException("TravelSupplier", id);
            return ApiResponse<int>.SuccessResponse(id, "Supplier updated.");
        }
        return ApiResponse<int>.SuccessResponse(await repository.CreateAsync(tenantId, dto, currentUser.UserId, cancellationToken), "Supplier added.");
    }
}

/// <summary>Soft-deletes a supplier that no booking references; otherwise deactivate it.</summary>
public record DeleteTravelSupplierCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => Id;
}

public class DeleteTravelSupplierCommandValidator : AbstractValidator<DeleteTravelSupplierCommand>
{
    public DeleteTravelSupplierCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelSupplierCommandHandler(ITravelSupplierRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelSupplierCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelSupplierCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var supplier = await repository.GetByIdAsync(request.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", request.Id);
        if (supplier.UsageCount > 0) return ApiResponse<bool>.FailResponse("Bookings use this supplier — deactivate it instead.");
        if (!await repository.DeleteAsync(request.Id, tenantId, cancellationToken)) throw new NotFoundException("TravelSupplier", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Supplier deleted.");
    }
}
