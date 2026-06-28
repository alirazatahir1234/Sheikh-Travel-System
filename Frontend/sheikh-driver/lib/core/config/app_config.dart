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

  static bool get isDev => environment == AppEnvironment.dev;
  static bool get isProd => environment == AppEnvironment.prod;

  static const String appVersion = '1.0.0';

  // SHA-256 certificate fingerprints for TLS pinning (production only).
  // To obtain: openssl s_client -connect your.api.host:443 </dev/null 2>/dev/null \
  //   | openssl x509 -fingerprint -sha256 -noout | cut -d= -f2 | tr -d ':'
  // Pass via --dart-define=CERT_PIN_1=<hex> and CERT_PIN_2=<rotation-hex>
  static const List<String> certFingerprints = [
    // String.fromEnvironment('CERT_PIN_1', defaultValue: ''),
    // String.fromEnvironment('CERT_PIN_2', defaultValue: ''),
  ];
}
