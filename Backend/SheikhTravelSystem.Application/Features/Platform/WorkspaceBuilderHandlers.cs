using System.Data;
using System.Text.Json;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Platform;

public record WorkspaceDefinitionDto(
    string WorkspaceKey,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    string HomeRoute,
    int SortOrder,
    bool Visible,
    bool IsActive,
    bool IsMobileSupported,
    IReadOnlyList<string> ModuleKeys,
    string? FeatureKey,
    string? DefaultDashboardKey,
    bool IsEnabled = true,
    bool CanToggle = true);

public record CompanyWorkspaceDto(
    string WorkspaceKey,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    string HomeRoute,
    bool IsEnabled,
    bool IsMobileSupported,
    IReadOnlyList<string> ModuleKeys,
    string? DefaultDashboardKey,
    bool CanToggle);

public record ResolvedWorkspaceDto(
    string Key,
    string DisplayName,
    string HomeRoute,
    string? Icon,
    string? DefaultDashboardKey,
    string Source,
    IReadOnlyList<string> ModuleKeys,
    bool IsMobileSupported = false);

public record UpdateWorkspaceDefinitionPayload(
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    string HomeRoute,
    int SortOrder,
    bool Visible,
    bool IsActive,
    bool IsMobileSupported,
    IReadOnlyList<string>? ModuleKeys,
    string? FeatureKey,
    string? DefaultDashboardKey);

public record CreateWorkspaceDefinitionPayload(
    string WorkspaceKey,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    string HomeRoute,
    int SortOrder,
    bool Visible,
    bool IsMobileSupported,
    IReadOnlyList<string>? ModuleKeys,
    string? FeatureKey,
    string? DefaultDashboardKey);

public record GetWorkspaceCatalogQuery : IRequest<ApiResponse<IReadOnlyList<WorkspaceDefinitionDto>>>;
public record GetCompanyWorkspacesQuery(int? TenantId = null) : IRequest<ApiResponse<IReadOnlyList<CompanyWorkspaceDto>>>;
public record GetMyWorkspaceQuery : IRequest<ApiResponse<ResolvedWorkspaceDto>>;
public record SetCompanyWorkspacesCommand(int TenantId, IReadOnlyList<string> EnabledWorkspaceKeys)
    : IRequest<ApiResponse<bool>>;
public record UpdateWorkspaceDefinitionCommand(string Key, UpdateWorkspaceDefinitionPayload Payload)
    : IRequest<ApiResponse<bool>>;
public record CreateWorkspaceDefinitionCommand(CreateWorkspaceDefinitionPayload Payload)
    : IRequest<ApiResponse<string>>;
public record DeactivateWorkspaceDefinitionCommand(string Key) : IRequest<ApiResponse<bool>>;

public static class WorkspaceBuilderQueries
{
    public sealed record WorkspaceRow(
        string WorkspaceKey,
        string DisplayName,
        string? Description,
        string? Category,
        string? Icon,
        string HomeRoute,
        int SortOrder,
        bool Visible,
        bool IsActive,
        bool IsMobileSupported,
        string? ModuleKeysJson,
        string? FeatureKey,
        string? DefaultDashboardKey);

