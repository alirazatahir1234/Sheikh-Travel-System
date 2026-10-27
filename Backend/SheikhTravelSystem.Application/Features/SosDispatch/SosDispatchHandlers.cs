using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.SosDispatch;

public record GetSosEventsQuery(string? Status = null, int Limit = 50)
    : IRequest<ApiResponse<List<SosEventListItemDto>>>;

public record GetSosRecoveryPartnersQuery()
    : IRequest<ApiResponse<List<SosRecoveryPartnerDto>>>;

public record UpsertSosRecoveryPartnerCommand(int? Id, UpsertSosRecoveryPartnerRequest Body)
    : IRequest<ApiResponse<int>>;

public record DeleteSosRecoveryPartnerCommand(int Id)
    : IRequest<ApiResponse<bool>>;

public record ResolveSosEventCommand(int Id)
    : IRequest<ApiResponse<bool>>;

public record GetDriverSosStatusQuery(int SosId)
    : IRequest<ApiResponse<DriverSosStatusDto>>;

public class UpsertSosRecoveryPartnerValidator : AbstractValidator<UpsertSosRecoveryPartnerCommand>
{
    public UpsertSosRecoveryPartnerValidator()
    {
        RuleFor(x => x.Body.RegionCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Body.PartnerType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body.Phone).NotEmpty().MaximumLength(40);
    }
}

public class GetSosEventsQueryHandler(ISosDispatchRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetSosEventsQuery, ApiResponse<List<SosEventListItemDto>>>
{
    public async Task<ApiResponse<List<SosEventListItemDto>>> Handle(
        GetSosEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListEventsAsync(tenantId, request.Status, request.Limit, cancellationToken);
        return ApiResponse<List<SosEventListItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetSosRecoveryPartnersQueryHandler(ISosDispatchRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetSosRecoveryPartnersQuery, ApiResponse<List<SosRecoveryPartnerDto>>>
{
    public async Task<ApiResponse<List<SosRecoveryPartnerDto>>> Handle(
        GetSosRecoveryPartnersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListPartnersAsync(tenantId, cancellationToken);
        return ApiResponse<List<SosRecoveryPartnerDto>>.SuccessResponse(rows.ToList());
    }
}

public class UpsertSosRecoveryPartnerCommandHandler(ISosDispatchRepository repository, ITenantContext tenantContext)
    : IRequestHandler<UpsertSosRecoveryPartnerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertSosRecoveryPartnerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var id = await repository.UpsertPartnerAsync(tenantId, request.Id, request.Body, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Partner saved.");
    }
}

public class DeleteSosRecoveryPartnerCommandHandler(ISosDispatchRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteSosRecoveryPartnerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteSosRecoveryPartnerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.SoftDeletePartnerAsync(tenantId, request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Partner removed.");
    }
}

public class ResolveSosEventCommandHandler(
    ISosDispatchRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ResolveSosEventCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ResolveSosEventCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString() ?? "erp";
        await repository.ResolveEventAsync(tenantId, request.Id, actor, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "SOS event resolved.");
    }
}

public class GetDriverSosStatusQueryHandler(
    ISosDispatchRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverSosStatusQuery, ApiResponse<DriverSosStatusDto>>
{
    public async Task<ApiResponse<DriverSosStatusDto>> Handle(
        GetDriverSosStatusQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverSosStatusDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await repository.GetDriverSosStatusAsync(
            tenantId, driverId.Value, request.SosId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverSosStatusDto>.FailResponse("SOS event not found.");
        return ApiResponse<DriverSosStatusDto>.SuccessResponse(row);
    }
}
