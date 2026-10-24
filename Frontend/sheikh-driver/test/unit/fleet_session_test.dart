import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/navigation/fleet_nav_config.dart';
import 'package:sheikh_go_driver/features/auth/domain/auth_models.dart';

CompanyContext _aiEntitledContext() => const CompanyContext(
      companyId: 1,
      tenantId: 1,
      companyName: 'Test Co',
      modules: [
        CompanyModule(moduleCode: 'AI', name: 'AAS Assistant'),
      ],
    );

FleetSession _session({
  List<String> roles = const [FleetRole.driver],
  List<String> permissions = FleetPermissions.driverDefaults,
  AuthMode authMode = AuthMode.driver,
  int? driverId = 42,
  CompanyContext? companyContext,
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
      companyContext: companyContext,
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

    test('owner role wins over fleet manager', () {
      final session = _session(
        roles: const [FleetRole.owner, FleetRole.fleetManager],
        permissions: const [FleetPermissions.gpsView, FleetPermissions.fuelView],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.primaryNavRole, FleetRole.owner);
      expect(session.isOwnerTracking, isTrue);
    });

    test('tenant admin wins over driver (not Role.Id order)', () {
      final session = _session(
        roles: const [FleetRole.driver, FleetRole.tenantAdmin],
        permissions: const [FleetPermissions.tripView, FleetPermissions.gpsView],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.primaryNavRole, FleetRole.tenantAdmin);
      expect(session.isDriverOnly, isFalse);
    });

    test('orderByPrivilege puts system admin first', () {
      expect(
        FleetRole.orderByPrivilege(const [
          FleetRole.driver,
          FleetRole.fleetManager,
          FleetRole.tenantAdmin,
        ]),
        [FleetRole.tenantAdmin, FleetRole.fleetManager, FleetRole.driver],
      );
    });

    test('fromStaffJson sorts roles by privilege not response order', () {
      final session = FleetSession.fromStaffJson({
        'accessToken': 'eyJhbGciOiJub25lIn0.e30.',
        'refreshToken': 'r',
        'fullName': 'Admin',
        'role': FleetRole.driver,
        'roles': [FleetRole.driver, FleetRole.fleetManager, FleetRole.tenantAdmin],
        'permissions': [FleetPermissions.tripView],
        'userId': 1,
        'tenantId': 1,
      });
      expect(session.roles.first, FleetRole.tenantAdmin);
      expect(session.primaryNavRole, FleetRole.tenantAdmin);
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

    test('fleet manager without AI module hides AI tab', () {
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
      expect(session.hasAiAssistantModule, isFalse);
      expect(session.canSeeAiTab, isFalse);
    });

    test('fleet manager with AI module sees AI tab', () {
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
        companyContext: _aiEntitledContext(),
      );
      expect(session.canSeeFleetTab, isTrue);
      expect(session.hasAiAssistantModule, isTrue);
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

    test('pure OWNER does not see Co-pilot even with Ai.View', () {
      final session = _session(
        roles: const [FleetRole.owner],
        permissions: const [
          FleetPermissions.aiView,
          FleetPermissions.gpsView,
          FleetPermissions.driverView,
          FleetPermissions.fuelView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(session.canSeeOwnerEarnings, isTrue);
      expect(session.canSeeAiTab, isFalse);
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
        ['dashboard', 'map', 'fleet', 'trips', 'more'],
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

    test('owner tracking shell — live map, drivers, fuel', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.owner],
          permissions: const [
            FleetPermissions.gpsView,
            FleetPermissions.driverView,
            FleetPermissions.fuelView,
            FleetPermissions.vehicleView,
            FleetPermissions.tripView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'map', 'drivers', 'fuel', 'more'],
      );
    });

    test('owner more menu includes tracker surfaces', () {
      final session = _session(
        roles: const [FleetRole.owner],
        permissions: const [
          FleetPermissions.gpsView,
          FleetPermissions.gpsCommandView,
          FleetPermissions.vehicleView,
          FleetPermissions.driverView,
          FleetPermissions.fuelView,
          FleetPermissions.tripView,
          FleetPermissions.reportView,
          FleetPermissions.aiView,
          FleetPermissions.paymentView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      final routes =
          FleetNavConfig.visibleMoreEntries(session).map((e) => e.route).toList();
      expect(routes, contains('/gps/trips'));
      expect(routes, contains('/gps/commands'));
      expect(routes, contains('/gps/geofences'));
      expect(routes, contains('/gps/incidents'));
      expect(routes, contains('/gps/mileage'));
      expect(routes, contains('/fleet'));
      expect(routes, isNot(contains('/ai')));
      expect(routes, contains('/earnings'));
    });

    test('tenant admin uses owner tracking shell', () {
      final tabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.tenantAdmin],
          permissions: const [
            FleetPermissions.gpsView,
            FleetPermissions.driverView,
            FleetPermissions.fuelView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      expect(
        tabs.map((t) => t.id).toList(),
        ['dashboard', 'map', 'drivers', 'fuel', 'more'],
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

    test('vehicle history never highlights Live Map tab', () {
      final ownerTabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.owner],
          permissions: const [
            FleetPermissions.gpsView,
            FleetPermissions.driverView,
            FleetPermissions.fuelView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      final historyIdx =
          FleetNavConfig.indexForLocation(ownerTabs, '/fleet/vehicles/42/history');
      expect(ownerTabs[historyIdx].id, isNot('map'));
      expect(ownerTabs[historyIdx].id, 'more');

      final fmTabs = FleetNavConfig.visibleTabs(
        _session(
          roles: const [FleetRole.fleetManager],
          permissions: const [
            FleetPermissions.gpsView,
            FleetPermissions.vehicleView,
            FleetPermissions.tripView,
          ],
          authMode: AuthMode.staff,
          driverId: null,
        ),
      );
      final fmHistoryIdx =
          FleetNavConfig.indexForLocation(fmTabs, '/fleet/vehicles/7/history');
      // Fleet managers with Trips tab prefer Trips for history/playback.
      expect(fmTabs[fmHistoryIdx].id, anyOf('trips', 'fleet'));
      expect(fmTabs[fmHistoryIdx].id, isNot('map'));
    });
  });
}
