using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Application.Features.Rentals.Commands;

public record CreateRentalRateCommand(CreateRentalRateDto Rate)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "RentalRate";
    public int? AuditEntityId => null;
}

public class CreateRentalRateCommandValidator : AbstractValidator<CreateRentalRateCommand>
{
    public CreateRentalRateCommandValidator()
    {
        RuleFor(x => x.Rate.RateType).Must(RentalRateTypes.IsValid).WithMessage("Rate type must be Hourly, Daily, Weekly, or Monthly.");
        RuleFor(x => x.Rate.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rate.DepositAmount).GreaterThanOrEqualTo(0);
    }
}

public class CreateRentalRateCommandHandler(
    IRentalRepository rentalRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateRentalRateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRentalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (request.Rate.VehicleId is int vehicleId
            && !await rentalRepository.VehicleExistsAsync(vehicleId, tenantId, cancellationToken))
            throw new NotFoundException("Vehicle", vehicleId);

        var id = await rentalRepository.CreateRateAsync(tenantId, request.Rate, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Rental rate created.");
    }
}

public record UpdateRentalRateCommand(int Id, UpdateRentalRateDto Rate)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "RentalRate";
    public int? AuditEntityId => Id;
}

public class UpdateRentalRateCommandValidator : AbstractValidator<UpdateRentalRateCommand>
{
    public UpdateRentalRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Rate.RateType).Must(RentalRateTypes.IsValid);
        RuleFor(x => x.Rate.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rate.DepositAmount).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRentalRateCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateRentalRateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRentalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (await rentalRepository.GetRateByIdAsync(request.Id, tenantId, cancellationToken) is null)
            throw new NotFoundException("RentalRate", request.Id);

        var ok = await rentalRepository.UpdateRateAsync(request.Id, tenantId, request.Rate, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(ok, ok ? "Rental rate updated." : "No changes.");
    }
}

public record DeleteRentalRateCommand(int Id)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "RentalRate";
    public int? AuditEntityId => Id;
}

public class DeleteRentalRateCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<DeleteRentalRateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteRentalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await rentalRepository.SoftDeleteRateAsync(request.Id, tenantId, cancellationToken))
            throw new NotFoundException("RentalRate", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Rental rate deleted.");
    }
}
