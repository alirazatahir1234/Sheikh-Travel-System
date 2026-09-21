using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    int? TenantId = null,
    int? BranchId = null,
    int? DepartmentId = null,
    string? Status = null,
    string? EmployeeType = null,
    string? Search = null)
    : IRequest<ApiResponse<PagedResult<UserDto>>>;

public class GetUsersQueryHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope) : IRequestHandler<GetUsersQuery, ApiResponse<PagedResult<UserDto>>>
{
    public async Task<ApiResponse<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        int tenantId;
        try
        {
            tenantId = ResolveTenantFilter(request.TenantId);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException(
                "Company context is missing. Sign in again or select a company (tenant) before loading users.");
        }

        var (items, totalCount) = await userRepository.GetPagedAsync(
            tenantId,
            request.Page,
            request.PageSize,
            request.BranchId,
            request.DepartmentId,
            request.Status,
            request.EmployeeType,
            request.Search,
            cancellationToken);

        return ApiResponse<PagedResult<UserDto>>.SuccessResponse(new PagedResult<UserDto>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private int ResolveTenantFilter(int? requestedTenantId)
    {
        if (requestedTenantId.HasValue)
        {
            platformScope.EnsureTenantAccess(requestedTenantId.Value);
            return requestedTenantId.Value;
        }

        return platformScope.TenantId;
    }
}
