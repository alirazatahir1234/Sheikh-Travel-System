using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;

namespace SheikhTravelSystem.Tests.Auth;

/// <summary>
/// SG-05: every HttpPost/Put/Patch/Delete must not be gated only by permissions
/// PLATFORM_AUDITOR holds (read/view family). Ungated / View-gated writes must be
/// explicit allowlists (existing debt shown in SG-05 summary).
/// </summary>
public class PlatformAuditorWriteGateReflectionTests
{
    /// <summary>
    /// Controller actions that intentionally have no RequirePermission (auth via other means,
    /// anonymous public, or self-scoped). Expand deliberately — do not silence new writes.
    /// </summary>
    private static readonly HashSet<string> UngatedWriteAllowlist = new(StringComparer.Ordinal)
    {
        "AuthController.Login",
        "AuthController.Refresh",
        "AuthController.Logout",
        "AuthController.ForgotPassword",
        "AuthController.ResetPassword",
        "CustomerPortalController.Login",
        "CustomerPortalController.Register",
        "PortalPaymentsController.CreateCheckout",
        "WebsiteController.SubmitContact",
        "WebsiteController.SubmitDemoRequest",
        // Authenticated self-service (Authorize only — no RequirePermission)
        "AiController.MobileHeartbeat",
        "AiController.RegisterDeviceToken",
        "ProfileController.ChangePassword",
        "ProfileController.UpdateProfile",
    };

    /// <summary>
    /// Existing write endpoints gated only by a View permission the auditor holds.
    /// These are pre-existing mis-gates (SG-16 in reverse); do not add new entries lightly.
    /// </summary>
    public static readonly HashSet<string> ViewGatedWriteAllowlist = new(StringComparer.Ordinal)
    {
        "AiController.AckEscalation",
        "AssignmentsController.Validate",
        "GoogleMapsController.SnapGpsPath",
        "GoogleMapsController.OptimizeDispatchTours",
        "GpsTrackingController.IngestPosition",
        "GpsTrackingController.PostHistoryReplayInsights",
        "GpsTrackingController.PostOperatorInsights",
        "GpsTrackingController.CompleteCommand",
        "TrackingController.UpdateLocation",
        "NotificationsController.UpsertPreferences",
        "NotificationsController.MarkRead",
        "NotificationsController.Archive",
        "NotificationsController.Restore",
        "NotificationsController.BulkDelete",
        "NotificationsController.DeleteAll",
        "NotificationsController.Delete",
        "PricingController.Calculate",
        "TripsController.EstimateCost",
        "WhatsAppInboxController.MarkRead",
    };

    [Fact]
    public void Write_actions_are_not_gated_only_by_auditor_held_permissions()
    {
        var auditorPerms = PlatformOperatorPermissionsMigration.BuildAuditorPermissionList(
                PlatformPermissions.All
                    .Concat(FleetPermissions.All)
                    .Concat(DriverPermissions.All)
                    .Concat(MaintenancePermissions.All)
                    .Concat(GpsPermissions.All)
                    .Concat(OperationsPermissions.All)
                    .Concat(FinancePermissions.All)
                    .Concat(AnalyticsPermissions.All)
                    .Concat(AiPermissions.All)
                    .Concat(NotificationPermissions.All)
                    .Concat(WebsitePermissions.All)
                    .Concat(WhatsAppPermissions.All)
                    .Concat(SmartOpsPermissions.All)
                    .Concat(RentalPermissions.All))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apiAssembly = typeof(SheikhTravelSystem.API.Controllers.AdminCrossTenantController).Assembly;
        var controllers = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        var violations = new List<string>();
        var ungated = new List<string>();

        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!IsWriteAction(method))
                    continue;

                var key = $"{controller.Name}.{method.Name}";
                var requirePerms = method.GetCustomAttributes(true)
                    .Concat(controller.GetCustomAttributes(true))
                    .OfType<RequirePermissionAttribute>()
                    .Select(a => a.Policy)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (requirePerms.Count == 0)
                {
                    if (!UngatedWriteAllowlist.Contains(key) && !IsOtherwiseGated(method, controller))
                        ungated.Add(key);
                    continue;
                }

                if (requirePerms.All(p => auditorPerms.Contains(p!))
                    && !ViewGatedWriteAllowlist.Contains(key))
                {
                    violations.Add($"{key} => [{string.Join(", ", requirePerms)}]");
                }
            }
        }

        violations.Should().BeEmpty(
            "PLATFORM_AUDITOR must not satisfy any write endpoint via RequirePermission alone (add to ViewGatedWriteAllowlist only for pre-existing debt). Offenders: {0}",
            string.Join("; ", violations));

        ungated.Should().BeEmpty(
            "Ungated write actions must be listed in UngatedWriteAllowlist. Found: {0}",
            string.Join(", ", ungated.OrderBy(x => x)));
    }

    private static bool IsWriteAction(MethodInfo method) =>
        method.GetCustomAttributes(true).Any(a =>
            a is HttpPostAttribute or HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute);

    private static bool IsOtherwiseGated(MethodInfo method, Type controller)
    {
        var attrs = method.GetCustomAttributes(true).Concat(controller.GetCustomAttributes(true)).ToList();

        if (attrs.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any(a => !string.IsNullOrWhiteSpace(a.Roles)
                      || (!string.IsNullOrWhiteSpace(a.Policy) && a is not RequirePermissionAttribute)))
            return true;

        if (attrs.Any(a => a is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute))
            return true;

        return false;
    }
}
