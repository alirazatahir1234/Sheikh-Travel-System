import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;

class ApiConfig {
  static const _apiBaseUrlFromEnv = String.fromEnvironment('API_BASE_URL');

  static String get baseUrl {
    final configured = _apiBaseUrlFromEnv.isNotEmpty
        ? _apiBaseUrlFromEnv
        : 'http://127.0.0.1:5082/api';

    if (!kIsWeb && Platform.isAndroid) {
      return configured
          .replaceFirst('127.0.0.1', '10.0.2.2')
          .replaceFirst('localhost', '10.0.2.2');
    }
    return configured;
  }

  static const tenantSlug = String.fromEnvironment(
    'TENANT_SLUG',
    defaultValue: 'default',
  );
}
