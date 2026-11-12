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
    test('expected package id is sheikhgo fleet', () {
      expect(AppConfig.expectedPackageId, 'com.sheikhgo.fleet');
    });

    test('appVersion is set', () {
      expect(AppConfig.appVersion, isNotEmpty);
    });

    test('default production API is Azure Caddy HTTPS (sslip.io)', () {
      expect(
        AppConfig.defaultProductionApiBaseUrl,
        'https://20-174-1-230.sslip.io/api',
      );
      final url = AppConfig.defaultProductionApiBaseUrl.toLowerCase();
      expect(url.contains('sslip.io'), isTrue);
      expect(url.contains('trycloudflare.com'), isFalse);
      expect(url.contains('railway.app'), isFalse);
      // Cleartext Azure :5082 breaks on many cellular networks (HTTP 302).
      expect(url.startsWith('https://'), isTrue);
      expect(url.contains('20.174.1.230:5082'), isFalse);
      expect(url.endsWith('/api'), isTrue);
    });

    test('hubBaseUrl ends with tracking hub path', () {
      expect(AppConfig.hubBaseUrl.endsWith('/hubs/tracking'), isTrue);
    });

    test('apiOrigin strips /api suffix from resolvedBaseUrl', () {
      final origin = AppConfig.apiOrigin;
      expect(origin.endsWith('/api'), isFalse);
      expect(AppConfig.resolvedBaseUrl.startsWith(origin), isTrue);
    });
  });
}
