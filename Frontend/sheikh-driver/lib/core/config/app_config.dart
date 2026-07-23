import 'package:flutter/foundation.dart';

enum AppEnvironment { dev, uat, prod }

class AppConfig {
  static const _env = String.fromEnvironment('ENV', defaultValue: 'dev');

  static AppEnvironment get environment => switch (_env) {
        'prod' => AppEnvironment.prod,
        'uat' => AppEnvironment.uat,
        _ => AppEnvironment.dev,
      };

  /// Compile-time override from `--dart-define=API_BASE_URL=...`.
  /// Empty means “use [resolvedBaseUrl] platform default”.
  static const String _apiBaseUrlDefine = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: '',
  );

  static const String _defaultIosHostApi = 'http://localhost:5082/api';
  static const String _defaultAndroidEmulatorApi = 'http://10.0.2.2:5082/api';

  /// Whether an explicit `API_BASE_URL` dart-define was provided.
  static bool get hasExplicitApiBaseUrl => _apiBaseUrlDefine.trim().isNotEmpty;

  /// API root used by Dio / SignalR.
  ///
  /// - Explicit `--dart-define=API_BASE_URL=...` always wins (required for
  ///   physical devices — use your Mac LAN IP, e.g. `http://10.x.x.x:5082/api`).
  /// - Android emulator default: `http://10.0.2.2:5082/api` (host loopback).
  /// - iOS simulator / desktop default: `http://localhost:5082/api`.
  static String get resolvedBaseUrl {
    final defined = _apiBaseUrlDefine.trim();
    if (defined.isNotEmpty) return defined;
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return _defaultAndroidEmulatorApi;
    }
    return _defaultIosHostApi;
  }

  /// Alias for [resolvedBaseUrl] (call sites historically used `baseUrl`).
  static String get baseUrl => resolvedBaseUrl;

  /// Origin without `/api` suffix (SignalR hubs).
  static String get apiOrigin {
    final u = resolvedBaseUrl;
    if (u.endsWith('/api')) return u.substring(0, u.length - 4);
    if (u.endsWith('/api/')) return u.substring(0, u.length - 5);
    return u.replaceFirst(RegExp(r'/api/?$'), '');
  }

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

  static const String appVersion = String.fromEnvironment(
    'APP_VERSION',
    defaultValue: '1.0.0',
  );

  static String get environmentLabel => environment.name.toUpperCase();

  /// Expected Android applicationId / iOS bundle id.
  static const String expectedPackageId = 'com.sheikhgo.fleet';

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
