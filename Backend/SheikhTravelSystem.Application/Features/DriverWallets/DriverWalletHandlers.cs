using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverWallets.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverWallets;

public record GetDriverWalletPolicyQuery : IRequest<ApiResponse<DriverWalletPolicyDto>>;

public class GetDriverWalletPolicyQueryHandler
    : IRequestHandler<GetDriverWalletPolicyQuery, ApiResponse<DriverWalletPolicyDto>>
{
    public Task<ApiResponse<DriverWalletPolicyDto>> Handle(
        GetDriverWalletPolicyQuery request, CancellationToken cancellationToken)
    {
        var dto = new DriverWalletPolicyDto(
            DriverWalletPolicy.MinimumCashOutAmountPkr,
            DriverWalletPolicy.CashOutProcessingFeeRate * 100m,
            DriverWalletPolicy.CurrencyCode);
        return Task.FromResult(ApiResponse<DriverWalletPolicyDto>.SuccessResponse(dto));
    }
}

public record PreviewDriverCashOutQuery(decimal GrossAmount)
    : IRequest<ApiResponse<DriverCashOutPreviewDto>>;

public class PreviewDriverCashOutQueryHandler
    : IRequestHandler<PreviewDriverCashOutQuery, ApiResponse<DriverCashOutPreviewDto>>
{
    public Task<ApiResponse<DriverCashOutPreviewDto>> Handle(
        PreviewDriverCashOutQuery request, CancellationToken cancellationToken)
    {
        var fee = DriverWalletPolicy.CalculateFeeBreakdown(request.GrossAmount);
        return Task.FromResult(ApiResponse<DriverCashOutPreviewDto>.SuccessResponse(new DriverCashOutPreviewDto(
            fee.GrossAmount,
            fee.ProcessingFee,
            fee.NetPayout,
            DriverWalletPolicy.CashOutProcessingFeeRate * 100m,
            DriverWalletPolicy.MinimumCashOutAmountPkr,
            DriverWalletPolicy.CurrencyCode)));
    }
}

public record GetMyDriverWalletQuery : IRequest<ApiResponse<DriverWalletDto>>;

