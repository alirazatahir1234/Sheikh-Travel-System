import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../alerts/domain/gps_alert_models.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../../compliance/domain/compliance_models.dart';
import '../../drivers/domain/driver_models.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../maintenance/domain/maintenance_models.dart';
import '../../notifications/domain/notification_models.dart';
import '../../gps_operator/domain/operator_dashboard_models.dart';
import '../../ops_trips/domain/ops_trip_models.dart';
import '../domain/dashboard_layout.dart';
import '../domain/dashboard_models.dart';
import '../domain/dashboard_role.dart';
import '../presentation/dashboard_layout_registry.dart';

final dashboardApiProvider = Provider<DashboardApi>(
  (ref) => DashboardApi(
    ref.read(dioProvider),
    () => ref.read(fleetSessionProvider),
  ),
);

class DashboardApi {
  DashboardApi(this._dio, this._session);

  final Dio _dio;
  final FleetSession? Function() _session;

  Future<RoleDashboardData> getRoleDashboard() async {
    final session = _session();
    final displayName = session?.displayName.split(' ').first ?? 'User';
    final tenantId = session?.tenantId;
    final role = session == null
        ? DashboardRole.driver
        : DashboardRoleX.fromNavRole(session.primaryNavRole);

    final overrideKeys = session?.companyContext?.dashboard?.widgetKeys;
    final widgets = DashboardLayoutRegistry.widgetsFor(
      role,
      overrideKeys: overrideKeys,
    );
    final quickActions = DashboardLayoutRegistry.quickActionsFor(role);

    if (role == DashboardRole.driver ||
        session == null ||
        session.isDriverOnly) {
      return _buildDriver(displayName, tenantId);
    }

    switch (role) {
      case DashboardRole.dispatcher:
        return _buildDispatcher(displayName, tenantId, widgets, quickActions);
      case DashboardRole.driverManager:
        return _buildDriverManager(
            displayName, tenantId, widgets, quickActions);
      case DashboardRole.accountant:
        return _buildAccountant(displayName, tenantId, widgets, quickActions);
      case DashboardRole.fleetManager:
      case DashboardRole.tenantAdmin:
      case DashboardRole.superAdmin:
        return _buildFleetOps(
            role, displayName, tenantId, widgets, quickActions);
      case DashboardRole.gpsOperator:
        return _buildGpsOperator(displayName, tenantId, widgets, quickActions);
      case DashboardRole.driver:
        return _buildDriver(displayName, tenantId);
    }
  }

