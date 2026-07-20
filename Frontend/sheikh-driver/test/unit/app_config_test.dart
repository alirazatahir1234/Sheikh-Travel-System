import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/config/app_config.dart';

void main() {
  group('AppConfig.normalizeCertPin', () {
    test('strips colons and lowercases', () {
      expect(
        AppConfig.normalizeCertPin('AB:CD:EF:12'),
        'abcdef12',
      );
    });

    test('trims whitespace', () {
      expect(AppConfig.normalizeCertPin('  deadbeef  '), 'deadbeef');
    });
  });

  group('AppConfig basics', () {
    test('expected package id is sheikhgo driver', () {
      expect(AppConfig.expectedPackageId, 'com.sheikhgo.driver');
    });

    test('appVersion is set', () {
      expect(AppConfig.appVersion, isNotEmpty);
    });
  });
}
