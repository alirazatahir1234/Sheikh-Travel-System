import 'package:flutter/material.dart';
import '../../features/auth/domain/auth_models.dart';

class FleetNavTab {
  const FleetNavTab({
    required this.id,
    required this.label,
    required this.icon,
    required this.activeIcon,
    required this.path,
    this.matchPaths = const [],
  });

  final String id;
  final String label;
  final IconData icon;
  final IconData activeIcon;
  final String path;

  /// Extra path prefixes that should highlight this tab (longest match wins).
  final List<String> matchPaths;

  bool matchesLocation(String loc) {
    final candidates = [path, ...matchPaths];
    for (final p in candidates) {
      if (loc == p || loc.startsWith('$p/')) return true;
    }
    return false;
  }
}

class MoreMenuEntry {
  const MoreMenuEntry({
    required this.label,
    required this.icon,
    required this.route,
    required this.isVisible,
    this.comingSoonSprint,
  });

  final String label;
  final IconData icon;
  final String route;
  final bool Function(FleetSession session) isVisible;
  final String? comingSoonSprint;
}

abstract final class FleetNavConfig {
  static const dashboard = FleetNavTab(
    id: 'dashboard',
    label: 'Dashboard',
    icon: Icons.dashboard_outlined,
    activeIcon: Icons.dashboard_rounded,
    path: '/dashboard',
  );

  static const fleet = FleetNavTab(
    id: 'fleet',
    label: 'Vehicles',
    icon: Icons.local_shipping_outlined,
    activeIcon: Icons.local_shipping_rounded,
    path: '/fleet',
  );

  static const trips = FleetNavTab(
    id: 'trips',
    label: 'Trips',
    icon: Icons.route_outlined,
    activeIcon: Icons.route_rounded,
    path: '/trips',
  );

  static const ai = FleetNavTab(
    id: 'ai',
    label: 'AI',
    icon: Icons.auto_awesome_outlined,
    activeIcon: Icons.auto_awesome_rounded,
    path: '/ai',
  );

  static const more = FleetNavTab(
    id: 'more',
    label: 'More',
    icon: Icons.apps_outlined,
    activeIcon: Icons.apps_rounded,
    path: '/more',
  );

  static const tracking = FleetNavTab(
    id: 'tracking',
    label: 'Tracking',
    icon: Icons.my_location_outlined,
    activeIcon: Icons.my_location,
    path: '/live',
  );

  static const notifications = FleetNavTab(
    id: 'notifications',
    label: 'Inbox',
    icon: Icons.notifications_outlined,
    activeIcon: Icons.notifications_rounded,
    path: '/notifications',
  );

  static const profile = FleetNavTab(
    id: 'profile',
    label: 'Profile',
    icon: Icons.person_outline_rounded,
    activeIcon: Icons.person_rounded,
    path: '/profile',
  );

  static const drivers = FleetNavTab(
    id: 'drivers',
    label: 'Drivers',
    icon: Icons.groups_outlined,
    activeIcon: Icons.groups_rounded,
    path: '/more/drivers',
  );

  static const bookings = FleetNavTab(
    id: 'bookings',
    label: 'Bookings',
    icon: Icons.event_note_outlined,
    activeIcon: Icons.event_note_rounded,
    path: '/bookings',
  );

  static const map = FleetNavTab(
    id: 'map',
    label: 'Live Map',
    icon: Icons.map_outlined,
    activeIcon: Icons.map_rounded,
    path: '/fleet/map',
    matchPaths: ['/fleet/map'],
  );

  static const alertsTab = FleetNavTab(
    id: 'alerts',
    label: 'Alerts',
    icon: Icons.warning_amber_outlined,
    activeIcon: Icons.warning_amber_rounded,
    path: '/alerts',
    matchPaths: ['/alerts'],
  );

  static const finance = FleetNavTab(
    id: 'finance',
    label: 'Finance',
    icon: Icons.account_balance_wallet_outlined,
    activeIcon: Icons.account_balance_wallet_rounded,
    path: '/finance',
  );

  static const reports = FleetNavTab(
    id: 'reports',
    label: 'Reports',
    icon: Icons.bar_chart_outlined,
    activeIcon: Icons.bar_chart_rounded,
    path: '/more/reports',
  );

  static const users = FleetNavTab(
    id: 'users',
    label: 'Users',
    icon: Icons.manage_accounts_outlined,
    activeIcon: Icons.manage_accounts_rounded,
    path: '/users',
  );

  /// Catalog of all known shell tabs (used for route registration helpers).
  static const allTabs = [
    dashboard,
    fleet,
    trips,
    ai,
    more,
    tracking,
    notifications,
    profile,
    drivers,
    bookings,
    map,
    alertsTab,
    finance,
    reports,
    users,
  ];

