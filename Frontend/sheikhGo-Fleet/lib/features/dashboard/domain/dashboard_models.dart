import '../../alerts/domain/gps_alert_models.dart';
import '../../auth/domain/auth_models.dart';
import '../../compliance/domain/compliance_models.dart';
import '../../drivers/domain/driver_models.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../maintenance/domain/maintenance_models.dart';
import '../../ops_trips/domain/ops_trip_models.dart';
import 'dashboard_layout.dart';
import 'dashboard_role.dart';
import 'dashboard_visibility.dart';

/// Legacy driver-shaped summary (kept for driver widgets / status updates).
class DriverDashboardSummary {
  const DriverDashboardSummary({
    required this.assignedTripsToday,
    required this.completedToday,
    required this.clockedIn,
    this.currentVehicle,
    this.currentVehiclePlate,
    required this.earningsThisWeek,
    required this.unreadNotifications,
    required this.driverStatus,
  });

  final int assignedTripsToday;
  final int completedToday;
  final bool clockedIn;
  final String? currentVehicle;
  final String? currentVehiclePlate;
  final double earningsThisWeek;
  final int unreadNotifications;
  final String driverStatus;

  factory DriverDashboardSummary.fromJson(Map<String, dynamic> json) {
    return DriverDashboardSummary(
      assignedTripsToday: json['assignedTripsToday'] as int? ??
          json['AssignedTripsToday'] as int? ??
          0,
      completedToday:
          json['completedToday'] as int? ?? json['CompletedToday'] as int? ?? 0,
      clockedIn:
          json['clockedIn'] as bool? ?? json['ClockedIn'] as bool? ?? false,
      currentVehicle: json['currentVehicle'] as String? ??
          json['CurrentVehicle'] as String?,
      currentVehiclePlate: json['currentVehiclePlate'] as String? ??
          json['CurrentVehiclePlate'] as String?,
      earningsThisWeek: (json['earningsThisWeek'] as num?)?.toDouble() ??
          (json['EarningsThisWeek'] as num?)?.toDouble() ??
          0.0,
      unreadNotifications: json['unreadNotifications'] as int? ??
          json['UnreadNotifications'] as int? ??
          0,
      driverStatus: json['driverStatus'] as String? ??
          json['DriverStatus'] as String? ??
          'Available',
    );
  }

  static const empty = DriverDashboardSummary(
    assignedTripsToday: 0,
    completedToday: 0,
    clockedIn: false,
    earningsThisWeek: 0,
    unreadNotifications: 0,
    driverStatus: 'Available',
  );
}

class AiAttentionItem {
  const AiAttentionItem({
    required this.text,
    this.route,
    this.suggestedPrompt,
    this.severity = 'info',
  });

  final String text;
  final String? route;
  final String? suggestedPrompt;

  /// critical | warning | success | info
  final String severity;
}

class DashboardSearchHit {
  const DashboardSearchHit({
    required this.kind,
    required this.title,
    required this.subtitle,
    required this.route,
  });

  final String kind;
  final String title;
  final String subtitle;
  final String route;
}

class FuelAnalyticsSummary {
  const FuelAnalyticsSummary({
    required this.totalLiters,
    required this.totalCost,
    this.fleetLitersPer100Km,
    this.todayLiters = 0,
    this.todayCost = 0,
  });

  final double totalLiters;
  final double totalCost;
  final double? fleetLitersPer100Km;
  final double todayLiters;
  final double todayCost;

  /// km/L derived from L/100km when available.
  double? get efficiencyKmPerL {
    final l = fleetLitersPer100Km;
    if (l == null || l <= 0) return null;
    return 100.0 / l;
  }

  factory FuelAnalyticsSummary.fromJson(Map<String, dynamic> json) {
    final dailyRaw = json['daily'] ?? json['Daily'];
    double todayLiters = 0;
    double todayCost = 0;
    final today = DateTime.now().toUtc();
    if (dailyRaw is List) {
      for (final row in dailyRaw.whereType<Map>()) {
        final map = Map<String, dynamic>.from(row);
        final dateRaw = map['date'] ?? map['Date'];
        final d = dateRaw == null
            ? null
            : DateTime.tryParse(dateRaw.toString())?.toUtc();
        if (d == null) continue;
        if (d.year == today.year &&
            d.month == today.month &&
            d.day == today.day) {
          todayLiters =
              (map['liters'] as num? ?? map['Liters'] as num? ?? 0).toDouble();
          todayCost =
              (map['cost'] as num? ?? map['Cost'] as num? ?? 0).toDouble();
          break;
        }
      }
    }
    return FuelAnalyticsSummary(
      totalLiters:
          (json['totalLiters'] as num? ?? json['TotalLiters'] as num? ?? 0)
              .toDouble(),
      totalCost: (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
          .toDouble(),
      fleetLitersPer100Km: (json['fleetLitersPer100Km'] as num? ??
              json['FleetLitersPer100Km'] as num?)
          ?.toDouble(),
      todayLiters: todayLiters,
      todayCost: todayCost,
    );
  }

  static const empty = FuelAnalyticsSummary(
    totalLiters: 0,
    totalCost: 0,
  );
}

class ActivityItem {
  const ActivityItem({
    required this.at,
    required this.title,
    required this.subtitle,
    required this.kind,
    this.route,
  });