    public static IReadOnlyList<string> ParseModuleKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string? SerializeModuleKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0) return null;
        var cleaned = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    public static async Task<IReadOnlyList<WorkspaceRow>> LoadCatalogAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        bool activeOnly = false)
    {
        try
        {
            var rows = await connection.QueryAsync<WorkspaceRow>(new CommandDefinition("""
                SELECT WorkspaceKey, DisplayName, Description, Category, Icon, HomeRoute, SortOrder,
                       Visible, IsActive, IsMobileSupported, ModuleKeysJson, FeatureKey, DefaultDashboardKey
                FROM WorkspaceDefinitions
                WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1))
                ORDER BY SortOrder, DisplayName
                """,
                new { ActiveOnly = activeOnly ? 1 : 0 },
                cancellationToken: cancellationToken));
            return rows.ToList();
        }
        catch
        {
            return Array.Empty<WorkspaceRow>();
        }
    }

    public static async Task<Dictionary<string, bool>> LoadTenantFlagsAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await connection.QueryAsync<(string WorkspaceKey, bool IsEnabled)>(
                new CommandDefinition("""
                    SELECT WorkspaceKey, IsEnabled FROM TenantWorkspaces WHERE TenantId = @TenantId
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
            return rows.ToDictionary(r => r.WorkspaceKey, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool IsCompanyEnabled(
        string workspaceKey,
        IReadOnlyDictionary<string, bool> flags)
    {
        if (flags.Count == 0) return true; // soft: no rows → all enabled
        if (!flags.TryGetValue(workspaceKey, out var enabled)) return true; // unmapped → pass
        return enabled;
    }

    public static string RoleHint(string? roleCode)
    {
        var code = (roleCode ?? string.Empty).ToUpperInvariant();
        return code switch
        {
            "SUPER_ADMIN" => "platform",
            "TENANT_ADMIN" => "company",
            "FLEET_MANAGER" => "fleet",
            "DRIVER_MANAGER" => "drivers",
            "DISPATCHER" => "trips",
            "GPS_OPERATOR" => "fleet",
            "ACCOUNTANT" => "finance",
            "DRIVER" => "driver",
            _ => "home"
        };
    }

    public static ResolvedWorkspaceDto Resolve(
        IReadOnlyList<WorkspaceRow> catalog,
        IReadOnlyDictionary<string, bool> flags,
        string? userDefaultKey,
        string? userHomeRoute,
        string? roleCode)
    {
        WorkspaceRow? Pick(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var row = catalog.FirstOrDefault(c =>
                c.IsActive && c.Visible &&
                string.Equals(c.WorkspaceKey, key, StringComparison.OrdinalIgnoreCase));
            if (row is null) return null;
            if (!IsCompanyEnabled(row.WorkspaceKey, flags)) return null;
            return row;
        }

        var source = "default";
        var selected = Pick(userDefaultKey);
        if (selected is not null)
            source = "user";
        else
        {
            selected = Pick(RoleHint(roleCode));
            if (selected is not null)
                source = "role";
            else
            {
                selected = Pick("home") ?? catalog.FirstOrDefault(c => c.IsActive && c.Visible && IsCompanyEnabled(c.WorkspaceKey, flags));
                source = "default";
            }
        }

        if (selected is null)
        {
            return new ResolvedWorkspaceDto(
                "home",
                "Home",
                string.IsNullOrWhiteSpace(userHomeRoute) ? "/dashboard" : userHomeRoute!,
                "home",
                null,
                source,
                Array.Empty<string>());
        }

        var home = !string.IsNullOrWhiteSpace(userHomeRoute) && source == "user"
            ? userHomeRoute!
            : selected.HomeRoute;

        return new ResolvedWorkspaceDto(
            selected.WorkspaceKey,
            selected.DisplayName,
            home,
            selected.Icon,
            selected.DefaultDashboardKey,
            source,
            ParseModuleKeys(selected.ModuleKeysJson),
            selected.IsMobileSupported);
    }

    public static WorkspaceDefinitionDto ToDefinitionDto(WorkspaceRow row, bool isEnabled = true)
        => new(
            row.WorkspaceKey,
            row.DisplayName,
            row.Description,
            row.Category,
            row.Icon,
            row.HomeRoute,
            row.SortOrder,
            row.Visible,
            row.IsActive,
            row.IsMobileSupported,
            ParseModuleKeys(row.ModuleKeysJson),
            row.FeatureKey,
            row.DefaultDashboardKey,
            isEnabled,
            CanToggle: true);

    public static CompanyWorkspaceDto ToCompanyDto(WorkspaceRow row, bool isEnabled)
        => new(
            row.WorkspaceKey,
            row.DisplayName,
            row.Description,
            row.Category,
            row.Icon,
            row.HomeRoute,
            isEnabled,
            row.IsMobileSupported,
            ParseModuleKeys(row.ModuleKeysJson),
            row.DefaultDashboardKey,
            CanToggle: row.IsActive && row.Visible);
}

public class GetWorkspaceCatalogQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetWorkspaceCatalogQuery, ApiResponse<IReadOnlyList<WorkspaceDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WorkspaceDefinitionDto>>> Handle(
        GetWorkspaceCatalogQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await WorkspaceBuilderQueries.LoadCatalogAsync(connection, cancellationToken);
        var dtos = rows.Select(r => WorkspaceBuilderQueries.ToDefinitionDto(r)).ToList();
        return ApiResponse<IReadOnlyList<WorkspaceDefinitionDto>>.SuccessResponse(dtos);
    }
}

public class GetCompanyWorkspacesQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyWorkspacesQuery, ApiResponse<IReadOnlyList<CompanyWorkspaceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<CompanyWorkspaceDto>>> Handle(
        GetCompanyWorkspacesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);

        using var connection = dbFactory.CreateConnection();
        var catalog = await WorkspaceBuilderQueries.LoadCatalogAsync(connection, cancellationToken, activeOnly: true);
        var flags = await WorkspaceBuilderQueries.LoadTenantFlagsAsync(connection, tenantId, cancellationToken);
        var dtos = catalog
            .Select(r => WorkspaceBuilderQueries.ToCompanyDto(
                r, WorkspaceBuilderQueries.IsCompanyEnabled(r.WorkspaceKey, flags)))
            .ToList();
        return ApiResponse<IReadOnlyList<CompanyWorkspaceDto>>.SuccessResponse(dtos);
    }
}

public class GetMyWorkspaceQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyWorkspaceQuery, ApiResponse<ResolvedWorkspaceDto>>
{
    public async Task<ApiResponse<ResolvedWorkspaceDto>> Handle(
        GetMyWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var tenantId = tenantContext.GetRequiredTenantId();

        using var connection = dbFactory.CreateConnection();
        var profile = await connection.QuerySingleOrDefaultAsync<(
            string? DefaultWorkspaceKey, string? HomeRoute, string? RoleCode)>(
            new CommandDefinition("""
                SELECT u.DefaultWorkspaceKey, u.HomeRoute,
                       (SELECT TOP 1 r.Code
                        FROM UserRoles ur INNER JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = u.Id ORDER BY r.Id) AS RoleCode
                FROM Users u
                WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
                """,
                new { UserId = userId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var catalog = await WorkspaceBuilderQueries.LoadCatalogAsync(connection, cancellationToken, activeOnly: true);
        var flags = await WorkspaceBuilderQueries.LoadTenantFlagsAsync(connection, tenantId, cancellationToken);
        var resolved = WorkspaceBuilderQueries.Resolve(
            catalog,
            flags,
            profile.DefaultWorkspaceKey,
            profile.HomeRoute,
            profile.RoleCode ?? currentUser.Role);

        return ApiResponse<ResolvedWorkspaceDto>.SuccessResponse(resolved);
    }
}

public class SetCompanyWorkspacesCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    ICurrentUserService currentUser)
    : IRequestHandler<SetCompanyWorkspacesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SetCompanyWorkspacesCommand request,
        CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        var enabledKeys = request.EnabledWorkspaceKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var connection = dbFactory.CreateConnection();
        var catalog = await WorkspaceBuilderQueries.LoadCatalogAsync(connection, cancellationToken, activeOnly: true);
        var toggleable = catalog.Select(c => c.WorkspaceKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = enabledKeys.Where(k => !toggleable.Contains(k)).ToList();
        if (invalid.Count > 0)
            return ApiResponse<bool>.FailResponse(
                $"Unknown or inactive workspaces: {string.Join(", ", invalid)}");

        var userId = currentUser.UserId;
        var now = DateTime.UtcNow;

        foreach (var key in toggleable)
        {
            var enabled = enabledKeys.Contains(key);
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantWorkspaces WHERE TenantId = @TenantId AND WorkspaceKey = @WorkspaceKey)
                    UPDATE TenantWorkspaces
                    SET IsEnabled = @IsEnabled,
                        EnabledBy = CASE WHEN @IsEnabled = 1 THEN @EnabledBy ELSE EnabledBy END,
                        EnabledDate = CASE WHEN @IsEnabled = 1 THEN COALESCE(EnabledDate, @Now) ELSE EnabledDate END,
                        LastModified = @Now
                    WHERE TenantId = @TenantId AND WorkspaceKey = @WorkspaceKey;
                ELSE
                    INSERT INTO TenantWorkspaces (TenantId, WorkspaceKey, IsEnabled, EnabledBy, EnabledDate, LastModified)
                    VALUES (@TenantId, @WorkspaceKey, @IsEnabled, @EnabledBy, @Now, @Now);
                """,
                new
                {
                    TenantId = request.TenantId,
                    WorkspaceKey = key,
                    IsEnabled = enabled,
                    EnabledBy = userId,
                    Now = now
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Company workspaces updated.");
    }
}

public class UpdateWorkspaceDefinitionCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateWorkspaceDefinitionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateWorkspaceDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var p = request.Payload;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkspaceDefinitions SET
                DisplayName = @DisplayName,
                Description = @Description,
                Category = @Category,
                Icon = @Icon,
                HomeRoute = @HomeRoute,
                SortOrder = @SortOrder,
                Visible = @Visible,
                IsActive = @IsActive,
                IsMobileSupported = @IsMobileSupported,
                ModuleKeysJson = @ModuleKeysJson,
                FeatureKey = @FeatureKey,
                DefaultDashboardKey = @DefaultDashboardKey,
                UpdatedAt = SYSUTCDATETIME()
            WHERE WorkspaceKey = @WorkspaceKey;
            """,
            new
            {
                WorkspaceKey = request.Key,
                p.DisplayName,
                p.Description,
                p.Category,
                p.Icon,
                HomeRoute = string.IsNullOrWhiteSpace(p.HomeRoute) ? "/dashboard" : p.HomeRoute.Trim(),
                p.SortOrder,
                p.Visible,
                p.IsActive,
                p.IsMobileSupported,
                ModuleKeysJson = WorkspaceBuilderQueries.SerializeModuleKeys(p.ModuleKeys),
                p.FeatureKey,
                p.DefaultDashboardKey
            },
            cancellationToken: cancellationToken));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Workspace not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Workspace updated.");
    }
}

public class CreateWorkspaceDefinitionCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateWorkspaceDefinitionCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(
        CreateWorkspaceDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var p = request.Payload;
        var key = (p.WorkspaceKey ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '-');
        if (string.IsNullOrWhiteSpace(key))
            return ApiResponse<string>.FailResponse("WorkspaceKey is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName))
            return ApiResponse<string>.FailResponse("DisplayName is required.");

        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM WorkspaceDefinitions WHERE WorkspaceKey = @Key) THEN 1 ELSE 0 END",
            new { Key = key }, cancellationToken: cancellationToken));
        if (exists == 1)
            return ApiResponse<string>.FailResponse("Workspace key already exists.");

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WorkspaceDefinitions (
                WorkspaceKey, DisplayName, Description, Category, Icon, HomeRoute, SortOrder,
                Visible, IsActive, IsMobileSupported, ModuleKeysJson, FeatureKey, DefaultDashboardKey)
            VALUES (
                @WorkspaceKey, @DisplayName, @Description, @Category, @Icon, @HomeRoute, @SortOrder,
                @Visible, 1, @IsMobileSupported, @ModuleKeysJson, @FeatureKey, @DefaultDashboardKey);

            INSERT INTO TenantWorkspaces (TenantId, WorkspaceKey, IsEnabled, EnabledDate, LastModified)
            SELECT t.Id, @WorkspaceKey, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM Tenants t
            WHERE NOT EXISTS (
                SELECT 1 FROM TenantWorkspaces tw
                WHERE tw.TenantId = t.Id AND tw.WorkspaceKey = @WorkspaceKey);
            """,
            new
            {
                WorkspaceKey = key,
                p.DisplayName,
                p.Description,
                p.Category,
                p.Icon,
                HomeRoute = string.IsNullOrWhiteSpace(p.HomeRoute) ? "/dashboard" : p.HomeRoute.Trim(),
                p.SortOrder,
                Visible = p.Visible,
                IsMobileSupported = p.IsMobileSupported,
                ModuleKeysJson = WorkspaceBuilderQueries.SerializeModuleKeys(p.ModuleKeys),
                p.FeatureKey,
                p.DefaultDashboardKey
            },
            cancellationToken: cancellationToken));

        return ApiResponse<string>.SuccessResponse(key, "Workspace created.");
    }
}

public class DeactivateWorkspaceDefinitionCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeactivateWorkspaceDefinitionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        DeactivateWorkspaceDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkspaceDefinitions SET
                IsActive = 0, Visible = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE WorkspaceKey = @Key;
            """, new { Key = request.Key }, cancellationToken: cancellationToken));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Workspace not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Workspace deactivated.");
    }
}