  static const moreEntries = [
    MoreMenuEntry(
      label: 'Fleet live map',
      icon: Icons.map_outlined,
      route: '/fleet/map',
      isVisible: _fleetLiveMap,
    ),
    MoreMenuEntry(
      label: 'My tracking',
      icon: Icons.my_location_outlined,
      route: '/live',
      isVisible: _driverTracking,
    ),
    MoreMenuEntry(
      label: 'Drivers',
      icon: Icons.groups_outlined,
      route: '/more/drivers',
      isVisible: _driversMenu,
    ),
    MoreMenuEntry(
      label: 'Alerts',
      icon: Icons.warning_amber_outlined,
      route: '/alerts',
      isVisible: _alertsMenu,
    ),
    MoreMenuEntry(
      label: 'GPS trips',
      icon: Icons.route_outlined,
      route: '/gps/trips',
      isVisible: _alertsMenu,
    ),
    MoreMenuEntry(
      label: 'Fuel analytics',
      icon: Icons.local_gas_station_outlined,
      route: '/gps/fuel',
      isVisible: _alertsMenu,
    ),
    MoreMenuEntry(
      label: 'Mileage',
      icon: Icons.speed_outlined,
      route: '/gps/mileage',
      isVisible: _alertsMenu,
    ),
    MoreMenuEntry(
      label: 'Maintenance',
      icon: Icons.build_outlined,
      route: '/more/maintenance',
      isVisible: _maintenanceMenu,
    ),
    MoreMenuEntry(
      label: 'Fuel',
      icon: Icons.local_gas_station_outlined,
      route: '/fuel',
      isVisible: _fuelMenu,
    ),
    MoreMenuEntry(
      label: 'Reports',
      icon: Icons.bar_chart_outlined,
      route: '/more/reports',
      isVisible: _reportsMenu,
    ),
    MoreMenuEntry(
      label: 'Documents',
      icon: Icons.folder_outlined,
      route: '/documents',
      isVisible: _documentsMenu,
    ),
    MoreMenuEntry(
      label: 'Notifications',
      icon: Icons.notifications_outlined,
      route: '/notifications',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'Bookings',
      icon: Icons.event_note_outlined,
      route: '/bookings',
      isVisible: _bookingsMenu,
    ),
    MoreMenuEntry(
      label: 'Finance',
      icon: Icons.account_balance_wallet_outlined,
      route: '/finance',
      isVisible: _financeMenu,
    ),
    MoreMenuEntry(
      label: 'Users',
      icon: Icons.manage_accounts_outlined,
      route: '/users',
      isVisible: _usersMenu,
    ),
    MoreMenuEntry(
      label: 'Attendance',
      icon: Icons.access_time_outlined,
      route: '/attendance',
      isVisible: _driverWorkflow,
    ),
    MoreMenuEntry(
      label: 'Inspection',
      icon: Icons.fact_check_outlined,
      route: '/inspection',
      isVisible: _driverWorkflow,
    ),
    MoreMenuEntry(
      label: 'Earnings',
      icon: Icons.payments_outlined,
      route: '/earnings',
      isVisible: _driverWorkflow,
    ),
    MoreMenuEntry(
      label: 'Timeline',
      icon: Icons.timeline_outlined,
      route: '/timeline',
      isVisible: _driverWorkflow,
    ),
        MoreMenuEntry(
      label: 'AI Copilot',
      icon: Icons.auto_awesome_outlined,
      route: '/ai',
      isVisible: _aiMenu,
    ),
    MoreMenuEntry(
      label: 'Profile',
      icon: Icons.person_outline_rounded,
      route: '/profile',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'Settings',
      icon: Icons.settings_outlined,
      route: '/settings',
      isVisible: _alwaysEntry,
    ),
  ];

  /// GPS Operator — ops-focused More grid (no fleet-admin chrome).
  static const operatorMoreEntries = [
    MoreMenuEntry(
      label: 'Profile',
      icon: Icons.person_outline_rounded,
      route: '/profile',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'Notifications',
      icon: Icons.notifications_outlined,
      route: '/notifications',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'Documents',
      icon: Icons.folder_outlined,
      route: '/documents',
      isVisible: _documentsMenu,
    ),
    MoreMenuEntry(
      label: 'GPS commands',
      icon: Icons.power_settings_new_outlined,
      route: '/gps/commands',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Incident center',
      icon: Icons.emergency_outlined,
      route: '/gps/incidents',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Geofences',
      icon: Icons.fence_outlined,
      route: '/gps/geofences',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'GPS trips',
      icon: Icons.route_outlined,
      route: '/gps/trips',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Fuel analytics',
      icon: Icons.local_gas_station_outlined,
      route: '/gps/fuel',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Mileage',
      icon: Icons.speed_outlined,
      route: '/gps/mileage',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Reports',
      icon: Icons.bar_chart_outlined,
      route: '/more/reports',
      isVisible: _reportsMenu,
    ),
    MoreMenuEntry(
      label: 'AI insights',
      icon: Icons.auto_awesome_outlined,
      route: '/gps/operator-ai',
      isVisible: _gpsOperatorOps,
    ),
    MoreMenuEntry(
      label: 'Support',
      icon: Icons.support_agent_outlined,
      route: '/legal/support',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'About',
      icon: Icons.info_outline_rounded,
      route: '/legal/about',
      isVisible: _alwaysEntry,
    ),
    MoreMenuEntry(
      label: 'Settings',
      icon: Icons.settings_outlined,
      route: '/settings',
      isVisible: _alwaysEntry,
    ),
  ];

