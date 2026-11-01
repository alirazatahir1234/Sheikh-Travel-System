import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/auth/domain/auth_models.dart';
import 'package:sheikh_go_driver/core/navigation/fleet_nav_config.dart';

FleetSession _gpsOperatorSession() {
  return const FleetSession(
    accessToken: 't',
    refreshToken: 'r',
    userId: 1,
    tenantId: 1,
    displayName: 'Ops',
    roles: [FleetRole.gpsOperator],
    permissions: [
      FleetPermissions.gpsView,
      FleetPermissions.reportView,
    ],
    authMode: AuthMode.staff,
  );
}

void main() {
  test('GPS operator bottom nav order', () {
    final session = _gpsOperatorSession();
    final tabs = FleetNavConfig.visibleTabs(session);
    expect(
      tabs.map((t) => t.id).toList(),
      ['dashboard', 'map', 'fleet', 'alerts', 'more'],
    );
  });

  test('GPS operator More menu excludes fleet documents, reports, and commands',
      () {
    final session = _gpsOperatorSession();
    final routes = FleetNavConfig.visibleMoreEntries(session)
        .map((e) => e.route)
        .toList();
    expect(routes, contains('/gps/geofences'));
    expect(routes, contains('/gps/trips'));
    expect(routes, contains('/gps/incidents'));
    expect(routes, isNot(contains('/documents')));
    expect(routes, isNot(contains('/more/reports')));
    expect(routes, isNot(contains('/gps/commands')));
    expect(session.canSeeTrackerHardware, isTrue);
    expect(session.canSeeFleetAssignments, isFalse);
  });
}
