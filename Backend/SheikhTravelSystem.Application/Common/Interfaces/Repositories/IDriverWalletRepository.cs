using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.DriverWallets.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IDriverWalletRepository
{
    Task EnsureWalletAsync(int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<DriverWalletDto?> GetWalletAsync(int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<PagedResult<DriverWalletDto>> GetWalletsPagedAsync(
        int tenantId, int page, int pageSize, int? driverId, CancellationToken cancellationToken = default);

    Task UpdatePayoutDetailsAsync(
        int tenantId,
        int driverId,
        UpdateDriverWalletPayoutDetailsDto details,
        CancellationToken cancellationToken = default);

    Task<bool> TryCreditAsync(
        int tenantId,
        int driverId,
        decimal amount,
        string entryType,
        string referenceType,
        int referenceId,
        string description,
        int? createdByUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DriverWalletLedgerEntryDto>> GetLedgerPagedAsync(
        int tenantId,
        int? driverId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DriverCashOutRequestDto>> GetCashOutRequestsPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        int? driverId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<int> CreateCashOutRequestAsync(
        int tenantId,
        int driverId,
        int? requestedByUserId,
        CreateDriverCashOutRequestDto request,
        CashOutFeeBreakdown fee,
        string destinationSnapshot,
        CancellationToken cancellationToken = default);

    Task<DriverCashOutRequestDto?> GetCashOutRequestAsync(
        int tenantId, int id, CancellationToken cancellationToken = default);

    Task RejectCashOutAsync(
        int tenantId, int id, int processedByUserId, string reason, CancellationToken cancellationToken = default);

    Task<int> BulkMarkCashOutPaidAsync(
        int tenantId,
        IReadOnlyList<int> ids,
        int processedByUserId,
        string? paymentReferencePrefix,
        CancellationToken cancellationToken = default);
}
