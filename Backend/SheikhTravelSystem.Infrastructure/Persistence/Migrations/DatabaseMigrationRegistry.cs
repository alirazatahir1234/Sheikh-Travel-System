using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Ordered registry of all custom schema migrations.
/// Order matches the historical Program.cs startup sequence (BookingNumber first, then GPS block, portal, tenant/fleet).
/// </summary>
public static class DatabaseMigrationRegistry
{
    public static IReadOnlyList<IDatabaseMigration> All { get; } = Build();

    private static IReadOnlyList<IDatabaseMigration> Build()
    {
        static DelegateMigration M(string name, Func<IDbConnectionFactory, ILogger, CancellationToken, Task> apply)
            => new(name, apply);

        return
        [
            M("BookingNumberMigration", BookingNumberMigration.ApplyAsync),
            M("GpsSchemaMigration", GpsSchemaMigration.ApplyAsync),
            M("GpsTraccarMigration", GpsTraccarMigration.ApplyAsync),
            M("GpsDeviceUniqueIdMigration", GpsDeviceUniqueIdMigration.ApplyAsync),
            M("GpsDevicesTenantMigration", GpsDevicesTenantMigration.ApplyAsync),
            M("GpsTraccarEventMigration", GpsTraccarEventMigration.ApplyAsync),
            M("GpsDeviceCommandsMigration", GpsDeviceCommandsMigration.ApplyAsync),
            M("GpsDeviceTelemetryMigration", GpsDeviceTelemetryMigration.ApplyAsync),
            M("GpsDeviceInstallationMigration", (db, log, _) => GpsDeviceInstallationMigration.ApplyAsync(db, log)),
            M("GpsTrackerBusinessMigration", GpsTrackerBusinessMigration.ApplyAsync),
            M("TrackerCatalogMigration", TrackerCatalogMigration.ApplyAsync),
            M("TrackerStatusMigration", TrackerStatusMigration.ApplyAsync),
            M("GpsDeviceAssignmentMigration", GpsDeviceAssignmentMigration.ApplyAsync),
            M("TrackerRelayMigration", TrackerRelayMigration.ApplyAsync),
            M("GpsTelemetryFieldsMigration", GpsTelemetryFieldsMigration.ApplyAsync),
            M("GpsGeofenceModuleMigration", GpsGeofenceModuleMigration.ApplyAsync),
            M("GpsFleetStatusHistoryMigration", GpsFleetStatusHistoryMigration.ApplyAsync),
            M("GpsAlertsPhase8Migration", GpsAlertsPhase8Migration.ApplyAsync),
            M("GpsAlertsLifecycleV2Migration", GpsAlertsLifecycleV2Migration.ApplyAsync),
            M("GpsCommandsPhase9Migration", GpsCommandsPhase9Migration.ApplyAsync),
            M("GpsAnalyticsPhase10Migration", GpsAnalyticsPhase10Migration.ApplyAsync),
            M("GpsAddressCacheMigration", GpsAddressCacheMigration.ApplyAsync),
            M("NotificationCenterMigration", NotificationCenterMigration.ApplyAsync),
            M("NotificationCenterV2Migration", NotificationCenterV2Migration.ApplyAsync),
            M("NotificationTenantIsolationMigration", NotificationTenantIsolationMigration.ApplyAsync),
            M("AiPlatformMigration", AiPlatformMigration.ApplyAsync),
            M("PerformanceIndexesMigration", PerformanceIndexesMigration.ApplyAsync),
            M("PortalSchemaMigration", PortalSchemaMigration.ApplyAsync),
            M("TenantSchemaMigration", TenantSchemaMigration.ApplyAsync),
            M("PlatformSchemaMigration", PlatformSchemaMigration.ApplyAsync),
            M("TenantNormalizationMigration", TenantNormalizationMigration.ApplyAsync),
            M("PlatformSettingsMigration", PlatformSettingsMigration.ApplyAsync),
            M("NotificationLifecycleMigration", NotificationLifecycleMigration.ApplyAsync),
            M("NotificationEmailTemplatesMigration", NotificationEmailTemplatesMigration.ApplyAsync),
            M("NotificationMessageSizeMigration", NotificationMessageSizeMigration.ApplyAsync),
            M("OrganizationDesignerMigration", OrganizationDesignerMigration.ApplyAsync),
            M("SubscriptionBillingMigration", SubscriptionBillingMigration.ApplyAsync),
            M("FleetSchemaMigration", FleetSchemaMigration.ApplyAsync),
            M("FleetComplianceMigration", FleetComplianceMigration.ApplyAsync),
            M("VehicleDocumentOcrMigration", VehicleDocumentOcrMigration.ApplyAsync),
            M("DriverPerformanceMigration", DriverPerformanceMigration.ApplyAsync),
            M("DriverVerificationMigration", DriverVerificationMigration.ApplyAsync),
            M("AssignmentSchemaMigration", AssignmentSchemaMigration.ApplyAsync),
            M("MaintenanceModuleMigration", MaintenanceModuleMigration.ApplyAsync),
            M("TripsModuleMigration", TripsModuleMigration.ApplyAsync),
            M("TripsPhase2Migration", TripsPhase2Migration.ApplyAsync),
            M("TripsPhase4Migration", TripsPhase4Migration.ApplyAsync),
            M("DriverAppSosMigration", DriverAppSosMigration.ApplyAsync),
            M("DriverInspectionMigration", DriverInspectionMigration.ApplyAsync),
            M("DriverFuelReceiptMigration", DriverFuelReceiptMigration.ApplyAsync),
            M("DriverDeviceRegistrationMigration", DriverDeviceRegistrationMigration.ApplyAsync),
            M("FleetTrackingRenameMigration", FleetTrackingRenameMigration.ApplyAsync),
            M("RouteWaypointsMigration", RouteWaypointsMigration.ApplyAsync),
            M("AiChatPhase1Migration", AiChatPhase1Migration.ApplyAsync),
            M("AiPendingActionsPhase3Migration", AiPendingActionsPhase3Migration.ApplyAsync),
            M("DriverManagerRoleTemplateMigration", DriverManagerRoleTemplateMigration.ApplyAsync),
            M("AccessManagementPermissionsMigration", AccessManagementPermissionsMigration.ApplyAsync),
            M("PlatformAdminFoundationMigration", PlatformAdminFoundationMigration.ApplyAsync),
            M("CompanyFeatureRegistryMigration", CompanyFeatureRegistryMigration.ApplyAsync),
            M("ModuleRegistryMigration", ModuleRegistryMigration.ApplyAsync),
            M("SubscriptionLicenseMigration", SubscriptionLicenseMigration.ApplyAsync),
            M("FeatureManagementMigration", FeatureManagementMigration.ApplyAsync),
            M("UserManagementFoundationMigration", UserManagementFoundationMigration.ApplyAsync),
        ];
    }
}
