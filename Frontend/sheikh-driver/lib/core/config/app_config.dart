import 'package:flutter/foundation.dart';

enum AppEnvironment { dev, uat, prod }

class AppConfig {
  static const _env = String.fromEnvironment('ENV', defaultValue: 'dev');

  static AppEnvironment get environment => switch (_env) {
        'prod' => AppEnvironment.prod,
        'uat' => AppEnvironment.uat,
        _ => AppEnvironment.dev,
      };

  /// Same host as ERP `environment.prod.ts` — baked in for release APKs.
  static const String defaultProductionApiBaseUrl =
      'https://sheikh-travel-system-production.up.railway.app/api';

  /// Compile-time override from `--dart-define=API_BASE_URL=...`.
  /// Empty means “use [resolvedBaseUrl] env/platform default”.
  static const String _apiBaseUrlDefine = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: '',
  );

  /// Optional SignalR origin override (`https://host` or `https://host/hubs`).
  /// Empty → derive from [apiOrigin] + `/hubs/tracking`.
  static const String _hubUrlDefine = String.fromEnvironment(
    'HUB_URL',
    defaultValue: '',
  );

  static const String _defaultIosHostApi = 'http://localhost:5082/api';
  static const String _defaultAndroidEmulatorApi = 'http://10.0.2.2:5082/api';

  /// Whether an explicit `API_BASE_URL` dart-define was provided.
  static bool get hasExplicitApiBaseUrl => _apiBaseUrlDefine.trim().isNotEmpty;

  /// API root used by Dio / uploads / background GPS.
  ///
  /// Resolution order:
  /// 1. Explicit `--dart-define=API_BASE_URL=...` (LAN IP for local device testing).
  /// 2. When `ENV=prod` or `ENV=uat`: [defaultProductionApiBaseUrl] (HTTPS).
  /// 3. Android emulator: `http://10.0.2.2:5082/api`.
  /// 4. iOS simulator / desktop: `http://localhost:5082/api`.
  static String get resolvedBaseUrl {
    final defined = _apiBaseUrlDefine.trim();
    if (defined.isNotEmpty) return defined;
    if (isProd || isUat) return defaultProductionApiBaseUrl;
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return _defaultAndroidEmulatorApi;
    }
    return _defaultIosHostApi;
  }

  /// Alias for [resolvedBaseUrl] (call sites historically used `baseUrl`).
  static String get baseUrl => resolvedBaseUrl;

  /// Origin without `/api` suffix (SignalR hubs / file hosts).
  static String get apiOrigin {
    final u = resolvedBaseUrl;
    if (u.endsWith('/api')) return u.substring(0, u.length - 4);
    if (u.endsWith('/api/')) return u.substring(0, u.length - 5);
    return u.replaceFirst(RegExp(r'/api/?$'), '');
  }

  /// Full tracking hub URL (no query string).
  ///
  /// Uses `--dart-define=HUB_URL=...` when set; otherwise `{apiOrigin}/hubs/tracking`.
  /// `HUB_URL` may be either the hubs root (`.../hubs`) or the full tracking path.
  static String get hubBaseUrl {
    final defined = _hubUrlDefine.trim();
    if (defined.isEmpty) return '$apiOrigin/hubs/tracking';
    final trimmed = defined.endsWith('/')
        ? defined.substring(0, defined.length - 1)
        : defined;
    if (trimmed.toLowerCase().endsWith('/hubs/tracking')) return trimmed;
    if (trimmed.toLowerCase().endsWith('/hubs')) return '$trimmed/tracking';
    return '$trimmed/hubs/tracking';
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
