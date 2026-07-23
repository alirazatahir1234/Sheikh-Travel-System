import '../domain/dashboard_layout.dart';
import '../domain/dashboard_role.dart';

/// Maps [DashboardRole] → ordered widget IDs + quick actions (Fleet Command).
abstract final class DashboardLayoutRegistry {
  static List<DashboardWidgetId> widgetsFor(DashboardRole role) {
    switch (role) {
      case DashboardRole.driver:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.myVehicle,
          DashboardWidgetId.driverTripKpis,
          DashboardWidgetId.earnings,
          DashboardWidgetId.quickActions,
        ];
      case DashboardRole.fleetManager:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.universalSearchBar,
          DashboardWidgetId.fleetHealthHeader,
          DashboardWidgetId.fleetStatsStrip,
          DashboardWidgetId.opsKpiGrid,
          DashboardWidgetId.liveMapPreview,
          DashboardWidgetId.aiAttention,
          DashboardWidgetId.criticalAlertsList,
          DashboardWidgetId.attentionVehicles,
          DashboardWidgetId.quickActions,
        ];
      case DashboardRole.tenantAdmin:
      case DashboardRole.superAdmin:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.platformBanner,
          DashboardWidgetId.universalSearchBar,
          DashboardWidgetId.fleetHealthHeader,
          DashboardWidgetId.fleetStatsStrip,
          DashboardWidgetId.opsKpiGrid,
          DashboardWidgetId.mapSummaryCard,
          DashboardWidgetId.aiAttention,
          DashboardWidgetId.criticalAlertsList,
          DashboardWidgetId.attentionVehicles,
          DashboardWidgetId.quickActions,
        ];
      case DashboardRole.dispatcher:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.universalSearchBar,
          DashboardWidgetId.fleetStatsStrip,
          DashboardWidgetId.opsKpiGrid,
          DashboardWidgetId.liveMapPreview,
          DashboardWidgetId.liveTripsPreview,
          DashboardWidgetId.pendingAssignments,
          DashboardWidgetId.criticalAlertsList,
          DashboardWidgetId.quickActions,
        ];
      case DashboardRole.driverManager:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.universalSearchBar,
          DashboardWidgetId.fleetStatsStrip,
          DashboardWidgetId.driverKpis,
          DashboardWidgetId.driverPerformance,
          DashboardWidgetId.complianceDocs,
          DashboardWidgetId.aiAttention,
          DashboardWidgetId.recentActivities,
          DashboardWidgetId.quickActions,
        ];
      case DashboardRole.accountant:
        return const [
          DashboardWidgetId.opsHeader,
          DashboardWidgetId.financeKpis,
          DashboardWidgetId.fuelSummary,
          DashboardWidgetId.maintenanceCost,
          DashboardWidgetId.aiAttention,
          DashboardWidgetId.quickActions,
        ];
    }
  }

  static List<DashboardQuickAction> quickActionsFor(DashboardRole role) {
    switch (role) {
      case DashboardRole.driver:
        return const [
          DashboardQuickAction(
            label: 'Trips',
            iconName: 'route',
            route: '/trips',
          ),
          DashboardQuickAction(
            label: 'Tracking',
            iconName: 'map',
            route: '/live',
          ),
          DashboardQuickAction(
            label: 'Fuel',
            iconName: 'local_gas_station',
            route: '/fuel',
          ),
          DashboardQuickAction(
            label: 'Attendance',
            iconName: 'fingerprint',
            route: '/attendance',
          ),
        ];
      case DashboardRole.fleetManager:
      case DashboardRole.tenantAdmin:
      case DashboardRole.superAdmin:
        return const [
          DashboardQuickAction(
            label: 'Fleet',
            iconName: 'local_shipping',
            route: '/fleet',
          ),
          DashboardQuickAction(
            label: 'Map',
            iconName: 'map',
            route: '/fleet/map',
          ),
          DashboardQuickAction(
            label: 'Alerts',
            iconName: 'warning',
            route: '/alerts',
          ),
          DashboardQuickAction(
            label: 'Trips',
            iconName: 'route',
            route: '/trips',
          ),
        ];
      case DashboardRole.dispatcher:
        return const [
          DashboardQuickAction(
            label: 'Bookings',
            iconName: 'event_note',
            route: '/bookings',
          ),
          DashboardQuickAction(
            label: 'Trips',
            iconName: 'route',
            route: '/trips',
          ),
          DashboardQuickAction(
            label: 'Map',
            iconName: 'map',
            route: '/fleet/map',
          ),
          DashboardQuickAction(
            label: 'Drivers',
            iconName: 'badge',
            route: '/more/drivers',
          ),
        ];
      case DashboardRole.driverManager:
        return const [
          DashboardQuickAction(
            label: 'Drivers',
            iconName: 'badge',
            route: '/more/drivers',
          ),
          DashboardQuickAction(
            label: 'Docs',
            iconName: 'folder',
            route: '/documents',
          ),
          DashboardQuickAction(
            label: 'Trips',
            iconName: 'route',
            route: '/trips',
          ),
          DashboardQuickAction(
            label: 'Alerts',
            iconName: 'warning',
            route: '/alerts',
          ),
        ];
      case DashboardRole.accountant:
        return const [
          DashboardQuickAction(
            label: 'Finance',
            iconName: 'payments',
            route: '/finance',
          ),
          DashboardQuickAction(
            label: 'Reports',
            iconName: 'assessment',
            route: '/more/reports',
          ),
          DashboardQuickAction(
            label: 'Fuel',
            iconName: 'local_gas_station',
            route: '/fuel',
          ),
          DashboardQuickAction(
            label: 'Maintenance',
            iconName: 'build',
            route: '/more/maintenance',
          ),
        ];
    }
  }
}