  final DateTime at;
  final String title;
  final String subtitle;

  /// alert | notification | trip | maintenance | system
  final String kind;
  final String? route;
}

/// Role-aware home dashboard payload.
class RoleDashboardData {
  const RoleDashboardData({
    required this.role,
    required this.displayName,
    required this.widgets,
    required this.quickActions,
    this.tenantId,
    this.lastSyncedAt,
    this.primaryKpis = const [],
    this.driver,
    this.fleet,
    this.gps,
    this.trips,
    this.liveTrips = const [],
    this.pendingTrips = const [],
    this.livePositions = const [],
    this.maintenance,
    this.alerts,
    this.alertEvents = const [],
    this.fuelAnalytics,
    this.driverStats,
    this.compliance,
    this.activities = const [],
    this.aiItems = const [],
    this.attentionVehicles = const [],
    this.healthSummary,
    this.unreadNotifications = 0,
    this.sectionErrors = const {},
  });

  final DashboardRole role;
  final String displayName;
  final int? tenantId;
  final DateTime? lastSyncedAt;
  final List<DashboardWidgetId> widgets;
  final List<DashboardQuickAction> quickActions;
  final List<KpiCell> primaryKpis;
  final DriverDashboardSummary? driver;
  final FleetOpsDashboard? fleet;
  final GpsFleetStatusKpis? gps;
  final OpsTripsDashboard? trips;
  final List<OpsTripListItem> liveTrips;
  final List<OpsTripListItem> pendingTrips;
  final List<GpsPosition> livePositions;
  final MaintenanceKpis? maintenance;
  final GpsAlertStats? alerts;
  final List<GpsAlertEvent> alertEvents;
  final FuelAnalyticsSummary? fuelAnalytics;
  final DriverStats? driverStats;
  final ComplianceSummary? compliance;
  final List<ActivityItem> activities;
  final List<AiAttentionItem> aiItems;
  final List<VehicleListItem> attentionVehicles;

  /// One-line AI / rule summary under Fleet Health.
  final String? healthSummary;
  final int unreadNotifications;

  /// Widget / section id → error message for soft failure UI.
  final Map<String, String> sectionErrors;

  bool get isDriver => role == DashboardRole.driver;

  /// Fleet health %: GPS online rate blended with offline + open critical alerts
  /// and maintenance due pressure (client composition; no dedicated health API).
  double get fleetHealthPercent {
    final g = gps;
    final total = g?.totalVehicles ?? fleet?.totalVehicles ?? 0;
    if (total <= 0) return 0;
    final online = g?.online ?? fleet?.activeVehicles ?? 0;
    var score = 100.0 * online / total;
    final offline = g?.offline ?? 0;
    if (offline > 0) {
      score -= (offline / total) * 25;
    }
    final critical = alerts?.critical ?? 0;
    if (critical > 0) {
      score -= (critical.clamp(0, 5)) * 4.0;
    }
    final due = fleet?.maintenanceDue ?? maintenance?.dueForService ?? 0;
    if (due > 0) {
      score -= (due.clamp(0, 10)) * 1.5;
    }
    return score.clamp(0, 100);
  }

  String get healthLabel {
    final p = fleetHealthPercent;
    if (p >= 85) return 'Good';
    if (p >= 70) return 'Healthy';
    if (p >= 50) return 'Fair';
    if (p > 0) return 'At risk';
    return 'Unknown';
  }

  /// Present ≈ on-duty; absent ≈ total − on-duty (approximation).
  int get attendancePresent {
    if (driverStats != null) return driverStats!.active;
    return fleet?.driversOnDuty ?? 0;
  }

  int get attendanceTotal {
    if (driverStats != null && driverStats!.totalDrivers > 0) {
      return driverStats!.totalDrivers;
    }
    final present = attendancePresent;
    return present > 0 ? present : 0;
  }

