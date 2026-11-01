import '../../../core/config/app_config.dart';
import '../../auth/domain/auth_models.dart';

/// Permission / role gates for Settings surfaces.
/// Backend JWT permissions remain the source of truth; this only hides UI.
abstract final class SettingsAccess {
  /// Own timeline for drivers; operational timeline for staff with trip/GPS scope.
  static bool canSeeActivityTimeline(FleetSession? session) {
    if (session == null) return false;
    if (session.isDriverSession) return true;
    return session.hasPermission(FleetPermissions.tripView) ||
        session.hasPermission(FleetPermissions.gpsView) ||
        session.hasAnyRole(const [
          FleetRole.superAdmin,
          FleetRole.tenantAdmin,
          FleetRole.fleetManager,
          FleetRole.dispatcher,
          FleetRole.driverManager,
        ]);
  }

  /// Drivers: own attendance. Managers with Driver.View: team entry (API still scopes).
  static bool canSeeAttendance(FleetSession? session) {
    if (session == null) return false;
    if (session.isDriverSession) return true;
    return session.hasPermission(FleetPermissions.driverView) ||
        session.hasAnyRole(const [
          FleetRole.superAdmin,
          FleetRole.tenantAdmin,
          FleetRole.fleetManager,
          FleetRole.driverManager,
        ]);
  }

  /// Driver tracking status / location permissions, or staff GPS viewers.
  static bool canSeeLocationTracking(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession ||
        session.hasPermission(FleetPermissions.gpsView);
  }

  /// Sync & Connectivity — all authenticated roles.
  static bool canSeeSyncStatus(FleetSession? session) => session != null;

  /// Destructive offline-queue remove — Super Admin only.
  static bool canClearOfflineQueue(FleetSession? session) {
    if (session == null) return false;
    return session.hasRole(FleetRole.superAdmin);
  }

  /// Drivers cannot freely stop company-mandatory live tracking from Settings.
  static bool canStopBackgroundTracking(FleetSession? session) {
    if (session == null) return false;
    if (session.isDriverOnly) return false;
    return session.hasAnyRole(const [
      FleetRole.superAdmin,
      FleetRole.tenantAdmin,
      FleetRole.fleetManager,
    ]);
  }

  /// Root / jailbreak / emulator diagnostics — Super Admin (and Tenant Admin on non-prod).
  static bool canSeeSecurityDiagnostics(FleetSession? session) {
    if (session == null) return false;
    if (session.hasRole(FleetRole.superAdmin)) return true;
    return !AppConfig.isProd && session.hasRole(FleetRole.tenantAdmin);
  }

  /// Role / permission summary — all authenticated users (content scoped by session).
  static bool canSeeSecurityPermissions(FleetSession? session) =>
      session != null;

  /// Environment badge — never on production builds; otherwise Super Admin / DEV only.
  static bool canSeeEnvironmentLabel(FleetSession? session) {
    if (AppConfig.isProd) return false;
    if (session == null) return false;
    if (AppConfig.environment == AppEnvironment.dev) {
      return session.hasRole(FleetRole.superAdmin) ||
          session.hasRole(FleetRole.tenantAdmin);
    }
    // UAT: Super Admin only
    return session.hasRole(FleetRole.superAdmin);
  }

  /// Profile deep-link from Settings (My Account).
  static bool canSeeProfileAccount(FleetSession? session) => session != null;
}
