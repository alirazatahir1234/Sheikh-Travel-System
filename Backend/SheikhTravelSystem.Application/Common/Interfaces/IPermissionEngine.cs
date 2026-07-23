using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public record EffectivePermissionDto(
    string Code,
    string DisplayName,
    string? Category,
    string? ModuleKey,
    string Action,
    IReadOnlyList<string>? SourceRoleCodes = null);

public record PermissionEvaluationResult(
    int UserId,
    int TenantId,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<EffectivePermissionDto> EffectivePermissions);

public interface IPermissionEngine
{
    /// <summary>
    /// Resolves role codes + effective permission codes (after soft module/feature gates).
    /// </summary>
    Task<PermissionEvaluationResult> EvaluateAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default);
}
