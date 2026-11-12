import 'package:flutter/foundation.dart';

enum AppEnvironment { dev, uat, prod }

class AppConfig {
  static const _env = String.fromEnvironment('ENV', defaultValue: 'dev');

  static AppEnvironment get environment => switch (_env) {
        'prod' => AppEnvironment.prod,
        'uat' => AppEnvironment.uat,
        _ => AppEnvironment.dev,
      };

  /// Production API for release builds when `API_BASE_URL` is omitted.
  ///
  /// Matches Fleet (`http://20.174.1.230:5082/api`) and Android
  /// `network_security_config` cleartext allowlist. Do **not** use:
  /// - `*.trycloudflare.com` (ephemeral / currently NXDOMAIN)
  /// - `https://20.174.1.230` (Let’s Encrypt SAN is only `20-174-1-230.sslip.io`)
  /// - `https://20-174-1-230.sslip.io` as the mobile default (hostname embeds
  ///   `ssl` and previously triggered false "Secure connection failed" copy;
  ///   some networks also fail direct Azure IP reachability via that path)
  ///
  /// Prefer later: `https://api.sheikhgo.com/api` once DNS A → 20.174.1.230
  /// is live (Caddyfile already lists that host). ERP browsers stay on HTTPS
  /// (sslip / api.sheikhgo.com) — never cleartext from www.sheikhgo.com.
  /// Override anytime with `--dart-define=API_BASE_URL=...`.
  static const String defaultProductionApiBaseUrl =
      'http://20.174.1.230:5082/api';

  /// Compile-time override from `--dart-define=API_BASE_URL=...`.
  /// Empty means “use [resolvedBaseUrl] env/platform default”.
  static const String _apiBaseUrlDefine = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: '',
  );

  /// Mac / host LAN IP for physical phones (Samsung A05, etc.).
  /// Example: `--dart-define=DEV_LAN_HOST=192.168.100.61`
  /// Builds `http://{host}:5082/api` when [API_BASE_URL] is not set.
  static const String _devLanHostDefine = String.fromEnvironment(
    'DEV_LAN_HOST',
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
  static const int _devApiPort = 5082;

  /// Whether an explicit `API_BASE_URL` dart-define was provided.
  static bool get hasExplicitApiBaseUrl => _apiBaseUrlDefine.trim().isNotEmpty;

  /// Whether a LAN host was provided for physical-device local testing.
  static bool get hasDevLanHost => _devLanHostDefine.trim().isNotEmpty;

  /// API root used by Dio / uploads / background GPS.
  ///
  /// Resolution order:
  /// 1. Explicit `--dart-define=API_BASE_URL=...` (full URL wins).
  /// 2. When `ENV=prod` or `ENV=uat`: [defaultProductionApiBaseUrl].
  /// 3. Release builds without prod/uat or an HTTPS define → force prod API
  ///    (never ship `10.0.2.2` / `localhost` / cleartext LAN to Play).
  /// 4. `--dart-define=DEV_LAN_HOST=...` → `http://{host}:5082/api`
  ///    (physical Android / iOS on the same Wi‑Fi as the Mac).
  /// 5. Android (no LAN host): emulator loopback `http://10.0.2.2:5082/api`.
  /// 6. iOS simulator / desktop: `http://localhost:5082/api`.
  ///
  /// Play/AAB builds must use `./scripts/build_prod.sh` (or equivalent dart-defines).
  static String get resolvedBaseUrl {
    final defined = _apiBaseUrlDefine.trim();
    if (defined.isNotEmpty) {
      if (kReleaseMode &&
          !isProd &&
          !isUat &&
          !defined.toLowerCase().startsWith('https://')) {
        // Accidental release with http://localhost or LAN — refuse cleartext.
        return defaultProductionApiBaseUrl;
      }
      return defined;
    }
    if (isProd || isUat) return defaultProductionApiBaseUrl;

    // Release without ENV=prod|uat and without API_BASE_URL → prod API.
    if (kReleaseMode) return defaultProductionApiBaseUrl;

    final lanHost = _devLanHostDefine.trim();
    if (lanHost.isNotEmpty) {
      final host = lanHost
          .replaceFirst(RegExp(r'^https?://'), '')
          .split('/')
          .first
          .split(':')
          .first;
      return 'http://$host:$_devApiPort/api';
    }

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

  /// Optional white-label slug via `--dart-define=TENANT_SLUG=...`.
  /// Empty by default so the multi-tenant app does not advertise Sheikh's
  /// legacy `default` slug on every request (JWT tenant_id is authoritative).
  static const String tenantSlug = String.fromEnvironment(
    'TENANT_SLUG',
    defaultValue: '',
  );

  /// True when a non-empty / non-legacy slug was compile-time configured.
  static bool get hasExplicitTenantSlug {
    final slug = tenantSlug.trim();
    if (slug.isEmpty) return false;
    return slug.toLowerCase() != 'default';
  }

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

  /// Support inbox for Help & Support / legal contact (override per tenant build).
  static const String supportEmail = String.fromEnvironment(
    'SUPPORT_EMAIL',
    defaultValue: 'support@sheikhgo.local',
  );

  /// Optional support phone (`tel:`) — omit from UI when empty.
  static const String supportPhone = String.fromEnvironment(
    'SUPPORT_PHONE',
    defaultValue: '',
  );

  /// Privacy contact from approved store copy (override before store submission).
  static const String privacyEmail = String.fromEnvironment(
    'PRIVACY_EMAIL',
    defaultValue: 'privacy@sheikhgo.local',
  );

  static bool get hasSupportPhone => supportPhone.trim().isNotEmpty;
  static bool get hasSupportEmail => supportEmail.trim().isNotEmpty;

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
    if (const bool.fromEnvironment('DISABLE_CERT_PINNING',
        defaultValue: false)) {
      return false;
    }
    if (certFingerprints.isEmpty) return false;
    return isProd || isUat;
  }

  static String normalizeCertPin(String pin) =>
      pin.replaceAll(':', '').replaceAll(' ', '').toLowerCase().trim();
}
