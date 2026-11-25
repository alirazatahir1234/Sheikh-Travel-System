using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Commands;

/// <summary>Creates (Id null) or updates a catalog hotel.</summary>
public record SaveTravelHotelCommand(int? Id, SaveTravelHotelDto Hotel) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelHotel";
    public int? AuditEntityId => Id;
}

public class SaveTravelHotelCommandValidator : AbstractValidator<SaveTravelHotelCommand>
{
    public SaveTravelHotelCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.Hotel).NotNull();
        RuleFor(x => x.Hotel.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Hotel.City).Must(c => TravelValues.IsOneOf(c, TravelValues.HotelCities)).WithMessage("Unknown city.");
        RuleFor(x => x.Hotel.CountryCode).Must(TravelValues.IsCountryCode).WithMessage("Country must be an ISO alpha-2 code.");
        RuleFor(x => x.Hotel.StarRating).InclusiveBetween(1, 5).When(x => x.Hotel.StarRating.HasValue);
        RuleFor(x => x.Hotel.DistanceToHaramMeters).InclusiveBetween(0, 100_000).When(x => x.Hotel.DistanceToHaramMeters.HasValue);
        RuleFor(x => x.Hotel.Address).MaximumLength(300);
        RuleFor(x => x.Hotel.Phone).MaximumLength(30);
    }
}

public class SaveTravelHotelCommandHandler(ITravelHotelRepository repository, ITravelConfigRepository config,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelHotelCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelHotelCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Hotel with { Name = request.Hotel.Name.Trim() };
        if (!await config.CountryExistsAsync(dto.CountryCode, cancellationToken)) return ApiResponse<int>.FailResponse("Unknown country.");
        if (await repository.NameExistsAsync(tenantId, dto.Name, dto.City, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A hotel with this name already exists in this city.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateAsync(id, tenantId, dto, cancellationToken)) throw new NotFoundException("TravelHotel", id);
            return ApiResponse<int>.SuccessResponse(id, "Hotel updated.");
        }
        return ApiResponse<int>.SuccessResponse(await repository.CreateAsync(tenantId, dto, currentUser.UserId, cancellationToken), "Hotel added.");
    }
}
