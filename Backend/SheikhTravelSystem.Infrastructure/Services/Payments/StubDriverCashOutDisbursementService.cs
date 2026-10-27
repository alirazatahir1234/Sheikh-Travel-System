using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

/// <summary>
/// Console/stub disbursement until JazzCash/Easypaisa payout credentials are configured.
/// </summary>
public sealed class StubDriverCashOutDisbursementService(ILogger<StubDriverCashOutDisbursementService> logger)
    : IDriverCashOutDisbursementService
{
    public Task<DriverCashOutDisbursementResult> DisburseAsync(
        DriverCashOutDisbursementRequest request,
        CancellationToken cancellationToken = default)
    {
        var reference = $"STUB-{request.Method.ToUpperInvariant()}-{request.CashOutRequestId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        logger.LogInformation(
            "Stub driver cash-out disbursement: RequestId={RequestId} DriverId={DriverId} Net={Net:N2} Method={Method} Dest={Dest} Ref={Ref}",
            request.CashOutRequestId,
            request.DriverId,
            request.NetPayout,
            request.Method,
            request.DestinationSnapshot,
            reference);

        return Task.FromResult(new DriverCashOutDisbursementResult(true, reference, "Stub payout recorded (configure gateway for live transfer)."));
    }
}
