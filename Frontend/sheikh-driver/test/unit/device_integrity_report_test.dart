import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/security/device_integrity_service.dart';

void main() {
  group('DeviceIntegrityReport', () {
    DeviceIntegrityReport report({
      bool emulator = false,
      bool rooted = false,
      bool jailbroken = false,
      bool tampered = false,
    }) =>
        DeviceIntegrityReport(
          isEmulator: emulator,
          isRooted: rooted,
          isJailbroken: jailbroken,
          isTampered: tampered,
          pinningConfigured: true,
          deviceId: 'dev-1',
          platform: 'android',
          model: 'Pixel',
          osVersion: '14',
          appVersion: '1.0.0+1',
          packageName: 'com.sheikhgo.driver',
          installerStore: 'com.android.vending',
          issues: [
            if (emulator) 'Running on emulator/simulator',
            if (rooted) 'Root indicators detected',
          ],
        );

    test('isCompromised when rooted or jailbroken', () {
      expect(report(rooted: true).isCompromised, isTrue);
      expect(report(jailbroken: true).isCompromised, isTrue);
      expect(report().isCompromised, isFalse);
    });

    test('registration payload includes flags', () {
      final p = report(emulator: true).toRegistrationPayload();
      expect(p['deviceId'], 'dev-1');
      expect(p['isEmulator'], isTrue);
      expect(p['packageName'], 'com.sheikhgo.driver');
    });

    test('fingerprintHash is stable', () {
      final r = report();
      final a = DeviceIntegrityService.fingerprintHash(r);
      final b = DeviceIntegrityService.fingerprintHash(r);
      expect(a, b);
      expect(a.length, 64);
    });
  });
}
