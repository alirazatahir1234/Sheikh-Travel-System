import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/navigation/fleet_nav_config.dart';
import 'package:sheikh_go_driver/features/auth/domain/auth_models.dart';

FleetSession _session({
  List<String> roles = const [FleetRole.driver],
  List<String> permissions = FleetPermissions.driverDefaults,
  AuthMode authMode = AuthMode.driver,
  int? driverId = 42,
}) =>
    FleetSession(
      accessToken: 'token',
      refreshToken: 'refresh',
      userId: 1,
      tenantId: 1,
      displayName: 'Test User',
      roles: roles,
      permissions: permissions,
      authMode: authMode,
      driverId: driverId,
    );

void main() {
  group('FleetSession.primaryNavRole', () {
    test('driver-only resolves to DRIVER', () {
      expect(_session().primaryNavRole, FleetRole.driver);
    });

    test('respects nav role priority', () {
      final session = _session(
        roles: const [FleetRole.dispatcher, FleetRole.driverManager],
        permissions: const [
          FleetPermissions.bookingView,
          FleetPermissions.driverView,
          FleetPermissions.tripView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.primaryNavRole, FleetRole.driverManager);
    });

    test('fleet manager before dispatcher', () {
      final session = _session(
        roles: const [FleetRole.fleetManager, FleetRole.dispatcher],
        permissions: const [FleetPermissions.tripView],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.primaryNavRole, FleetRole.fleetManager);
    });
  });

  group('FleetSession permissions', () {
    test('driver-only session hides fleet and AI tabs', () {
      final session = _session();
      expect(session.isDriverOnly, isTrue);
      expect(session.canSeeFleetTab, isFalse);
      expect(session.canSeeAiTab, isFalse);
      expect(session.canSeeTripsTab, isTrue);
    });

    test('fleet manager sees fleet and AI tabs', () {
      final session = _session(
        roles: const [FleetRole.fleetManager],
        permissions: const [
          FleetPermissions.tripView,
          FleetPermissions.gpsView,
          FleetPermissions.vehicleView,
          FleetPermissions.aiView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.canSeeFleetTab, isTrue);
      expect(session.canSeeAiTab, isTrue);
      expect(session.canSeeTripsTab, isTrue);
    });

    test('driver manager sees drivers but not AI', () {
      final session = _session(
        roles: const [FleetRole.driverManager],
        permissions: const [
          FleetPermissions.driverView,
          FleetPermissions.tripView,
          FleetPermissions.vehicleView,
          FleetPermissions.gpsView,
          FleetPermissions.reportView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.canSeeDriversTab, isTrue);
      expect(session.canSeeAiTab, isFalse);
      expect(session.canSeeFleetTab, isTrue);
    });
  });

  group('FleetNavConfig role shells', () {
    test('driver shell', () {
      final tabs = FleetNavConfig.visibleTabs(_session());
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'trips', 'tracking', 'notifications', 'profile'],
      );
    });

    test('fleet manager shell', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.fleetManager],
          permissions: const [
            FleetPermissions.tripView,
            FleetPermissions.gpsView,
            FleetPermissions.vehicleView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'fleet', 'trips', 'more'],
      );
    });

    test('dispatcher shell', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.dispatcher],
          permissions: const [
            FleetPermissions.bookingView,
            FleetPermissions.tripView,
            FleetPermissions.gpsView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'bookings', 'trips', 'map', 'more'],
      );
    });

    test('driver manager shell', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.driverManager],
          permissions: const [
            FleetPermissions.driverView,
            FleetPermissions.tripView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'drivers', 'trips', 'more'],
      );
    });

    test('accountant shell', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.accountant],
          permissions: const [
            FleetPermissions.paymentView,
            FleetPermissions.invoiceView,
            FleetPermissions.reportView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'finance', 'reports', 'notifications', 'more'],
      );
    });

    test('index prefers longest path match for map tab', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.dispatcher],
          permissions: const [FleetPermissions.bookingView],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(FleetNavConfig.indexForLocation(tabs, '/fleet/map'), 3);
    });
  });
}