public class GetMyDriverWalletQueryHandler(
    IDriverWalletRepository walletRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetMyDriverWalletQuery, ApiResponse<DriverWalletDto>>
{
    public async Task<ApiResponse<DriverWalletDto>> Handle(
        GetMyDriverWalletQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverWalletDto>.FailResponse("Driver context required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        await walletRepository.EnsureWalletAsync(tenantId, driverId.Value, cancellationToken);
        var wallet = await walletRepository.GetWalletAsync(tenantId, driverId.Value, cancellationToken);
        return wallet is null
            ? ApiResponse<DriverWalletDto>.FailResponse("Wallet not found.")
            : ApiResponse<DriverWalletDto>.SuccessResponse(wallet);
    }
}

public record GetDriverWalletsQuery(int Page = 1, int PageSize = 20, int? DriverId = null)
    : IRequest<ApiResponse<PagedResult<DriverWalletDto>>>;

public class GetDriverWalletsQueryHandler(
    IDriverWalletRepository walletRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverWalletsQuery, ApiResponse<PagedResult<DriverWalletDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverWalletDto>>> Handle(
        GetDriverWalletsQuery request, CancellationToken cancellationToken)
    {
        var result = await walletRepository.GetWalletsPagedAsync(
            tenantContext.GetRequiredTenantId(), request.Page, request.PageSize, request.DriverId, cancellationToken);
        return ApiResponse<PagedResult<DriverWalletDto>>.SuccessResponse(result);
    }
}

public record GetDriverWalletByDriverIdQuery(int DriverId) : IRequest<ApiResponse<DriverWalletDto>>;

public class GetDriverWalletByDriverIdQueryHandler(
    IDriverWalletRepository walletRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverWalletByDriverIdQuery, ApiResponse<DriverWalletDto>>
{
    public async Task<ApiResponse<DriverWalletDto>> Handle(
        GetDriverWalletByDriverIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await walletRepository.EnsureWalletAsync(tenantId, request.DriverId, cancellationToken);
        var wallet = await walletRepository.GetWalletAsync(tenantId, request.DriverId, cancellationToken);
        return wallet is null
            ? ApiResponse<DriverWalletDto>.FailResponse("Wallet not found.")
            : ApiResponse<DriverWalletDto>.SuccessResponse(wallet);
    }
}

public record GetDriverWalletLedgerQuery(int Page = 1, int PageSize = 50, int? DriverId = null)
    : IRequest<ApiResponse<PagedResult<DriverWalletLedgerEntryDto>>>;

public class GetDriverWalletLedgerQueryHandler(
    IDriverWalletRepository walletRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverWalletLedgerQuery, ApiResponse<PagedResult<DriverWalletLedgerEntryDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverWalletLedgerEntryDto>>> Handle(
        GetDriverWalletLedgerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        int? driverId = request.DriverId;
        if (currentUser.DriverId.HasValue)
            driverId = currentUser.DriverId;

        var result = await walletRepository.GetLedgerPagedAsync(
            tenantId, driverId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<DriverWalletLedgerEntryDto>>.SuccessResponse(result);
    }
}

public record GetDriverCashOutRequestsQuery(
    int Page = 1,
    int PageSize = 50,
    int? DriverId = null,
    string? Status = null)
    : IRequest<ApiResponse<PagedResult<DriverCashOutRequestDto>>>;

public class GetDriverCashOutRequestsQueryHandler(
    IDriverWalletRepository walletRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverCashOutRequestsQuery, ApiResponse<PagedResult<DriverCashOutRequestDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverCashOutRequestDto>>> Handle(
        GetDriverCashOutRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        int? driverId = request.DriverId;
        if (currentUser.DriverId.HasValue)
            driverId = currentUser.DriverId;

        var result = await walletRepository.GetCashOutRequestsPagedAsync(
            tenantId, request.Page, request.PageSize, driverId, request.Status, cancellationToken);
        return ApiResponse<PagedResult<DriverCashOutRequestDto>>.SuccessResponse(result);
    }
}

public record UpdateMyDriverWalletPayoutDetailsCommand(UpdateDriverWalletPayoutDetailsDto Details)
    : IRequest<ApiResponse<bool>>;

public class UpdateMyDriverWalletPayoutDetailsCommandValidator
    : AbstractValidator<UpdateMyDriverWalletPayoutDetailsCommand>
{
    public UpdateMyDriverWalletPayoutDetailsCommandValidator()
    {
        RuleFor(x => x.Details.CashOutCadence)
            .Must(c => c is null or "" or "Weekly" or "Monthly")
            .WithMessage("Cash-out cadence must be Weekly or Monthly.");
    }
}

public class UpdateMyDriverWalletPayoutDetailsCommandHandler(
    IDriverWalletRepository walletRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateMyDriverWalletPayoutDetailsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateMyDriverWalletPayoutDetailsCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<bool>.FailResponse("Driver context required.");

        await walletRepository.UpdatePayoutDetailsAsync(
            tenantContext.GetRequiredTenantId(), driverId.Value, request.Details, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Payout details saved.");
    }
}

public record CreateDriverCashOutRequestCommand(CreateDriverCashOutRequestDto Request)
    : IRequest<ApiResponse<DriverCashOutRequestDto>>;

public class CreateDriverCashOutRequestCommandValidator : AbstractValidator<CreateDriverCashOutRequestCommand>
{
    private static readonly string[] AllowedMethods =
        ["BankTransfer", "JazzCash", "EasyPaisa"];

    public CreateDriverCashOutRequestCommandValidator()
    {
        RuleFor(x => x.Request.GrossAmount).GreaterThan(0);
        RuleFor(x => x.Request.Method).NotEmpty().Must(m => AllowedMethods.Contains(m, StringComparer.OrdinalIgnoreCase));
    }
}

public class CreateDriverCashOutRequestCommandHandler(
    IDriverWalletRepository walletRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<CreateDriverCashOutRequestCommand, ApiResponse<DriverCashOutRequestDto>>
{
    public async Task<ApiResponse<DriverCashOutRequestDto>> Handle(
        CreateDriverCashOutRequestCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverCashOutRequestDto>.FailResponse("Driver context required.");

        var gross = request.Request.GrossAmount;
        if (!DriverWalletPolicy.MeetsMinimumCashOut(gross))
        {
            return ApiResponse<DriverCashOutRequestDto>.FailResponse(
                $"Minimum cash-out is {DriverWalletPolicy.MinimumCashOutAmountPkr:N0} {DriverWalletPolicy.CurrencyCode}.");
        }

        var tenantId = tenantContext.GetRequiredTenantId();
        await walletRepository.EnsureWalletAsync(tenantId, driverId.Value, cancellationToken);
        var wallet = await walletRepository.GetWalletAsync(tenantId, driverId.Value, cancellationToken);
        if (wallet is null)
            return ApiResponse<DriverCashOutRequestDto>.FailResponse("Wallet not found.");

        if (wallet.Balance < gross)
            return ApiResponse<DriverCashOutRequestDto>.FailResponse("Insufficient wallet balance.");

        var destination = ResolveDestination(wallet, request.Request.Method);
        if (string.IsNullOrWhiteSpace(destination))
            return ApiResponse<DriverCashOutRequestDto>.FailResponse(
                "Add payout details for the selected method in your wallet settings first.");

        var fee = DriverWalletPolicy.CalculateFeeBreakdown(gross);
        int id;
        try
        {
            id = await walletRepository.CreateCashOutRequestAsync(
                tenantId,
                driverId.Value,
                currentUser.UserId,
                request.Request,
                fee,
                destination,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<DriverCashOutRequestDto>.FailResponse(ex.Message);
        }

        var created = await walletRepository.GetCashOutRequestAsync(tenantId, id, cancellationToken);
        return created is null
            ? ApiResponse<DriverCashOutRequestDto>.FailResponse("Cash-out request could not be loaded.")
            : ApiResponse<DriverCashOutRequestDto>.SuccessResponse(created, "Cash-out request submitted.");
    }

    private static string? ResolveDestination(DriverWalletDto wallet, string method)
    {
        var p = wallet.PayoutDetails;
        return method.ToUpperInvariant() switch
        {
            "BANKTRANSFER" => string.Join(" | ", new[]
            {
                p.BankName, p.BankAccountName, p.BankAccountNumber
            }.Where(s => !string.IsNullOrWhiteSpace(s))),
            "JAZZCASH" => p.JazzCashNumber,
            "EASYPAISA" => p.EasypaisaNumber,
            _ => null
        };
    }
}

public record RejectDriverCashOutCommand(int Id, string Reason)
    : IRequest<ApiResponse<bool>>;

public class RejectDriverCashOutCommandValidator : AbstractValidator<RejectDriverCashOutCommand>
{
    public RejectDriverCashOutCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class RejectDriverCashOutCommandHandler(
    IDriverWalletRepository walletRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<RejectDriverCashOutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        RejectDriverCashOutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await walletRepository.RejectCashOutAsync(
                tenantContext.GetRequiredTenantId(),
                request.Id,
                currentUser.UserId ?? 0,
                request.Reason,
                cancellationToken);
            return ApiResponse<bool>.SuccessResponse(true, "Cash-out rejected and balance restored.");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.FailResponse(ex.Message);
        }
    }
}

public record BulkApproveDriverCashOutsCommand(IReadOnlyList<int> Ids, string? PaymentReferencePrefix = null)
    : IRequest<ApiResponse<int>>;

public class BulkApproveDriverCashOutsCommandValidator : AbstractValidator<BulkApproveDriverCashOutsCommand>
{
    public BulkApproveDriverCashOutsCommandValidator()
        => RuleFor(x => x.Ids).NotEmpty().WithMessage("Select at least one cash-out request.");
}

public class BulkApproveDriverCashOutsCommandHandler(
    IDriverWalletRepository walletRepository,
    IDriverCashOutDisbursementService disbursementService,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<BulkApproveDriverCashOutsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        BulkApproveDriverCashOutsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var paid = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var row = await walletRepository.GetCashOutRequestAsync(tenantId, id, cancellationToken);
            if (row is null || !string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                continue;

            var disburse = await disbursementService.DisburseAsync(new DriverCashOutDisbursementRequest(
                id,
                tenantId,
                row.DriverId,
                row.NetPayout,
                row.Method,
                row.DestinationSnapshot ?? ""), cancellationToken);

            if (!disburse.Success)
                continue;

            paid += await walletRepository.BulkMarkCashOutPaidAsync(
                tenantId,
                [id],
                currentUser.UserId ?? 0,
                disburse.ProviderReference ?? request.PaymentReferencePrefix,
                cancellationToken);
        }

        return ApiResponse<int>.SuccessResponse(paid, paid > 0 ? $"{paid} cash-out(s) marked paid." : "No requests were processed.");
    }
}