  /// Whether [id] should render given session permissions + loaded data.
  bool shouldShow(DashboardWidgetId id, FleetSession? session) {
    if (session == null) {
      return id == DashboardWidgetId.greeting ||
          id == DashboardWidgetId.opsHeader;
    }

    if (!DashboardVisibility.roleAllows(role, id)) return false;

    switch (id) {
      case DashboardWidgetId.greeting:
      case DashboardWidgetId.opsHeader:
      case DashboardWidgetId.platformBanner:
      case DashboardWidgetId.quickActions:
      case DashboardWidgetId.universalSearchBar:
        return true;
      case DashboardWidgetId.primaryKpis:
        return primaryKpis.isNotEmpty;
      case DashboardWidgetId.myVehicle:
      case DashboardWidgetId.driverTripKpis:
      case DashboardWidgetId.earnings:
        return driver != null;
      case DashboardWidgetId.fleetHealthHeader:
      case DashboardWidgetId.fleetStatsStrip:
      case DashboardWidgetId.opsKpiGrid:
      case DashboardWidgetId.fleetKpis:
      case DashboardWidgetId.fleetStatusStrip:
      case DashboardWidgetId.liveFleetCard:
      case DashboardWidgetId.liveMapPreview:
      case DashboardWidgetId.mapSummaryCard:
      case DashboardWidgetId.attentionVehicles:
        return DashboardVisibility.hasOpsDataPerms(session) ||
            fleet != null ||
            gps != null ||
            livePositions.isNotEmpty ||
            attentionVehicles.isNotEmpty ||
            trips != null;
      case DashboardWidgetId.maintenanceKpis:
      case DashboardWidgetId.maintenanceCost:
        return session.hasPermission(FleetPermissions.maintenanceView) ||
            maintenance != null;
      case DashboardWidgetId.fuelSummary:
      case DashboardWidgetId.fuelCost:
      case DashboardWidgetId.financeKpis:
        return session.hasPermission(FleetPermissions.fuelView) ||
            session.hasPermission(FleetPermissions.reportView) ||
            fleet != null ||
            fuelAnalytics != null;
      case DashboardWidgetId.recentAlerts:
      case DashboardWidgetId.criticalAlertsList:
        return session.hasPermission(FleetPermissions.gpsView) ||
            alerts != null ||
            alertEvents.isNotEmpty;
      case DashboardWidgetId.todayOpsKpis:
        return trips != null ||
            fleet != null ||
            maintenance != null ||
            driverStats != null ||
            fuelAnalytics != null;
      case DashboardWidgetId.recentActivities:
        return true;
      case DashboardWidgetId.tripKpis:
      case DashboardWidgetId.liveTripsPreview:
      case DashboardWidgetId.pendingAssignments:
        return session.hasPermission(FleetPermissions.tripView) ||
            session.hasPermission(FleetPermissions.bookingView) ||
            trips != null;
      case DashboardWidgetId.driverKpis:
      case DashboardWidgetId.driverPerformance:
        return session.hasPermission(FleetPermissions.driverView) ||
            driverStats != null;
      case DashboardWidgetId.complianceDocs:
        return session.hasPermission(FleetPermissions.vehicleView) ||
            session.hasPermission(FleetPermissions.driverView) ||
            compliance != null ||
            driverStats != null;
      case DashboardWidgetId.aiAttention:
        return aiItems.isNotEmpty;
    }
  }
}

/// @Deprecated — use [RoleDashboardData] / [DriverDashboardSummary].
@Deprecated('Use RoleDashboardData')
enum DashboardViewKind { driver, staff }

/// @Deprecated — use [RoleDashboardData].
@Deprecated('Use RoleDashboardData')
class DashboardSummary {
  const DashboardSummary({
    required this.kind,
    required this.assignedTripsToday,
    required this.completedToday,
    required this.clockedIn,
    this.currentVehicle,
    this.currentVehiclePlate,
    required this.earningsThisWeek,
    required this.unreadNotifications,
    required this.driverStatus,
    this.movingVehicles = 0,
    this.offlineVehicles = 0,
    this.maintenanceDue = 0,
    this.complianceAlerts = 0,
  });

  final DashboardViewKind kind;
  final int assignedTripsToday;
  final int completedToday;
  final bool clockedIn;
  final String? currentVehicle;
  final String? currentVehiclePlate;
  final double earningsThisWeek;
  final int unreadNotifications;
  final String driverStatus;
  final int movingVehicles;
  final int offlineVehicles;
  final int maintenanceDue;
  final int complianceAlerts;

  bool get isStaffView => kind == DashboardViewKind.staff;

  factory DashboardSummary.fromDriverJson(Map<String, dynamic> json) {
    final d = DriverDashboardSummary.fromJson(json);
    return DashboardSummary(
      kind: DashboardViewKind.driver,
      assignedTripsToday: d.assignedTripsToday,
      completedToday: d.completedToday,
      clockedIn: d.clockedIn,
      currentVehicle: d.currentVehicle,
      currentVehiclePlate: d.currentVehiclePlate,
      earningsThisWeek: d.earningsThisWeek,
      unreadNotifications: d.unreadNotifications,
      driverStatus: d.driverStatus,
    );
  }

  static DashboardSummary empty(
          {DashboardViewKind kind = DashboardViewKind.driver}) =>
      DashboardSummary(
        kind: kind,
        assignedTripsToday: 0,
        completedToday: 0,
        clockedIn: false,
        earningsThisWeek: 0,
        unreadNotifications: 0,
        driverStatus:
            kind == DashboardViewKind.staff ? 'No GPS data' : 'Available',
      );
}
