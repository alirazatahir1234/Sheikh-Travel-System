using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Platform;

public record GetSecurityCatalogQuery(bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<SecurityPolicyDefinitionDto>>>;

public record GetSecurityCompanyPoliciesQuery(int? TenantId = null)
    : IRequest<ApiResponse<IReadOnlyList<SecurityPolicyValueDto>>>;

public record UpdateSecurityCompanyPoliciesPayload(int? TenantId, IReadOnlyDictionary<string, string> Values);

public record UpdateSecurityCompanyPoliciesCommand(UpdateSecurityCompanyPoliciesPayload Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "SecurityPolicy";
    public int? AuditEntityId => Payload.TenantId;
}

public record GetMySecuritySummaryQuery : IRequest<ApiResponse<SecurityCompanySummaryDto>>;

public class GetSecurityCatalogQueryHandler(ISecurityEngine securityEngine)
    : IRequestHandler<GetSecurityCatalogQuery, ApiResponse<IReadOnlyList<SecurityPolicyDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<SecurityPolicyDefinitionDto>>> Handle(
        GetSecurityCatalogQuery request, CancellationToken cancellationToken)
    {
        var catalog = await securityEngine.GetCatalogAsync(request.ActiveOnly, cancellationToken);
        return ApiResponse<IReadOnlyList<SecurityPolicyDefinitionDto>>.SuccessResponse(catalog);
    }
}

public class GetSecurityCompanyPoliciesQueryHandler(
    ISecurityEngine securityEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetSecurityCompanyPoliciesQuery, ApiResponse<IReadOnlyList<SecurityPolicyValueDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<SecurityPolicyValueDto>>> Handle(
        GetSecurityCompanyPoliciesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);
        var policies = await securityEngine.GetCompanyPoliciesAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<SecurityPolicyValueDto>>.SuccessResponse(policies);
    }
}

public class UpdateSecurityCompanyPoliciesCommandHandler(
    ISecurityEngine securityEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSecurityCompanyPoliciesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateSecurityCompanyPoliciesCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.Payload.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);
        await securityEngine.SetCompanyPoliciesAsync(
            tenantId,
            request.Payload.Values ?? new Dictionary<string, string>(),
            currentUser.UserId,
            cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Security policies updated.");
    }
}

public class GetMySecuritySummaryQueryHandler(
    ISecurityEngine securityEngine,
    ITenantContext tenantContext,
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMySecuritySummaryQuery, ApiResponse<SecurityCompanySummaryDto>>
{
    public async Task<ApiResponse<SecurityCompanySummaryDto>> Handle(
        GetMySecuritySummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DateTime? passwordChangedAt = null;
        if (currentUser.UserId is int userId)
        {
            using var connection = dbFactory.CreateConnection();
            passwordChangedAt = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                "SELECT PasswordChangedAt FROM Users WHERE Id = @Id",
                new { Id = userId },
                cancellationToken: cancellationToken));
        }

        var summary = await securityEngine.GetSafeSummaryAsync(tenantId, passwordChangedAt, cancellationToken);
        return ApiResponse<SecurityCompanySummaryDto>.SuccessResponse(summary);
    }
}
