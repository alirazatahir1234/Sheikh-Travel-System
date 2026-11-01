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

    test('default production API is Cloudflare quick tunnel HTTPS', () {
      expect(
        AppConfig.defaultProductionApiBaseUrl,
        'https://joke-rebates-titans-duties.trycloudflare.com/api',
      );
      final url = AppConfig.defaultProductionApiBaseUrl.toLowerCase();
      expect(url.contains('trycloudflare.com'), isTrue);
      expect(url.contains('sslip.io'), isFalse);
      expect(url.contains('railway.app'), isFalse);
      expect(url.startsWith('https://'), isTrue);
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
