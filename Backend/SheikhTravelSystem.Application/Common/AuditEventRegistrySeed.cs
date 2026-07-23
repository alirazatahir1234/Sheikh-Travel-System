namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stable EventKey constants for Stage 14 Audit Center.
/// </summary>
public static class AuditEventKeys
{
    public const string LoginSuccess = "auth.login.success";
    public const string LoginFailed = "auth.login.failed";
    public const string Logout = "auth.logout";
    public const string Lockout = "auth.lockout";
    public const string PasswordChanged = "auth.password.changed";
    public const string PasswordReset = "auth.password.reset";

    public const string UserCreate = "users.create";
    public const string UserUpdate = "users.update";
    public const string UserDelete = "users.delete";
    public const string UserStatus = "users.status";
    public const string RoleCreate = "roles.create";
    public const string RoleUpdate = "roles.update";
    public const string RoleDelete = "roles.delete";
    public const string RolePermissions = "roles.permissions";
    public const string PermissionChange = "permissions.change";

    public const string SecurityPolicyUpdate = "security.policy.update";
    public const string ModuleEnable = "platform.module.enable";
    public const string ModuleDisable = "platform.module.disable";
    public const string FeatureEnable = "platform.feature.enable";
    public const string FeatureDisable = "platform.feature.disable";

    public const string GenericCreate = "generic.create";
    public const string GenericUpdate = "generic.update";
    public const string GenericDelete = "generic.delete";
    public const string GenericAction = "generic.action";
    public const string GenericError = "generic.error";
}

public record AuditEventDefinitionSeed(
    string EventKey,
    string DisplayName,
    string Category,
    string Severity,
    string? Description,
    int SortOrder = 0,
    bool Visible = true);

/// <summary>
/// Seed catalog for AuditEventDefinitions.
/// </summary>
public static class AuditEventRegistrySeed
{
    public static IReadOnlyList<AuditEventDefinitionSeed> All { get; } =
    [
        new(AuditEventKeys.LoginSuccess, "Login success", "Authentication", "Information",
            "User signed in successfully.", 10),
        new(AuditEventKeys.LoginFailed, "Login failed", "Authentication", "Warning",
            "Failed sign-in attempt.", 20),
        new(AuditEventKeys.Logout, "Logout", "Authentication", "Information",
            "User signed out.", 30),
        new(AuditEventKeys.Lockout, "Account lockout", "Authentication", "Warning",
            "Account locked after failed attempts.", 40),
        new(AuditEventKeys.PasswordChanged, "Password changed", "Authentication", "Information",
            "User changed their password.", 50),
        new(AuditEventKeys.PasswordReset, "Password reset", "Authentication", "Warning",
            "Password was reset by an administrator.", 60),

        new(AuditEventKeys.UserCreate, "User created", "Users", "Information", null, 110),
        new(AuditEventKeys.UserUpdate, "User updated", "Users", "Information", null, 120),
        new(AuditEventKeys.UserDelete, "User deleted", "Users", "Critical", null, 130),
        new(AuditEventKeys.UserStatus, "User status changed", "Users", "Warning", null, 140),

        new(AuditEventKeys.RoleCreate, "Role created", "Roles", "Information", null, 210),
        new(AuditEventKeys.RoleUpdate, "Role updated", "Roles", "Information", null, 220),
        new(AuditEventKeys.RoleDelete, "Role deleted", "Roles", "Critical", null, 230),
        new(AuditEventKeys.RolePermissions, "Role permissions updated", "Authorization", "Warning", null, 240),
        new(AuditEventKeys.PermissionChange, "Permission change", "Permissions", "Warning", null, 250),

        new(AuditEventKeys.SecurityPolicyUpdate, "Security policy updated", "Security", "Critical",
            "Company security policy values changed.", 310),
        new(AuditEventKeys.ModuleEnable, "Module enabled", "Platform", "Information", null, 320),
        new(AuditEventKeys.ModuleDisable, "Module disabled", "Platform", "Warning", null, 330),
        new(AuditEventKeys.FeatureEnable, "Feature enabled", "Settings", "Information", null, 340),
        new(AuditEventKeys.FeatureDisable, "Feature disabled", "Settings", "Warning", null, 350),

        new(AuditEventKeys.GenericCreate, "Entity created", "Administration", "Information",
            "Fallback create event.", 900),
        new(AuditEventKeys.GenericUpdate, "Entity updated", "Administration", "Information",
            "Fallback update event.", 910),
        new(AuditEventKeys.GenericDelete, "Entity deleted", "Administration", "Critical",
            "Fallback delete event.", 920),
        new(AuditEventKeys.GenericAction, "Entity action", "Administration", "Information",
            "Fallback generic action.", 930),
        new(AuditEventKeys.GenericError, "Operation failed", "Administration", "Error",
            "Failed auditable command.", 940),

        // Domain placeholders (Fleet / Travel / Finance / AI / Reports) for catalog completeness
        new("fleet.vehicle.create", "Vehicle created", "Fleet", "Information", null, 410),
        new("fleet.vehicle.update", "Vehicle updated", "Fleet", "Information", null, 420),
        new("fleet.vehicle.delete", "Vehicle deleted", "Fleet", "Critical", null, 430),
        new("fleet.driver.create", "Driver created", "Fleet", "Information", null, 440),
        new("fleet.driver.update", "Driver updated", "Fleet", "Information", null, 450),
        new("fleet.driver.delete", "Driver deleted", "Fleet", "Critical", null, 460),
        new("travel.trip.create", "Trip created", "Travel", "Information", null, 510),
        new("travel.trip.update", "Trip updated", "Travel", "Information", null, 520),
        new("travel.booking.create", "Booking created", "Bookings", "Information", null, 530),
        new("travel.booking.update", "Booking updated", "Bookings", "Information", null, 540),
        new("finance.payment.create", "Payment recorded", "Finance", "Information", null, 610),
        new("ai.action.execute", "AI action executed", "AI", "Information", null, 710),
        new("reports.export", "Report exported", "Reports", "Information", null, 810),
    ];

