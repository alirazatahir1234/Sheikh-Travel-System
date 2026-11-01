import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/auth/domain/auth_models.dart';
import 'package:sheikh_go_driver/features/settings/domain/settings_access.dart';

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
  group('SettingsAccess', () {
    test('all roles get sync + profile + security permissions summary', () {
      final driver = _session();
      expect(SettingsAccess.canSeeSyncStatus(driver), isTrue);
      expect(SettingsAccess.canSeeProfileAccount(driver), isTrue);
      expect(SettingsAccess.canSeeSecurityPermissions(driver), isTrue);
      expect(SettingsAccess.canSeeLocationTracking(driver), isTrue);
      expect(SettingsAccess.canSeeAttendance(driver), isTrue);
      expect(SettingsAccess.canSeeActivityTimeline(driver), isTrue);
    });

    test('drivers cannot stop mandatory tracking or clear offline queue', () {
      final driver = _session();
      expect(SettingsAccess.canStopBackgroundTracking(driver), isFalse);
      expect(SettingsAccess.canClearOfflineQueue(driver), isFalse);
      expect(SettingsAccess.canSeeSecurityDiagnostics(driver), isFalse);
    });

    test('super admin can clear queue and see diagnostics', () {
      final admin = _session(
        roles: const [FleetRole.superAdmin],
        permissions: const [FleetPermissions.gpsView],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(SettingsAccess.canClearOfflineQueue(admin), isTrue);
      expect(SettingsAccess.canSeeSecurityDiagnostics(admin), isTrue);
      expect(SettingsAccess.canStopBackgroundTracking(admin), isTrue);
    });

    test('fleet manager can stop tracking but not clear queue', () {
      final fm = _session(
        roles: const [FleetRole.fleetManager],
        permissions: const [
          FleetPermissions.gpsView,
          FleetPermissions.driverView,
        ],
        authMode: AuthMode.staff,
        driverId: null,
      );
      expect(SettingsAccess.canStopBackgroundTracking(fm), isTrue);
      expect(SettingsAccess.canClearOfflineQueue(fm), isFalse);
      expect(SettingsAccess.canSeeSecurityDiagnostics(fm), isFalse);
      expect(SettingsAccess.canSeeLocationTracking(fm), isTrue);
      expect(SettingsAccess.canSeeAttendance(fm), isTrue);
    });

    test('environment label never for null session', () {
      expect(SettingsAccess.canSeeEnvironmentLabel(null), isFalse);
    });
  });
}
