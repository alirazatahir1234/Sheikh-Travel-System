import 'package:flutter/foundation.dart';

enum AppEnvironment { dev, uat, prod }

class AppConfig {
  static const _env = String.fromEnvironment('ENV', defaultValue: 'dev');

  static AppEnvironment get environment => switch (_env) {
        'prod' => AppEnvironment.prod,
        'uat' => AppEnvironment.uat,
        _ => AppEnvironment.dev,
      };

  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5082/api',
  );

  static const String tenantSlug = String.fromEnvironment(
    'TENANT_SLUG',
    defaultValue: 'default',
  );

  static const String googleMapsApiKey = String.fromEnvironment(
    'GOOGLE_MAPS_KEY',
    defaultValue: '',
  );

  /// Public HTTPS URLs for store compliance (Settings → Legal).
  static const String privacyPolicyUrl = String.fromEnvironment(
    'PRIVACY_URL',
    defaultValue: '',
  );

  static const String termsOfServiceUrl = String.fromEnvironment(
    'TERMS_URL',
    defaultValue: '',
  );

  static bool get isDev => environment == AppEnvironment.dev;
  static bool get isUat => environment == AppEnvironment.uat;
  static bool get isProd => environment == AppEnvironment.prod;

  static const String appVersion = '1.0.0';

  /// Expected Android applicationId / iOS bundle id.
  static const String expectedPackageId = 'com.sheikhgo.driver';

  /// When true (prod release), rooted/jailbroken devices are blocked.
  static bool get blockCompromisedDevices =>
      isProd &&
      !kDebugMode &&
      const bool.fromEnvironment('BLOCK_COMPROMISED', defaultValue: true);

  /// When true (prod release), emulators are blocked.
  static bool get blockEmulators =>
      isProd &&
      !kDebugMode &&
      const bool.fromEnvironment('BLOCK_EMULATORS', defaultValue: true);

  /// SHA-256 certificate fingerprints (hex, with or without colons).
  /// Pass via --dart-define=CERT_PIN_1=... --dart-define=CERT_PIN_2=...
  static List<String> get certFingerprints {
    const raw = [
      String.fromEnvironment('CERT_PIN_1', defaultValue: ''),
      String.fromEnvironment('CERT_PIN_2', defaultValue: ''),
      String.fromEnvironment('CERT_PIN_3', defaultValue: ''),
    ];
    return raw
        .map(normalizeCertPin)
        .where((p) => p.isNotEmpty && p.length >= 32)
        .toList();
  }

  /// TLS pinning: release builds with pins configured (unless disabled).
  static bool get shouldPinCertificates {
    if (kDebugMode) return false;
    if (const bool.fromEnvironment('DISABLE_CERT_PINNING', defaultValue: false)) {
      return false;
    }
    if (certFingerprints.isEmpty) return false;
    return isProd || isUat;
  }

  static String normalizeCertPin(String pin) =>
      pin.replaceAll(':', '').replaceAll(' ', '').toLowerCase().trim();
}