  static List<FleetNavTab> visibleTabs(FleetSession? session) {
    if (session == null) return const [];
    switch (session.primaryNavRole) {
      case FleetRole.driver:
        return const [dashboard, trips, tracking, notifications, profile];
      case FleetRole.driverManager:
        return const [dashboard, drivers, trips, more];
      case FleetRole.gpsOperator:
        return const [dashboard, map, fleet, alertsTab, more];
      case FleetRole.dispatcher:
        return const [dashboard, bookings, trips, map, more];
      case FleetRole.accountant:
        return const [dashboard, finance, reports, notifications, more];
      case FleetRole.superAdmin:
      case FleetRole.tenantAdmin:
      case FleetRole.fleetManager:
      default:
        return const [dashboard, fleet, trips, more];
    }
  }

  static List<MoreMenuEntry> visibleMoreEntries(FleetSession session) {
    if (session.isGpsOperator) {
      return operatorMoreEntries.where((e) => e.isVisible(session)).toList();
    }
    return moreEntries.where((entry) => entry.isVisible(session)).toList();
  }

  static bool isShellRoute(String path) {
    for (final tab in allTabs) {
      if (path == tab.path || path.startsWith('${tab.path}/')) return true;
    }
    if (path == '/more') return true;
    return _moreFeatureRoutes.any((route) => path.startsWith(route));
  }

  /// Picks the best bottom-nav index for [loc] (longest path match wins).
  static int indexForLocation(List<FleetNavTab> tabs, String loc) {
    var bestIndex = -1;
    var bestLength = -1;
    for (var i = 0; i < tabs.length; i++) {
      final tab = tabs[i];
      final candidates = [tab.path, ...tab.matchPaths];
      for (final p in candidates) {
        if (loc == p || loc.startsWith('$p/')) {
          if (p.length > bestLength) {
            bestLength = p.length;
            bestIndex = i;
          }
        }
      }
    }
    if (bestIndex >= 0) return bestIndex;
    if (isShellRoute(loc)) {
      final moreIdx = tabs.indexWhere((t) => t.id == 'more');
      if (moreIdx >= 0) return moreIdx;
    }
    return 0;
  }

  static const _moreFeatureRoutes = [
    '/live',
    '/alerts',
    '/notifications',
    '/profile',
    '/attendance',
    '/fuel',
    '/inspection',
    '/documents',
    '/earnings',
    '/timeline',
    '/settings',
    '/offline-queue',
    '/security',
    '/legal/',
    '/bookings',
    '/finance',
    '/users',
    '/ai',
    '/gps/',
    '/gps/commands',
    '/gps/incidents',
    '/gps/geofences',
    '/gps/operator-ai',
    '/legal/',
  ];

  static bool _alwaysEntry(FleetSession _) => true;
  static bool _fleetLiveMap(FleetSession s) => s.canSeeFleetTab;
  static bool _driverTracking(FleetSession s) => s.isDriverSession;
  static bool _driversMenu(FleetSession s) => s.canSeeDriversTab;
  static bool _alertsMenu(FleetSession s) =>
      !s.isDriverOnly && s.hasPermission(FleetPermissions.gpsView);
  static bool _maintenanceMenu(FleetSession s) =>
      !s.isDriverOnly && s.hasPermission(FleetPermissions.maintenanceView);
  static bool _fuelMenu(FleetSession s) =>
      s.hasPermission(FleetPermissions.fuelView) || s.isDriverSession;
  static bool _reportsMenu(FleetSession s) =>
      !s.isDriverOnly && s.hasPermission(FleetPermissions.reportView);
  static bool _documentsMenu(FleetSession s) =>
      s.isDriverSession || s.hasPermission(FleetPermissions.vehicleView);
  static bool _driverWorkflow(FleetSession s) => s.isDriverSession;
  static bool _bookingsMenu(FleetSession s) => s.canSeeBookingsTab;
  static bool _financeMenu(FleetSession s) => s.canSeeFinanceTab;
  static bool _usersMenu(FleetSession s) => s.canSeeUsersTab;
  static bool _aiMenu(FleetSession s) => s.canSeeAiTab;
  static bool _gpsOperatorOps(FleetSession s) =>
      s.isGpsOperator || (!s.isDriverOnly && s.hasPermission(FleetPermissions.gpsView));
}