    /// <summary>
    /// Maps legacy IAuditableCommand EntityName + Action to a registry EventKey.
    /// </summary>
    public static string ResolveEventKey(string? entityName, string? action)
    {
        var entity = (entityName ?? "").Trim();
        var act = (action ?? "").Trim();
        var e = entity.ToLowerInvariant();
        var a = act.ToLowerInvariant();

        if (e.Contains("securitypolicy") || e == "securitypolicy")
            return AuditEventKeys.SecurityPolicyUpdate;
        if (e.Contains("user"))
        {
            if (a.Contains("password") && a.Contains("reset")) return AuditEventKeys.PasswordReset;
            if (a.Contains("password")) return AuditEventKeys.PasswordChanged;
            if (a.Contains("status")) return AuditEventKeys.UserStatus;
            if (a.Contains("delete")) return AuditEventKeys.UserDelete;
            if (a.Contains("create") || a.Contains("add")) return AuditEventKeys.UserCreate;
            return AuditEventKeys.UserUpdate;
        }
        if (e.Contains("role"))
        {
            if (a.Contains("permission")) return AuditEventKeys.RolePermissions;
            if (a.Contains("delete")) return AuditEventKeys.RoleDelete;
            if (a.Contains("create")) return AuditEventKeys.RoleCreate;
            return AuditEventKeys.RoleUpdate;
        }
        if (e.Contains("permission")) return AuditEventKeys.PermissionChange;
        if (e.Contains("module"))
            return a.Contains("disable") || a.Contains("off") ? AuditEventKeys.ModuleDisable : AuditEventKeys.ModuleEnable;
        if (e.Contains("feature"))
            return a.Contains("disable") || a.Contains("off") ? AuditEventKeys.FeatureDisable : AuditEventKeys.FeatureEnable;
        if (e.Contains("vehicle"))
        {
            if (a.Contains("delete")) return "fleet.vehicle.delete";
            if (a.Contains("create")) return "fleet.vehicle.create";
            return "fleet.vehicle.update";
        }
        if (e.Contains("driver"))
        {
            if (a.Contains("delete")) return "fleet.driver.delete";
            if (a.Contains("create")) return "fleet.driver.create";
            return "fleet.driver.update";
        }
        if (e.Contains("trip"))
            return a.Contains("create") ? "travel.trip.create" : "travel.trip.update";
        if (e.Contains("booking"))
            return a.Contains("create") ? "travel.booking.create" : "travel.booking.update";
        if (e.Contains("payment")) return "finance.payment.create";

        if (a.Contains("fail") || a.Contains("error")) return AuditEventKeys.GenericError;
        if (a.Contains("delete")) return AuditEventKeys.GenericDelete;
        if (a.Contains("create") || a.Contains("add")) return AuditEventKeys.GenericCreate;
        if (a.Contains("update") || a.Contains("edit") || a.Contains("save")) return AuditEventKeys.GenericUpdate;
        return AuditEventKeys.GenericAction;
    }
}