  Future<RoleDashboardData> _buildDriver(
    String displayName,
    int? tenantId,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'driver', () => _getDriverSummary(), DriverDashboardSummary.empty, errors),
      _safeTagged(
          'timeline', () => _getDriverTimeline(), <ActivityItem>[], errors),
      _safeTagged('notifications', () => _getDriverNotifications(),
          <AppNotification>[], errors),
    ]);
    final driver = results[0] as DriverDashboardSummary;
    final timeline = results[1] as List<ActivityItem>;
    final notifs = results[2] as List<AppNotification>;
    final fromNotifs = notifs
        .map(
          (n) => ActivityItem(
            at: n.createdAt,
            title: n.title.isNotEmpty ? n.title : n.type,
            subtitle: n.message,
            kind: 'notification',
            route: '/notifications',
          ),
        )
        .toList();
    final activities = [...timeline, ...fromNotifs]
      ..sort((a, b) => b.at.compareTo(a.at));

    return RoleDashboardData(
      role: DashboardRole.driver,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: DashboardLayoutRegistry.widgetsFor(
        DashboardRole.driver,
        overrideKeys: _session()?.companyContext?.dashboard?.widgetKeys,
      ),
      quickActions:
          DashboardLayoutRegistry.quickActionsFor(DashboardRole.driver),
      primaryKpis: _driverPrimaryKpis(driver),
      driver: driver,
      activities: activities.take(6).toList(),
      unreadNotifications: driver.unreadNotifications,
      aiItems: _driverAi(driver),
      sectionErrors: errors,
    );
  }

  Future<RoleDashboardData> _buildFleetOps(
    DashboardRole role,
    String displayName,
    int? tenantId,
    List<DashboardWidgetId> widgets,
    List<DashboardQuickAction> quickActions,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'fleet', () => _getFleetOps(), FleetOpsDashboard.empty, errors),
      _safeTagged('gps', () => _getGps(), GpsFleetStatusKpis.empty, errors),
      _safeTagged('maintenance', () => _getMaintenance(), MaintenanceKpis.empty,
          errors),
      _safeTagged(
          'alerts', () => _getAlertStats(), GpsAlertStats.empty, errors),
      _safeTagged(
          'alertEvents', () => _getAlertEvents(), <GpsAlertEvent>[], errors),
      _safeTagged('live', () => _getLivePositions(), <GpsPosition>[], errors),
      _safeTagged(
          'trips', () => _getTripsDashboard(), OpsTripsDashboard.empty, errors),
      _safeTagged('fuel', () => _getFuelAnalytics(), FuelAnalyticsSummary.empty,
          errors),
      _safeTagged(
          'drivers', () => _getDriverStats(), DriverStats.empty, errors),
      _safeTagged('notifications', () => _getRecentNotifications(),
          <AppNotification>[], errors),
      _safeTagged('unread', () => _getUnread(), 0, errors),
      _safeTagged(
          'vehicles', () => _getVehicles(), <VehicleListItem>[], errors),
    ]);

    final fleet = results[0] as FleetOpsDashboard;
    final gps = results[1] as GpsFleetStatusKpis;
    final maintenance = results[2] as MaintenanceKpis;
    final alerts = results[3] as GpsAlertStats;
    final alertEvents = results[4] as List<GpsAlertEvent>;
    final live = results[5] as List<GpsPosition>;
    final trips = results[6] as OpsTripsDashboard;
    final fuel = results[7] as FuelAnalyticsSummary;
    final drivers = results[8] as DriverStats;
    final notifs = results[9] as List<AppNotification>;
    final unread = results[10] as int;
    final vehicles = results[11] as List<VehicleListItem>;

    final filteredAlerts = _filterAlertsForRole(role, alertEvents);
    final snapshotGps = _normalizeGpsSnapshot(gps, fleet);
    final normalizedFleet = FleetOpsDashboard(
      totalVehicles: snapshotGps.totalVehicles,
      activeVehicles: snapshotGps.moving,
      driversOnDuty: fleet.driversOnDuty,
      maintenanceDue: fleet.maintenanceDue,
      monthlyFuelCost: fleet.monthlyFuelCost,
      complianceAlerts: fleet.complianceAlerts,
    );
    final criticalFromEvents = filteredAlerts
        .where(
          (e) =>
              e.isOpen &&
              (e.severity.toLowerCase() == 'critical' ||
                  e.severity.toLowerCase() == 'high'),
        )
        .length;
    final normalizedAlerts = GpsAlertStats(
      total: alerts.total,
      today: alerts.today,
      unread: alerts.unread,
      active: alerts.active,
      resolved: alerts.resolved,
      critical: criticalFromEvents,
      archived: alerts.archived,
    );
    final attention = _attentionVehicles(role, vehicles, live);
    final healthBlurb = _healthSummaryText(
      RoleDashboardData(
        role: role,
        displayName: displayName,
        widgets: widgets,
        quickActions: quickActions,
        fleet: normalizedFleet,
        gps: snapshotGps,
        maintenance: maintenance,
        alerts: normalizedAlerts,
      ),
    );

    return RoleDashboardData(
      role: role,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: List.from(widgets),
      quickActions: List.from(quickActions),
      primaryKpis: role == DashboardRole.fleetManager
          ? _fleetManagerPrimaryKpis(
              normalizedFleet, snapshotGps, normalizedAlerts)
          : _tenantAdminPrimaryKpis(
              normalizedFleet, snapshotGps, normalizedAlerts),
      fleet: normalizedFleet,
      gps: snapshotGps,
      maintenance: maintenance,
      alerts: normalizedAlerts,
      alertEvents: filteredAlerts,
      livePositions: live,
      trips: trips,
      fuelAnalytics: fuel,
      driverStats: drivers,
      attentionVehicles: attention,
      healthSummary: healthBlurb,
      activities: _mergeActivities(notifs, alertEvents),
      unreadNotifications: unread,
      aiItems: role == DashboardRole.fleetManager
          ? _fleetAi(fleet, gps, maintenance, alerts, fuel)
          : role == DashboardRole.superAdmin
              ? _superAdminAi(fleet, gps, maintenance, alerts)
              : _tenantAdminAi(fleet, gps, trips, maintenance, alerts, fuel),
      sectionErrors: errors,
    );
  }

  Future<RoleDashboardData> _buildGpsOperator(
    String displayName,
    int? tenantId,
    List<DashboardWidgetId> widgets,
    List<DashboardQuickAction> quickActions,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'operatorSummary',
          () => _getOperatorSummary(),
          GpsOperatorSummary.empty,
          errors),
      _safeTagged(
          'liveTrips', () => _getLiveTrips(), <OpsTripListItem>[], errors),
      _safeTagged(
          'alertEvents', () => _getAlertEvents(), <GpsAlertEvent>[], errors),
      _safeTagged(
          'alerts', () => _getAlertStats(), GpsAlertStats.empty, errors),
      _safeTagged('live', () => _getLivePositions(), <GpsPosition>[], errors),
      _safeTagged('notifications', () => _getRecentNotifications(),
          <AppNotification>[], errors),
      _safeTagged('unread', () => _getUnread(), 0, errors),
    ]);

    final summary = results[0] as GpsOperatorSummary;
    final liveTrips = results[1] as List<OpsTripListItem>;
    final alertEvents = results[2] as List<GpsAlertEvent>;
    final alerts = results[3] as GpsAlertStats;
    final live = results[4] as List<GpsPosition>;
    final notifs = results[5] as List<AppNotification>;
    final unread = results[6] as int;

    final openAlerts =
        _filterAlertsForRole(DashboardRole.gpsOperator, alertEvents);

    final gps = GpsFleetStatusKpis(
      totalVehicles: summary.totalVehicles,
      online: summary.online,
      offline: summary.offline,
      moving: summary.moving,
      idle: summary.idle,
      parked: summary.parked,
      neverSeen: summary.neverSeen,
      sos: summary.sos,
      alertsToday: summary.alertsToday,
    );

    return RoleDashboardData(
      role: DashboardRole.gpsOperator,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: List.from(widgets),
      quickActions: List.from(quickActions),
      primaryKpis: _gpsOperatorPrimaryKpis(summary),
      gps: gps,
      alerts: alerts,
      alertEvents: openAlerts,
      livePositions: live,
      liveTrips: liveTrips.take(6).toList(),
      operatorSummary: summary,
      activities: _mergeActivities(notifs, alertEvents),
      unreadNotifications: unread,
      sectionErrors: errors,
    );
  }

  List<KpiCell> _gpsOperatorPrimaryKpis(GpsOperatorSummary s) => [
        KpiCell(label: 'Online', value: '${s.online}', route: '/fleet'),
        KpiCell(label: 'Moving', value: '${s.moving}', route: '/fleet/map'),
        KpiCell(label: 'Alerts', value: '${s.alertsToday}', route: '/alerts'),
        KpiCell(label: 'Offline', value: '${s.offline}', route: '/fleet'),
      ];

  Future<GpsOperatorSummary> _getOperatorSummary() async {
    final res = await _dio.get(ApiEndpoints.gpsOperatorSummary);
    return GpsOperatorSummary.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<RoleDashboardData> _buildDispatcher(
    String displayName,
    int? tenantId,
    List<DashboardWidgetId> widgets,
    List<DashboardQuickAction> quickActions,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'trips', () => _getTripsDashboard(), OpsTripsDashboard.empty, errors),
      _safeTagged(
          'liveTrips', () => _getLiveTrips(), <OpsTripListItem>[], errors),
      _safeTagged(
          'pending', () => _getPendingTrips(), <OpsTripListItem>[], errors),
      _safeTagged('live', () => _getLivePositions(), <GpsPosition>[], errors),
      _safeTagged(
          'alertEvents', () => _getAlertEvents(), <GpsAlertEvent>[], errors),
      _safeTagged(
          'alerts', () => _getAlertStats(), GpsAlertStats.empty, errors),
      _safeTagged('notifications', () => _getRecentNotifications(),
          <AppNotification>[], errors),
      _safeTagged('unread', () => _getUnread(), 0, errors),
      _safeTagged('gps', () => _getGps(), GpsFleetStatusKpis.empty, errors),
      _safeTagged(
          'drivers', () => _getDriverStats(), DriverStats.empty, errors),
      _safeTagged(
          'fleet', () => _getFleetOps(), FleetOpsDashboard.empty, errors),
    ]);

    final trips = results[0] as OpsTripsDashboard;
    final liveTrips = results[1] as List<OpsTripListItem>;
    final pending = results[2] as List<OpsTripListItem>;
    final live = results[3] as List<GpsPosition>;
    final alertEvents = results[4] as List<GpsAlertEvent>;
    final alerts = results[5] as GpsAlertStats;
    final notifs = results[6] as List<AppNotification>;
    final unread = results[7] as int;
    final gps = results[8] as GpsFleetStatusKpis;
    final drivers = results[9] as DriverStats;
    final fleet = results[10] as FleetOpsDashboard;

    final openAlerts =
        _filterAlertsForRole(DashboardRole.dispatcher, alertEvents);

    return RoleDashboardData(
      role: DashboardRole.dispatcher,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: List.from(widgets),
      quickActions: List.from(quickActions),
      primaryKpis: _dispatcherPrimaryKpis(trips, pending),
      trips: trips,
      liveTrips: liveTrips.take(5).toList(),
      pendingTrips: pending.take(5).toList(),
      livePositions: live,
      gps: gps,
      fleet: fleet,
      driverStats: drivers,
      alerts: alerts,
      alertEvents: openAlerts,
      activities: _mergeActivities(notifs, alertEvents),
      unreadNotifications: unread,
      aiItems: _dispatchAi(trips, pending, alerts),
      sectionErrors: errors,
    );
  }

  Future<RoleDashboardData> _buildDriverManager(
    String displayName,
    int? tenantId,
    List<DashboardWidgetId> widgets,
    List<DashboardQuickAction> quickActions,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'drivers', () => _getDriverStats(), DriverStats.empty, errors),
      _safeTagged('compliance', () => _getCompliance(), ComplianceSummary.empty,
          errors),
      _safeTagged(
          'fleet', () => _getFleetOps(), FleetOpsDashboard.empty, errors),
      _safeTagged('notifications', () => _getRecentNotifications(),
          <AppNotification>[], errors),
      _safeTagged(
          'alertEvents', () => _getAlertEvents(), <GpsAlertEvent>[], errors),
      _safeTagged('unread', () => _getUnread(), 0, errors),
    ]);

    final stats = results[0] as DriverStats;
    final compliance = results[1] as ComplianceSummary;
    final fleet = results[2] as FleetOpsDashboard;
    final notifs = results[3] as List<AppNotification>;
    final alertEvents = results[4] as List<GpsAlertEvent>;
    final unread = results[5] as int;

    return RoleDashboardData(
      role: DashboardRole.driverManager,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: List.from(widgets),
      quickActions: List.from(quickActions),
      primaryKpis: _driverManagerPrimaryKpis(stats),
      driverStats: stats,
      compliance: compliance,
      fleet: fleet,
      activities: _mergeActivities(notifs, alertEvents),
      unreadNotifications: unread,
      aiItems: _driverManagerAi(stats, compliance),
      sectionErrors: errors,
    );
  }

  Future<RoleDashboardData> _buildAccountant(
    String displayName,
    int? tenantId,
    List<DashboardWidgetId> widgets,
    List<DashboardQuickAction> quickActions,
  ) async {
    final errors = <String, String>{};
    final results = await Future.wait([
      _safeTagged(
          'fleet', () => _getFleetOps(), FleetOpsDashboard.empty, errors),
      _safeTagged('maintenance', () => _getMaintenance(), MaintenanceKpis.empty,
          errors),
      _safeTagged('fuel', () => _getFuelAnalytics(), FuelAnalyticsSummary.empty,
          errors),
      _safeTagged('notifications', () => _getRecentNotifications(),
          <AppNotification>[], errors),
      _safeTagged('unread', () => _getUnread(), 0, errors),
    ]);

    final fleet = results[0] as FleetOpsDashboard;
    final maintenance = results[1] as MaintenanceKpis;
    final fuel = results[2] as FuelAnalyticsSummary;
    final notifs = results[3] as List<AppNotification>;
    final unread = results[4] as int;

    return RoleDashboardData(
      role: DashboardRole.accountant,
      displayName: displayName,
      tenantId: tenantId,
      lastSyncedAt: DateTime.now(),
      widgets: List.from(widgets),
      quickActions: List.from(quickActions),
      primaryKpis: _accountantPrimaryKpis(fleet, maintenance, fuel, unread),
      fleet: fleet,
      maintenance: maintenance,
      fuelAnalytics: fuel,
      activities: _mergeActivities(notifs, const []),
      unreadNotifications: unread,
      aiItems: _accountantAi(fleet, maintenance, fuel),
      sectionErrors: errors,
    );
  }

  Future<DriverDashboardSummary> _getDriverSummary() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverDashboard);
    final body = res.data;
    if (body == null) return DriverDashboardSummary.empty;
    return DriverDashboardSummary.fromJson(ApiResponseParser.dataMap(body));
  }

  Future<FleetOpsDashboard> _getFleetOps() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.fleetDashboard);
    return FleetOpsDashboard.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<GpsFleetStatusKpis> _getGps() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.gpsFleetStatusLocal);
    return GpsFleetStatusKpis.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<MaintenanceKpis> _getMaintenance() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.maintenanceDashboard);
    final map = ApiResponseParser.dataMap(res.data);
    final kpis = map['kpis'] ?? map['Kpis'] ?? map;
    if (kpis is Map) {
      return MaintenanceKpis.fromJson(Map<String, dynamic>.from(kpis));
    }
    return MaintenanceKpis.fromJson(map);
  }

  Future<GpsAlertStats> _getAlertStats() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.gpsAlertStats);
    return GpsAlertStats.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<GpsAlertEvent>> _getAlertEvents() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertEvents,
      queryParameters: {'unacknowledgedOnly': false},
    );
    final body = res.data;
    ApiResponseParser.ensureSuccess(body);
    final data = body?['data'];
    List<GpsAlertEvent> events;
    if (data is List) {
      events = data
          .whereType<Map>()
          .map((e) => GpsAlertEvent.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    } else {
      events = ApiResponseParser.pagedItems(body)
          .map(GpsAlertEvent.fromJson)
          .toList();
    }
    events.sort((a, b) => b.timestamp.compareTo(a.timestamp));
    return events.take(20).toList();
  }

  Future<List<GpsPosition>> _getLivePositions() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsLive,
      queryParameters: {'page': 1, 'pageSize': 100},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(GpsPosition.fromJson)
        .toList();
  }

  Future<List<VehicleListItem>> _getVehicles() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicles,
      queryParameters: {'page': 1, 'pageSize': 50},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(VehicleListItem.fromJson)
        .toList();
  }

  GpsFleetStatusKpis _normalizeGpsSnapshot(
    GpsFleetStatusKpis gps,
    FleetOpsDashboard fleet,
  ) {
    final total = gps.totalVehicles > 0 ? gps.totalVehicles : fleet.totalVehicles;
    final moving = gps.moving < 0 ? 0 : gps.moving;
    final idle = (gps.idle + gps.parked + gps.sos).clamp(0, 1 << 30);
    final neverSeen = gps.neverSeen < 0 ? 0 : gps.neverSeen;
    // Prefer API "online" (fresh GPS = moving+idle). Fall back to moving+idle if API omitted it.
    final onlineFromApi = gps.online;
    final online = onlineFromApi > 0
        ? onlineFromApi
        : (moving + idle);
    var offline = total - online - neverSeen;
    if (offline < 0) {
      offline = gps.offline < 0 ? 0 : gps.offline;
    }
    return GpsFleetStatusKpis(
      totalVehicles: total < 0 ? 0 : total,
      online: online,
      offline: offline,
      moving: moving,
      idle: idle,
      parked: 0,
      neverSeen: neverSeen,
      sos: 0,
      alertsToday: gps.alertsToday < 0 ? 0 : gps.alertsToday,
    );
  }

  List<GpsAlertEvent> _filterAlertsForRole(
    DashboardRole role,
    List<GpsAlertEvent> events,
  ) {
    final open = events.where((e) => e.isOpen).toList();
    bool match(GpsAlertEvent e) {
      final sev = e.severity.toLowerCase();
      final type = e.eventType.toLowerCase();
      switch (role) {
        case DashboardRole.tenantAdmin:
        case DashboardRole.superAdmin:
          return sev.contains('critical') ||
              sev.contains('high') ||
              type.contains('business');
        case DashboardRole.fleetManager:
        case DashboardRole.gpsOperator:
          return type.contains('overspeed') ||
              type.contains('offline') ||
              type.contains('geofence') ||
              type.contains('ignition') ||
              type.contains('battery') ||
              type.contains('sos') ||
              type.contains('maint') ||
              sev.contains('critical') ||
              sev.contains('high') ||
              sev.contains('warning');
        case DashboardRole.dispatcher:
          return type.contains('delay') ||
              type.contains('route') ||
              type.contains('deviation') ||
              type.contains('late') ||
              type.contains('assignment') ||
              type.contains('trip') ||
              sev.contains('critical') ||
              sev.contains('high');
        default:
          return true;
      }
    }

    final filtered = open.where(match).toList();
    final list = filtered.isNotEmpty ? filtered : open;
    return list.take(6).toList();
  }

  List<VehicleListItem> _attentionVehicles(
    DashboardRole role,
    List<VehicleListItem> vehicles,
    List<GpsPosition> live,
  ) {
    final byId = {for (final p in live) p.vehicleId: p};
    final scored = <({VehicleListItem v, int score})>[];
    for (final v in vehicles) {
      if (v.isRetired) continue;
      var score = 0;
      if (!v.gpsOnline) score += 3;
      if (v.serviceAlert != null && v.serviceAlert!.isNotEmpty) score += 4;
      final st = v.status.toLowerCase();
      if (st.contains('maint') || st.contains('breakdown')) score += 5;
      final pos = byId[v.id];
      if (pos != null && pos.speed > 100) score += 2;
      if (score > 0) scored.add((v: v, score: score));
    }
    scored.sort((a, b) => b.score.compareTo(a.score));
    final top = scored.map((e) => e.v).take(8).toList();
    if (top.isNotEmpty) return top;
    return vehicles.where((v) => !v.isRetired).take(6).toList();
  }

  String _healthSummaryText(RoleDashboardData data) {
    final offline = data.gps?.offline ?? 0;
    final due =
        data.fleet?.maintenanceDue ?? data.maintenance?.dueForService ?? 0;
    final critical = data.alerts?.critical ?? 0;
    final parts = <String>[
      'Fleet health is ${data.healthLabel.toLowerCase()}.',
    ];
    if (due > 0) {
      parts.add(
          '$due vehicle${due == 1 ? '' : 's'} require maintenance.');
    }
    if (offline > 0) {
      parts.add(
          '$offline vehicle${offline == 1 ? '' : 's'} offline.');
    }
    if (critical > 0) {
      parts.add('$critical critical alert${critical == 1 ? '' : 's'}.');
    } else {
      parts.add('No critical issues.');
    }
    return parts.join(' ');
  }

  /// Universal search across vehicles, drivers, trips, bookings.
  Future<List<DashboardSearchHit>> searchUniversal(String query) async {
    final q = query.trim();
    if (q.length < 2) return const [];
    final results = await Future.wait([
      _searchVehicles(q),
      _searchDrivers(q),
      _searchTrips(q),
      _searchBookings(q),
    ]);
    return [
      ...results[0],
      ...results[1],
      ...results[2],
      ...results[3],
    ].take(40).toList();
  }

  Future<List<DashboardSearchHit>> _searchVehicles(String q) async {
    try {
      final items = await _getVehicles();
      final lq = q.toLowerCase();
      return items
          .where(
            (v) =>
                v.name.toLowerCase().contains(lq) ||
                v.registrationNumber.toLowerCase().contains(lq) ||
                (v.driverName?.toLowerCase().contains(lq) ?? false),
          )
          .take(10)
          .map(
            (v) => DashboardSearchHit(
              kind: 'vehicle',
              title: v.name,
              subtitle: v.registrationNumber,
              route: '/fleet/vehicles/${v.id}',
            ),
          )
          .toList();
    } catch (_) {
      return const [];
    }
  }

  Future<List<DashboardSearchHit>> _searchDrivers(String q) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.drivers,
        queryParameters: {'page': 1, 'pageSize': 30, 'q': q},
      );
      return ApiResponseParser.pagedItems(res.data)
          .map(DriverListItem.fromJson)
          .take(10)
          .map(
            (d) => DashboardSearchHit(
              kind: 'driver',
              title: d.fullName,
              subtitle: d.phone,
              route: '/more/drivers/${d.id}',
            ),
          )
          .toList();
    } catch (_) {
      return const [];
    }
  }

  Future<List<DashboardSearchHit>> _searchTrips(String q) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.opsTrips,
        queryParameters: {'page': 1, 'pageSize': 30, 'q': q},
      );
      final lq = q.toLowerCase();
      return ApiResponseParser.pagedItems(res.data)
          .map(OpsTripListItem.fromJson)
          .where(
            (t) =>
                t.tripNumber.toLowerCase().contains(lq) ||
                (t.customerName?.toLowerCase().contains(lq) ?? false) ||
                (t.driverName?.toLowerCase().contains(lq) ?? false),
          )
          .take(10)
          .map(
            (t) => DashboardSearchHit(
              kind: 'trip',
              title: t.tripNumber,
              subtitle: t.customerName ?? t.status,
              route: '/trips/${t.id}',
            ),
          )
          .toList();
    } catch (_) {
      return const [];
    }
  }

  Future<List<DashboardSearchHit>> _searchBookings(String q) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.bookings,
        queryParameters: {'page': 1, 'pageSize': 30},
      );
      final lq = q.toLowerCase();
      final items = ApiResponseParser.pagedItems(res.data);
      return items
          .where((raw) {
            final n =
                (raw['bookingNumber'] ?? raw['BookingNumber'] ?? '').toString();
            final c =
                (raw['customerName'] ?? raw['CustomerName'] ?? '').toString();
            return n.toLowerCase().contains(lq) ||
                c.toLowerCase().contains(lq);
          })
          .take(10)
          .map((raw) {
            final id = raw['id'] as int? ?? raw['Id'] as int? ?? 0;
            final n =
                (raw['bookingNumber'] ?? raw['BookingNumber'] ?? 'Booking')
                    .toString();
            final c =
                (raw['customerName'] ?? raw['CustomerName'] ?? '').toString();
            return DashboardSearchHit(
              kind: 'booking',
              title: n,
              subtitle: c,
              route: '/bookings/$id',
            );
          })
          .toList();
    } catch (_) {
      return const [];
    }
  }

  Future<FuelAnalyticsSummary> _getFuelAnalytics() async {
    final now = DateTime.now().toUtc();
    final from = DateTime.utc(now.year, now.month, 1);
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsFuelAnalytics,
      queryParameters: {
        'from': from.toIso8601String(),
        'to': now.toIso8601String(),
      },
    );
    return FuelAnalyticsSummary.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<OpsTripsDashboard> _getTripsDashboard() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.opsTripsDashboard);
    return OpsTripsDashboard.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<OpsTripListItem>> _getLiveTrips() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.opsTripsLive,
      queryParameters: {'todayOnly': true},
    );
    final body = res.data;
    ApiResponseParser.ensureSuccess(body);
    final data = body?['data'];
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => OpsTripListItem.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }
    return ApiResponseParser.pagedItems(body)
        .map(OpsTripListItem.fromJson)
        .toList();
  }

  Future<List<OpsTripListItem>> _getPendingTrips() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.opsTrips,
      queryParameters: {
        'page': 1,
        'pageSize': 20,
        'todayOnly': true,
        'status': 'Scheduled',
      },
    );
    final all =
        ApiResponseParser.pagedItems(res.data).map(OpsTripListItem.fromJson);
    return all.where((t) => t.driverId == null || t.vehicleId == null).toList();
  }

  Future<DriverStats> _getDriverStats() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driversStats);
    return DriverStats.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<ComplianceSummary> _getCompliance() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.maintenanceComplianceSummary,
      );
      return ComplianceSummary.fromJson(ApiResponseParser.dataMap(res.data));
    } catch (_) {
      return ComplianceSummary.empty;
    }
  }

  Future<List<AppNotification>> _getRecentNotifications() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.staffNotifications,
      queryParameters: {
        'page': 1,
        'pageSize': 15,
        'archived': false,
      },
    );
    final data = res.data?['data'];
    List list;
    if (data is Map<String, dynamic>) {
      list = (data['items'] as List?) ?? [];
    } else if (data is List) {
      list = data;
    } else {
      list = [];
    }
    return list
        .whereType<Map>()
        .map((e) => AppNotification.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<int> _getUnread() async {
    try {
      final unreadRes = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.staffNotificationsUnreadCount,
      );
      final data = unreadRes.data?['data'];
      if (data is int) return data;
      if (data is num) return data.toInt();
      if (data is Map) {
        final map = Map<String, dynamic>.from(data);
        return (map['count'] as num?)?.toInt() ??
            (map['unreadCount'] as num?)?.toInt() ??
            0;
      }
    } catch (_) {}
    return 0;
  }

  List<ActivityItem> _mergeActivities(
    List<AppNotification> notifs,
    List<GpsAlertEvent> alerts,
  ) {
    final items = <ActivityItem>[
      ...notifs.map(
        (n) => ActivityItem(
          at: n.createdAt,
          title: n.title.isNotEmpty ? n.title : n.type,
          subtitle: n.message,
          kind: _activityKindFromNotification(n),
          route: '/notifications',
        ),
      ),
      ...alerts.take(10).map(
            (a) => ActivityItem(
              at: a.timestamp,
              title: a.eventType.isNotEmpty ? a.eventType : 'GPS Alert',
              subtitle: [
                if (a.vehicleName != null && a.vehicleName!.isNotEmpty)
                  a.vehicleName!,
                if (a.speed > 0) '${a.speed.toStringAsFixed(0)} km/h',
                if (a.geofenceName != null) a.geofenceName!,
              ].where((s) => s.isNotEmpty).join(' · '),
              kind: 'alert',
              route: '/alerts',
            ),
          ),
    ];
    items.sort((a, b) => b.at.compareTo(a.at));
    return items.take(6).toList();
  }

  String _activityKindFromNotification(AppNotification n) {
    final t = '${n.type} ${n.module ?? ''}'.toLowerCase();
    if (t.contains('trip') || t.contains('booking')) return 'trip';
    if (t.contains('maint') || t.contains('work')) return 'maintenance';
    if (t.contains('alert') || t.contains('gps')) return 'alert';
    return 'notification';
  }

  Future<List<ActivityItem>> _getDriverTimeline() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.timeline,
      queryParameters: {'page': 1, 'pageSize': 10},
    );
    final body = res.data;
    final list = (body?['data'] as List?) ?? (body?['items'] as List?) ?? [];
    return list.whereType<Map>().map((raw) {
      final json = Map<String, dynamic>.from(raw);
      final at = DateTime.tryParse(json['eventTime']?.toString() ?? '') ??
          DateTime.now();
      return ActivityItem(
        at: at,
        title: json['title'] as String? ?? json['eventType'] as String? ?? 'Event',
        subtitle: json['description'] as String? ?? '',
        kind: 'trip',
        route: '/timeline',
      );
    }).toList();
  }

  Future<List<AppNotification>> _getDriverNotifications() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.notifications,
      queryParameters: {
        'page': 1,
        'pageSize': 15,
        'archived': false,
      },
    );
    final data = res.data?['data'];
    List list;
    if (data is Map<String, dynamic>) {
      list = (data['items'] as List?) ?? [];
    } else if (data is List) {
      list = data;
    } else {
      list = [];
    }
    return list
        .whereType<Map>()
        .map((e) => AppNotification.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  List<KpiCell> _driverPrimaryKpis(DriverDashboardSummary d) {
    final remaining =
        (d.assignedTripsToday - d.completedToday).clamp(0, 999);
    return [
      KpiCell(
        label: "Today's Trips",
        value: '${d.assignedTripsToday}',
        colorKey: 'primary',
        route: '/trips',
      ),
      KpiCell(
        label: 'Completed',
        value: '${d.completedToday}',
        colorKey: 'success',
        route: '/trips',
      ),
      KpiCell(
        label: 'Remaining',
        value: '$remaining',
        colorKey: 'warning',
        route: '/trips',
      ),
      KpiCell(
        label: 'Earnings',
        value: d.earningsThisWeek >= 1000
            ? '${(d.earningsThisWeek / 1000).toStringAsFixed(0)}k'
            : d.earningsThisWeek.toStringAsFixed(0),
        colorKey: 'success',
        route: '/earnings',
      ),
    ];
  }

  List<KpiCell> _fleetManagerPrimaryKpis(
    FleetOpsDashboard fleet,
    GpsFleetStatusKpis gps,
    GpsAlertStats alerts,
  ) {
    final total = gps.totalVehicles <= 0 ? fleet.totalVehicles : gps.totalVehicles;
    final onlinePct = total <= 0 ? 0 : (100.0 * gps.online / total).round();
    return [
      KpiCell(
        label: 'Vehicles',
        value: '${total < 0 ? 0 : total}',
        colorKey: 'primary',
        route: '/fleet',
        subtitle: '${gps.online} Online',
      ),
      KpiCell(
        label: 'Drivers',
        value: '${fleet.driversOnDuty}',
        colorKey: 'success',
        route: '/more/drivers',
        subtitle: '${fleet.driversOnDuty} On Duty',
      ),
      KpiCell(
        label: 'Online',
        value: '${gps.online}',
        colorKey: 'info',
        route: '/fleet/map',
        subtitle: '$onlinePct% vs total',
      ),
      KpiCell(
        label: 'Active Alerts',
        value: '${alerts.critical > 0 ? alerts.critical : alerts.active}',
        colorKey: 'error',
        route: '/alerts',
        subtitle: 'Requires attention',
      ),
    ];
  }

  List<KpiCell> _tenantAdminPrimaryKpis(
    FleetOpsDashboard fleet,
    GpsFleetStatusKpis gps,
    GpsAlertStats alerts,
  ) {
    final total = gps.totalVehicles <= 0 ? fleet.totalVehicles : gps.totalVehicles;
    final onlinePct = total <= 0 ? 0 : (100.0 * gps.online / total).round();
    return [
      KpiCell(
        label: 'Vehicles',
        value: '${total < 0 ? 0 : total}',
        colorKey: 'primary',
        route: '/fleet',
        subtitle: '${gps.online} Online',
      ),
      KpiCell(
        label: 'Drivers',
        value: '${fleet.driversOnDuty}',
        colorKey: 'success',
        route: '/more/drivers',
        subtitle: '${fleet.driversOnDuty} On Duty',
      ),
      KpiCell(
        label: 'Online',
        value: '${gps.online}',
        colorKey: 'info',
        route: '/fleet/map',
        subtitle: '$onlinePct% vs total',
      ),
      KpiCell(
        label: 'Active Alerts',
        value: '${alerts.critical > 0 ? alerts.critical : alerts.active}',
        colorKey: 'error',
        route: '/alerts',
        subtitle: 'Requires attention',
      ),
    ];
  }

  List<KpiCell> _dispatcherPrimaryKpis(
    OpsTripsDashboard trips,
    List<OpsTripListItem> pending,
  ) {
    return [
      KpiCell(
        label: "Today's Trips",
        value: '${trips.total}',
        colorKey: 'primary',
        route: '/trips',
      ),
      KpiCell(
        label: 'In Progress',
        value: '${trips.inProgress}',
        colorKey: 'info',
        route: '/trips',
      ),
      KpiCell(
        label: 'Pending',
        value: '${pending.length}',
        colorKey: 'error',
        route: '/trips',
      ),
      KpiCell(
        label: 'Completed',
        value: '${trips.completed}',
        colorKey: 'success',
        route: '/trips',
      ),
    ];
  }

  List<KpiCell> _driverManagerPrimaryKpis(DriverStats stats) {
    final absent = (stats.totalDrivers - stats.active).clamp(0, 9999);
    return [
      KpiCell(
        label: 'Drivers',
        value: '${stats.totalDrivers}',
        colorKey: 'primary',
        route: '/more/drivers',
      ),
      KpiCell(
        label: 'Present',
        value: '${stats.active}',
        colorKey: 'success',
        route: '/more/drivers',
      ),
      KpiCell(
        label: 'Absent',
        value: '$absent',
        colorKey: 'error',
        route: '/more/drivers',
      ),
      KpiCell(
        label: 'Lic. soon',
        value: '${stats.licensesExpiringSoon}',
        colorKey: 'warning',
        route: '/documents',
      ),
      KpiCell(
        label: 'Lic. expired',
        value: '${stats.licensesExpired}',
        colorKey: 'error',
        route: '/documents',
      ),
    ];
  }

  List<KpiCell> _accountantPrimaryKpis(
    FleetOpsDashboard fleet,
    MaintenanceKpis maint,
    FuelAnalyticsSummary fuel,
    int unread,
  ) {
    return [
      KpiCell(
        label: 'Fuel (mo)',
        value: _shortMoney(fuel.totalCost > 0 ? fuel.totalCost : fleet.monthlyFuelCost),
        colorKey: 'warning',
        route: '/more/reports',
      ),
      KpiCell(
        label: 'Maint. (mo)',
        value: _shortMoney(maint.monthlyMaintenanceCost),
        colorKey: 'error',
        route: '/more/maintenance',
      ),
      KpiCell(
        label: 'Fuel today',
        value: _shortMoney(fuel.todayCost),
        colorKey: 'info',
        route: '/fuel',
      ),
      KpiCell(
        label: 'Inbox',
        value: '$unread',
        colorKey: 'primary',
        route: '/notifications',
      ),
    ];
  }

  String _shortMoney(double v) {
    if (v >= 1000000) return '${(v / 1000000).toStringAsFixed(1)}M';
    if (v >= 1000) return '${(v / 1000).toStringAsFixed(0)}k';
    return v.toStringAsFixed(0);
  }

  Future<void> setStatus(String status) async {
    final session = _session();
    if (session != null && !session.isDriverOnly) return;
    await _dio.post(ApiEndpoints.driverStatus, data: {'status': status});
  }

  Future<T> _safeTagged<T>(
    String key,
    Future<T> Function() fn,
    T fallback,
    Map<String, String> errors,
  ) async {
    try {
      return await fn();
    } catch (e) {
      errors[key] = e.toString();
      return fallback;
    }
  }

  List<AiAttentionItem> _driverAi(DriverDashboardSummary d) {
    final items = <AiAttentionItem>[];
    final remaining = (d.assignedTripsToday - d.completedToday).clamp(0, 999);
    if (d.assignedTripsToday == 0) {
      items.add(const AiAttentionItem(
        text: "No trips assigned yet — check today's schedule",
        route: '/trips',
        suggestedPrompt: 'What trips do I have today?',
        severity: 'info',
      ));
    } else if (remaining > 0) {
      items.add(AiAttentionItem(
        text: "Today's first remaining trip — $remaining left",
        route: '/trips',
        suggestedPrompt: 'What trips do I have left today?',
        severity: 'warning',
      ));
    }
    if (!d.clockedIn) {
      items.add(const AiAttentionItem(
        text: 'Attendance missing — clock in when ready',
        route: '/attendance',
        suggestedPrompt: 'Have I checked in today?',
        severity: 'warning',
      ));
    }
    if (d.unreadNotifications > 0) {
      items.add(AiAttentionItem(
        text: '${d.unreadNotifications} unread notification(s)',
        route: '/notifications',
        severity: 'info',
      ));
    }
    if (items.isEmpty) {
      items.add(const AiAttentionItem(
        text: 'All clear — ask AI for a day briefing',
        route: '/ai',
        suggestedPrompt: 'Summarize my day',
        severity: 'success',
      ));
    }
    return items.take(4).toList();
  }

  List<AiAttentionItem> _fleetAi(
    FleetOpsDashboard fleet,
    GpsFleetStatusKpis gps,
    MaintenanceKpis maint,
    GpsAlertStats alerts,
    FuelAnalyticsSummary fuel,
  ) {
    final items = <AiAttentionItem>[];
    if (gps.offline > 0) {
      items.add(AiAttentionItem(
        text: '${gps.offline} vehicle(s) offline',
        route: '/fleet/map',
        suggestedPrompt: 'Which vehicles are offline?',
        severity: 'critical',
      ));
    }
    final due = maint.overdueServices > 0
        ? maint.overdueServices
        : maint.dueForService;
    if (due > 0) {
      items.add(AiAttentionItem(
        text: '$due maintenance due',
        route: '/more/maintenance',
        suggestedPrompt: 'What maintenance is due today?',
        severity: 'warning',
      ));
    }
    if (alerts.critical > 0) {
      items.add(AiAttentionItem(
        text: '${alerts.critical} overspeed / critical alert(s)',
        route: '/alerts',
        suggestedPrompt: 'Show critical GPS alerts',
        severity: 'warning',
      ));
    }
    items.add(const AiAttentionItem(
      text: 'Fuel usage normal',
      route: '/more/reports',
      severity: 'success',
    ));
    if (fleet.complianceAlerts > 0) {
      items.add(AiAttentionItem(
        text: '${fleet.complianceAlerts} compliance alert(s)',
        route: '/documents',
        severity: 'warning',
      ));
    }
    return items.take(4).toList();
  }

  List<AiAttentionItem> _tenantAdminAi(
    FleetOpsDashboard fleet,
    GpsFleetStatusKpis gps,
    OpsTripsDashboard trips,
    MaintenanceKpis maint,
    GpsAlertStats alerts,
    FuelAnalyticsSummary fuel,
  ) {
    final health = gps.totalVehicles <= 0
        ? 0
        : (100.0 * gps.online / gps.totalVehicles).round();
    return [
      AiAttentionItem(
        text: health >= 80
            ? 'Fleet healthy ($health%)'
            : 'Fleet needs attention ($health%)',
        route: '/fleet',
        suggestedPrompt: "How is today's business looking?",
        severity: health >= 80 ? 'success' : 'warning',
      ),
      AiAttentionItem(
        text: '${trips.total} trips today · ${trips.completed} completed',
        route: '/trips',
        severity: 'info',
      ),
      if (alerts.active > 0)
        AiAttentionItem(
          text: '${alerts.active} active alert(s)',
          route: '/alerts',
          severity: 'warning',
        ),
      if (maint.pendingRequests > 0)
        AiAttentionItem(
          text: '${maint.pendingRequests} maintenance approval(s) pending',
          route: '/more/maintenance',
          severity: 'warning',
        )
      else
        AiAttentionItem(
          text: fuel.todayCost > 0
              ? 'Fuel spend today PKR ${fuel.todayCost.toStringAsFixed(0)}'
              : 'Monthly fuel PKR ${fleet.monthlyFuelCost.toStringAsFixed(0)}',
          route: '/more/reports',
          severity: 'info',
        ),
    ].take(4).toList();
  }

  List<AiAttentionItem> _superAdminAi(
    FleetOpsDashboard fleet,
    GpsFleetStatusKpis gps,
    MaintenanceKpis maint,
    GpsAlertStats alerts,
  ) {
    final health = gps.totalVehicles <= 0
        ? 0
        : (100.0 * gps.online / gps.totalVehicles).round();
    return [
      AiAttentionItem(
        text:
            'Platform overview (tenant scope) — fleet health $health%',
        route: '/fleet',
        suggestedPrompt: 'Summarize this tenant operational health',
        severity: health >= 80 ? 'success' : 'warning',
      ),
      if (gps.offline > 0)
        AiAttentionItem(
          text: '${gps.offline} vehicle(s) offline in this tenant',
          route: '/fleet/map',
          severity: 'critical',
        ),
      if (alerts.critical > 0)
        AiAttentionItem(
          text: '${alerts.critical} critical alert(s)',
          route: '/alerts',
          severity: 'warning',
        ),
      AiAttentionItem(
        text:
            '${fleet.totalVehicles} vehicles · ${fleet.driversOnDuty} drivers on duty',
        route: '/fleet',
        severity: 'info',
      ),
    ].take(4).toList();
  }

  List<AiAttentionItem> _dispatchAi(
    OpsTripsDashboard trips,
    List<OpsTripListItem> pending,
    GpsAlertStats alerts,
  ) {
    final items = <AiAttentionItem>[];
    if (pending.isNotEmpty) {
      items.add(AiAttentionItem(
        text: '${pending.length} trip(s) need assignment',
        route: '/trips',
        suggestedPrompt: 'Which trips need assignment?',
        severity: 'critical',
      ));
    }
    if (trips.inProgress > 0) {
      items.add(AiAttentionItem(
        text: '${trips.inProgress} trip(s) in progress',
        route: '/trips',
        suggestedPrompt: 'Show delayed or active trips',
        severity: 'info',
      ));
    }
    items.add(const AiAttentionItem(
      text: 'Ask AI for nearest available vehicle',
      route: '/ai',
      suggestedPrompt: 'Suggest nearest available vehicles for today',
      severity: 'success',
    ));
    if (alerts.critical > 0) {
      items.add(AiAttentionItem(
        text: '${alerts.critical} critical alert(s) on the road',
        route: '/alerts',
        severity: 'warning',
      ));
    }
    return items.take(4).toList();
  }

  List<AiAttentionItem> _driverManagerAi(
    DriverStats stats,
    ComplianceSummary compliance,
  ) {
    final items = <AiAttentionItem>[];
    if (stats.licensesExpired > 0) {
      items.add(AiAttentionItem(
        text: '${stats.licensesExpired} expired driver license(s)',
        route: '/more/drivers',
        suggestedPrompt: 'Which drivers need license renewal?',
        severity: 'critical',
      ));
    }
    if (stats.licensesExpiringSoon > 0) {
      items.add(AiAttentionItem(
        text: '${stats.licensesExpiringSoon} license(s) expiring soon',
        route: '/more/drivers',
        suggestedPrompt: 'Which drivers need license renewal?',
        severity: 'warning',
      ));
    }
    if (compliance.expired > 0) {
      items.add(AiAttentionItem(
        text: '${compliance.expired} expired compliance document(s)',
        route: '/documents',
        severity: 'warning',
      ));
    }
    if (stats.offDuty > 0) {
      items.add(AiAttentionItem(
        text: '${stats.offDuty} driver(s) off duty',
        route: '/more/drivers',
        severity: 'info',
      ));
    }
    if (items.isEmpty) {
      items.add(const AiAttentionItem(
        text: 'Ask AI which drivers need training or coaching',
        route: '/ai',
        suggestedPrompt: 'Which drivers need training?',
        severity: 'success',
      ));
    }
    return items.take(4).toList();
  }

  List<AiAttentionItem> _accountantAi(
    FleetOpsDashboard fleet,
    MaintenanceKpis maint,
    FuelAnalyticsSummary fuel,
  ) {
    return [
      AiAttentionItem(
        text: fuel.todayCost > 0
            ? 'Today fuel PKR ${fuel.todayCost.toStringAsFixed(0)}'
            : 'Monthly fuel PKR ${fleet.monthlyFuelCost.toStringAsFixed(0)}',
        route: '/more/reports',
        suggestedPrompt: "Summarize this month's fuel and maintenance costs",
        severity: 'info',
      ),
      AiAttentionItem(
        text:
            'Monthly maintenance PKR ${maint.monthlyMaintenanceCost.toStringAsFixed(0)}',
        route: '/more/maintenance',
        severity: 'info',
      ),
      if (maint.pendingRequests > 0)
        AiAttentionItem(
          text: '${maint.pendingRequests} pending maintenance request(s)',
          route: '/more/maintenance',
          severity: 'warning',
        ),
    ];
  }
}
