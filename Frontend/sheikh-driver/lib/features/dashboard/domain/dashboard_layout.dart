/// Identifiers for composable dashboard widgets.
enum DashboardWidgetId {
  greeting,
  opsHeader,
  platformBanner,
  primaryKpis,
  myVehicle,
  driverTripKpis,
  earnings,
  fleetHealthHeader,
  fleetStatsStrip,
  opsKpiGrid,
  fleetKpis,
  fleetStatusStrip,
  liveFleetCard,
  liveMapPreview,
  mapSummaryCard,
  aiAttention,
  criticalAlertsList,
  todayOpsKpis,
  recentActivities,
  maintenanceKpis,
  fuelSummary,
  recentAlerts,
  tripKpis,
  liveTripsPreview,
  pendingAssignments,
  driverKpis,
  driverPerformance,
  complianceDocs,
  financeKpis,
  fuelCost,
  maintenanceCost,
  universalSearchBar,
  attentionVehicles,
  gpsExceptionKpiGrid,
  trackerHealthCard,
  recentGpsAlertsFeed,
  quickActions,
}

class DashboardQuickAction {
  const DashboardQuickAction({
    required this.label,
    required this.iconName,
    required this.route,
    this.colorKey = 'primary',
  });

  final String label;
  /// Material icon name key resolved in UI.
  final String iconName;
  final String route;
  final String colorKey;
}

/// Role-specific primary KPI cell for the shared shell strip.
class KpiCell {
  const KpiCell({
    required this.label,
    required this.value,
    this.colorKey = 'primary',
    this.route,
    this.subtitle,
  });

  final String label;
  final String value;
  final String colorKey;
  final String? route;
  final String? subtitle;
}
