using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetCurrentUserProfileQuery : IRequest<ApiResponse<UserProfileDto>>;

public class GetCurrentUserProfileQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserProfileQuery, ApiResponse<UserProfileDto>>
{
    public async Task<ApiResponse<UserProfileDto>> Handle(
        GetCurrentUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not int userId)
            return ApiResponse<UserProfileDto>.FailResponse("Not authenticated.");

        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UserQueries.UserRow>(
            new CommandDefinition(
                UserQueries.SelectSql + " WHERE u.Id = @Id AND u.IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("User", userId);

        return ApiResponse<UserProfileDto>.SuccessResponse(UserQueries.ToProfileDto(row));
    }
}

public record GetCompanyUserSummaryQuery(int? TenantId = null)
    : IRequest<ApiResponse<CompanyUserSummaryDto>>;

public class GetCompanyUserSummaryQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyUserSummaryQuery, ApiResponse<CompanyUserSummaryDto>>
{
    public async Task<ApiResponse<CompanyUserSummaryDto>> Handle(
        GetCompanyUserSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? platformScope.TenantId;
        platformScope.EnsureTenantAccess(tenantId);

        using var connection = dbFactory.CreateConnection();
        try
        {
            var summary = await UserQueries.LoadCompanyUserSummaryAsync(
                connection, tenantId, cancellationToken);
            return ApiResponse<CompanyUserSummaryDto>.SuccessResponse(summary);
        }
        catch
        {
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
            var depts = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Departments WHERE TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
            return ApiResponse<CompanyUserSummaryDto>.SuccessResponse(
                new CompanyUserSummaryDto(tenantId, total, 0, 0, 0, 0, depts));
        }
    }
}
