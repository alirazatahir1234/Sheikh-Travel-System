using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for customers. SQL lives in Infrastructure.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Inserts a customer. Returns failure when CNIC already exists.
    /// </summary>
    Task<CustomerMutationResult<int>> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a customer. Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// Returns failure when CNIC conflicts with another customer.
    /// </summary>
    Task<CustomerMutationResult<bool>> UpdateAsync(int id, UpdateCustomerDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a customer. Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// </summary>
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<CustomerDto>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? recency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when customer is missing.
    /// </summary>
    Task<CustomerDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CustomerListStatsDto> GetListStatsAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default);
}

public sealed record CustomerMutationResult<T>(bool Success, T Data = default!, string? ErrorMessage = null)
{
    public static CustomerMutationResult<T> Ok(T data) => new(true, data);
    public static CustomerMutationResult<T> Fail(string message) => new(false, default!, message);
}
