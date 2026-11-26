using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;

/// <summary>
/// Creates (Id null, Draft) or updates a package with its default hotel stays and prices. Archived packages are
/// read-only. Groups copy stays and quoted prices when created/registered, so later edits do not change them.
/// </summary>
public record SaveTravelPackageCommand(int? Id, SaveTravelPackageDto Package) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelPackage";
    public int? AuditEntityId => Id;
}

public class SaveTravelPackageCommandValidator : AbstractValidator<SaveTravelPackageCommand>
{
    public SaveTravelPackageCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.Package).NotNull();
        RuleFor(x => x.Package.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9-]+$").WithMessage("Code may contain letters, digits and '-'.");
        RuleFor(x => x.Package.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Package.PackageType).Must(t => TravelValues.IsOneOf(t, TravelValues.PackageTypes)).WithMessage("Package type must be Umrah or Hajj.");
        RuleFor(x => x.Package.DurationNights).InclusiveBetween(1, 90);
        RuleFor(x => x.Package.NightsMakkah).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Package.NightsMadinah).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Package).Must(p => p.NightsMakkah + p.NightsMadinah <= p.DurationNights)
            .WithName("NightsMakkah").WithMessage("Makkah + Madinah nights cannot exceed the package duration.");
        RuleFor(x => x.Package.CurrencyCode).Must(TravelValues.IsCurrencyCode).WithMessage("Currency must be an ISO 4217 code (e.g. PKR, SAR).");
        RuleFor(x => x.Package.Inclusions).MaximumLength(2000);
        RuleFor(x => x.Package.Exclusions).MaximumLength(2000);
        RuleFor(x => x.Package.Stays).NotNull().Must(s => s.Count <= 10).WithMessage("At most 10 hotel stays.");
        RuleForEach(x => x.Package.Stays).ChildRules(s =>
        {
            s.RuleFor(i => i.HotelId).GreaterThan(0);
            s.RuleFor(i => i.Nights).InclusiveBetween(1, 90);
        });
        RuleFor(x => x.Package).Must(p => p.Stays.Sum(s => s.Nights) <= p.DurationNights)
            .WithName("Stays").WithMessage("Hotel nights cannot exceed the package duration.");
        RuleFor(x => x.Package.Prices).NotNull()
            .Must(p => p.Select(i => i.RoomType).Distinct().Count() == p.Count).WithMessage("Each room type can be priced once.");
        RuleForEach(x => x.Package.Prices).ChildRules(p =>
        {
            p.RuleFor(i => i.RoomType).Must(t => TravelValues.IsOneOf(t, TravelValues.RoomTypes)).WithMessage("Unknown room type.");
            p.RuleFor(i => i.AdultPrice).GreaterThanOrEqualTo(0);
            p.RuleFor(i => i.ChildPrice).GreaterThanOrEqualTo(0);
            p.RuleFor(i => i.InfantPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public class SaveTravelPackageCommandHandler(ITravelPackageRepository repository, ITravelHotelRepository hotels, ITravelConfigRepository config,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelPackageCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelPackageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Package with
        {
            Code = request.Package.Code.Trim().ToUpperInvariant(),
            Name = request.Package.Name.Trim(),
            CurrencyCode = request.Package.CurrencyCode.ToUpperInvariant()
        };

        if (request.Id is int existingId)
        {
            var existing = await repository.GetByIdAsync(existingId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPackage", existingId);
            if (existing.Status == "Archived") return ApiResponse<int>.FailResponse("Archived packages cannot be edited.");
        }

        if (await repository.CodeExistsAsync(tenantId, dto.Code, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("Another package already uses this code.");
        if (dto.VisaTypeId is int visaTypeId && await config.GetVisaTypeByIdAsync(visaTypeId, tenantId, cancellationToken) is null)
            return ApiResponse<int>.FailResponse("Visa type not found.");
        foreach (var hotelId in dto.Stays.Select(s => s.HotelId).Distinct())
        {
            var hotel = await hotels.GetByIdAsync(hotelId, tenantId, cancellationToken);
            if (hotel is null || !hotel.IsActive) return ApiResponse<int>.FailResponse("Every stay needs an active hotel from the catalog.");
        }

        if (request.Id is int id)
        {
            if (!await repository.UpdateAsync(id, tenantId, dto, cancellationToken))
                throw new ConflictException("The package was changed by someone else. Reload and try again.");
            return ApiResponse<int>.SuccessResponse(id, "Package saved.");
        }
        return ApiResponse<int>.SuccessResponse(await repository.CreateAsync(tenantId, dto, currentUser.UserId, cancellationToken), "Package created as draft.");
    }
}
