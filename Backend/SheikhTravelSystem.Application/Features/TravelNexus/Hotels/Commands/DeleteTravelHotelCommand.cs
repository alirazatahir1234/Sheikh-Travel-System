using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Commands;

/// <summary>Soft-deletes a catalog hotel (existing packages and groups keep their reference).</summary>
public record DeleteTravelHotelCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelHotel";
    public int? AuditEntityId => Id;
}

public class DeleteTravelHotelCommandValidator : AbstractValidator<DeleteTravelHotelCommand>
{
    public DeleteTravelHotelCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelHotelCommandHandler(ITravelHotelRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelHotelCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelHotelCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken))
            throw new NotFoundException("TravelHotel", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Hotel deleted.");
    }
}
