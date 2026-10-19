import '../../auth/domain/auth_models.dart';
import 'dashboard_layout.dart';
import 'dashboard_role.dart';

/// Widget-level visibility for Fleet Command Dashboard (PRD role mapping).
///
/// PRD → JWT: Owner=`OWNER`/`TENANT_ADMIN`/`SUPER_ADMIN`, Ops=`FLEET_MANAGER`,
/// Supervisor=`DRIVER_MANAGER`, Maintenance Manager=`FLEET_MANAGER`+`Maintenance.View`.
abstract final class DashboardVisibility {
  static bool isOwnerLike(DashboardRole role) =>
      role == DashboardRole.owner ||
      role == DashboardRole.tenantAdmin ||
      role == DashboardRole.superAdmin;

  static bool isFleetOps(DashboardRole role) =>
      role == DashboardRole.fleetManager ||
      role == DashboardRole.gpsOperator ||
      isOwnerLike(role);

  /// Interactive live map preview for Owner Tracking + fleet ops roles.
  static bool showInteractiveMap(DashboardRole role) =>
      isOwnerLike(role) ||
      role == DashboardRole.fleetManager ||
      role == DashboardRole.gpsOperator ||
      role == DashboardRole.dispatcher;

  /// Legacy summary card — unused for Owner Tracking (interactive map preferred).
  static bool showMapSummary(DashboardRole role) => false;

  static bool showFleetHealth(DashboardRole role) => isFleetOps(role);

  static bool showUniversalSearch(DashboardRole role) =>
      role != DashboardRole.driver;

  static bool showAttentionVehicles(DashboardRole role) => isFleetOps(role);

  static bool showAiAttention(DashboardRole role) =>
      isFleetOps(role) ||
      role == DashboardRole.driverManager ||
      role == DashboardRole.accountant ||
      role == DashboardRole.driver;

  /// Whether [id] is allowed for [role] before data/permission checks.
  static bool roleAllows(DashboardRole role, DashboardWidgetId id) {
    switch (id) {
      case DashboardWidgetId.fleetHealthHeader:
        return showFleetHealth(role);
      case DashboardWidgetId.liveMapPreview:
      case DashboardWidgetId.liveFleetCard:
        return showInteractiveMap(role);
      case DashboardWidgetId.mapSummaryCard:
        return showMapSummary(role);
      case DashboardWidgetId.universalSearchBar:
        return showUniversalSearch(role);
      case DashboardWidgetId.attentionVehicles:
        return showAttentionVehicles(role);
      case DashboardWidgetId.aiAttention:
        return showAiAttention(role);
      default:
        return true;
    }
  }

  static bool hasOpsDataPerms(FleetSession session) =>
      session.hasPermission(FleetPermissions.vehicleView) ||
      session.hasPermission(FleetPermissions.gpsView);
}
