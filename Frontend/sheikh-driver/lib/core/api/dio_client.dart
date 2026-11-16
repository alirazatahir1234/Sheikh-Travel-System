import 'dart:convert';
import 'dart:io';
import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../config/app_config.dart';
import '../errors/app_exception.dart';
import '../errors/error_handler.dart';
import 'api_endpoints.dart';

const _accessTokenKey = 'fleet_access_token';
const _refreshTokenKey = 'fleet_refresh_token';
const _legacyAccessTokenKey = 'driver_access_token';
const _legacyRefreshTokenKey = 'driver_refresh_token';
const _sessionKey = 'fleet_session';
const _platformCompanyFilterKey = 'platform_company_filter_id';

/// Signals that the server rejected the saved refresh token. Keeping this
/// separate from the HTTP client avoids a dependency cycle with AuthRepository.
final sessionInvalidationProvider =
    ChangeNotifierProvider<SessionInvalidationNotifier>(
  (_) => SessionInvalidationNotifier(),
);

enum SessionTerminationReason {
  expiredToken,
  refreshRejected,
  manual,
}

class SessionInvalidationNotifier extends ChangeNotifier {
  bool _isTerminating = false;
  SessionTerminationReason? _reason;

  bool get isTerminating => _isTerminating;
  SessionTerminationReason? get reason => _reason;

  bool requestTermination(SessionTerminationReason reason) {
    if (_isTerminating) return false;
    _isTerminating = true;
    _reason = reason;
    notifyListeners();
    return true;
  }

  void completeTermination() {
    _isTerminating = false;
    _reason = null;
  }
}

final secureStorageProvider = Provider(
  (_) => const FlutterSecureStorage(),
);

final dioProvider = Provider<Dio>((ref) {
  final storage = ref.read(secureStorageProvider);
  final sessionInvalidation = ref.read(sessionInvalidationProvider);
  // Azure/sslip TLS+RTT is often ~0.3–1.7s on Wi‑Fi and higher on cellular.
  // 20s was too tight when handshake stalls or the first auth/DB round-trip
  // is slow — Dio then surfaces connectionTimeout/receiveTimeout as
  // "Server is taking too long." Keep finite caps (no infinite waits).
  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.resolvedBaseUrl,
    connectTimeout: const Duration(seconds: 35),
    receiveTimeout: const Duration(seconds: 45),
    sendTimeout: const Duration(seconds: 35),
    headers: {
      // Do not hardcode Sheikh's `default` slug — AuthInterceptor sets the
      // logged-in company slug (JWT tenant_id remains authoritative on API).
      if (AppConfig.hasExplicitTenantSlug)
        'X-Tenant-Slug': AppConfig.tenantSlug,
      'Content-Type': 'application/json',
      'User-Agent': 'SheikhGoTracking/${AppConfig.appVersion} Flutter',
    },
  ));

  if (kDebugMode) {
    dio.interceptors.add(LogInterceptor(
      requestBody: true,
      responseBody: true,
      error: true,
      logPrint: (obj) => debugPrint('[API] $obj'),
    ));
  }

  dio.interceptors.add(_RetryInterceptor(dio));
  dio.interceptors.add(_AuthInterceptor(dio, storage, sessionInvalidation));

  if (AppConfig.shouldPinCertificates) {
    _applyCertPinning(dio);
    debugPrint(
      '[Security] TLS pinning enabled (${AppConfig.certFingerprints.length} pin(s))',
    );
  } else if (AppConfig.isProd && !kDebugMode) {
    debugPrint(
      '[Security] WARNING: production build without CERT_PIN_* dart-defines',
    );
  }

  return dio;
});

/// Enforces leaf-certificate SHA-256 pins on every TLS handshake (not only
/// when the system rejects the cert — unlike badCertificateCallback alone).
void _applyCertPinning(Dio dio) {
  final pins = AppConfig.certFingerprints.toSet();

  dio.httpClientAdapter = IOHttpClientAdapter(
    createHttpClient: () {
      final client = HttpClient();
      // Still reject untrusted CAs unless they match a pin (rotation / custom CA).
      client.badCertificateCallback = (cert, host, port) {
        final fp =
            AppConfig.normalizeCertPin(sha256.convert(cert.der).toString());
        return pins.contains(fp);
      };
      return client;
    },
    validateCertificate: (cert, host, port) {
      if (cert == null) return false;
      final fp =
          AppConfig.normalizeCertPin(sha256.convert(cert.der).toString());
      final ok = pins.contains(fp);
      if (!ok) {
        debugPrint('[Security] TLS pin mismatch for $host ($fp)');
      }
      return ok;
    },
  );
}

// ── Retry interceptor ──────────────────────────────────────────────────────
class _RetryInterceptor extends Interceptor {
  const _RetryInterceptor(this._dio);
  final Dio _dio;

  static const _maxRetries = 3;
  static const _delays = [500, 1000, 2000]; // ms per attempt

  static bool _shouldRetry(DioException e) {
    // Let _AuthInterceptor handle 401
    if (e.response?.statusCode == 401) return false;
    // Don't retry client errors
    final status = e.response?.statusCode;
    if (status != null && status >= 400 && status < 500) return false;
    // Timeouts: another full wait (connect/receive) × retries makes cellular
    // login/dashboard feel hung before "Server is taking too long" appears.
    // Only retry clear transport drops and 5xx.
    if (e.type == DioExceptionType.connectionTimeout ||
        e.type == DioExceptionType.receiveTimeout ||
        e.type == DioExceptionType.sendTimeout) {
      return false;
    }
    return e.type == DioExceptionType.connectionError ||
        (status != null && status >= 500);
  }

  @override
  Future<void> onError(
      DioException err, ErrorInterceptorHandler handler) async {
    // Skip retry for auth endpoints to avoid refresh-token loops
    if (err.requestOptions.path.contains('/auth/')) {
      return handler.next(err);
    }

    // Avoid nested interceptor re-entry doubling retries; callers with their
    // own time budget (e.g. AI chat) opt out via `extra['noRetry']`.
    if (err.requestOptions.extra['isRetry'] == true ||
        err.requestOptions.extra['noRetry'] == true ||
        err.type == DioExceptionType.cancel) {
      return handler.next(err);
    }

    var attempt = err.requestOptions.extra['retryCount'] as int? ?? 0;
    var current = err;

    while (_shouldRetry(current) && attempt < _maxRetries) {
      await Future.delayed(Duration(milliseconds: _delays[attempt]));
      attempt += 1;
      current.requestOptions.extra['retryCount'] = attempt;
      current.requestOptions.extra['isRetry'] = true;
      try {
        final response = await _dio.fetch(current.requestOptions);
        return handler.resolve(response);
      } on DioException catch (retryErr) {
        current = retryErr;
      }
    }

    handler.next(current);
  }
}

// ── Auth interceptor ───────────────────────────────────────────────────────
class _AuthInterceptor extends QueuedInterceptor {
  _AuthInterceptor(this._dio, this._storage, this._sessionInvalidation);

  final Dio _dio;
  final FlutterSecureStorage _storage;
  final SessionInvalidationNotifier _sessionInvalidation;
  final Set<CancelToken> _authSessionCancelTokens = <CancelToken>{};

  static bool _isAuthRequest(RequestOptions options) =>
      options.path.contains('/auth/');

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    // Prefer the logged-in company's slug over a compile-time default so
    // Chand Travels never advertises Sheikh Travels' tenant slug.
    final sessionSlug = await _sessionTenantSlug();
    if (sessionSlug != null) {
      options.headers['X-Tenant-Slug'] = sessionSlug;
    } else if (!AppConfig.hasExplicitTenantSlug) {
      options.headers.remove('X-Tenant-Slug');
    }

    // Login, logout and token refresh must never carry a stale access token.
    if (_isAuthRequest(options)) {
      return handler.next(options);
    }

    var token = await _storage.read(key: _accessTokenKey);
    token ??= await _storage.read(key: _legacyAccessTokenKey);
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
      final cancelToken = options.cancelToken ?? CancelToken();
      options.cancelToken = cancelToken;
      _authSessionCancelTokens.add(cancelToken);
    }

    // Super Admin company filter → X-Tenant-Id.
    // Omit = JWT tenant only, except endpoints that opt in to all-companies
    // ([AllowCrossTenantBypass], e.g. GET /api/admin/vehicles).
    final scopedCompany =
        await _storage.read(key: _platformCompanyFilterKey);
    final companyId = int.tryParse(scopedCompany ?? '');
    if (companyId != null && companyId > 0) {
      options.headers['X-Tenant-Id'] = '$companyId';
    } else {
      options.headers.remove('X-Tenant-Id');
    }

    handler.next(options);
  }

  Future<String?> _sessionTenantSlug() async {
    final raw = await _storage.read(key: _sessionKey);
    if (raw == null || raw.isEmpty) return null;
    try {
      final map = jsonDecode(raw) as Map<String, dynamic>;
      final ctx = map['companyContext'];
      if (ctx is Map) {
        final slug = (ctx['slug'] ?? ctx['Slug'])?.toString().trim();
        if (slug != null && slug.isNotEmpty) return slug;
      }
      final top = (map['slug'] ?? map['Slug'])?.toString().trim();
      if (top != null && top.isNotEmpty) return top;
    } catch (_) {
      // Ignore corrupt session JSON — JWT still scopes the request.
    }
    return null;
  }

  @override
  void onResponse(Response response, ResponseInterceptorHandler handler) {
    final cancelToken = response.requestOptions.cancelToken;
    if (cancelToken != null) {
      _authSessionCancelTokens.remove(cancelToken);
    }
    handler.next(response);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final cancelToken = err.requestOptions.cancelToken;
    if (cancelToken != null) {
      _authSessionCancelTokens.remove(cancelToken);
    }

    // A refresh-token 401 is terminal. Retrying it would recurse forever and
    // leaving only the persisted credentials cleared keeps the UI "logged in".
    if (err.response?.statusCode == 401 &&
        !_isAuthRequest(err.requestOptions) &&
        err.requestOptions.extra['retriedAfterRefresh'] != true) {
      var refreshToken = await _storage.read(key: _refreshTokenKey);
      refreshToken ??= await _storage.read(key: _legacyRefreshTokenKey);
      if (refreshToken != null) {
        try {
          final res = await _dio.post<Map<String, dynamic>>(
            ApiEndpoints.refreshToken,
            data: {'refreshToken': refreshToken},
            options: Options(headers: {'Authorization': null}),
          );
          final data = res.data?['data'] as Map<String, dynamic>?;
          final newToken = data?['accessToken'] as String?;
          final newRefresh = data?['refreshToken'] as String?;
          if (newToken != null) {
            await _storage.write(key: _accessTokenKey, value: newToken);
            if (newRefresh != null) {
              await _storage.write(key: _refreshTokenKey, value: newRefresh);
            }
            err.requestOptions.headers['Authorization'] = 'Bearer $newToken';
            err.requestOptions.extra['retriedAfterRefresh'] = true;
            final retried = await _dio.fetch(err.requestOptions);
            return handler.resolve(retried);
          }
        } catch (_) {
          await _invalidateSession(SessionTerminationReason.refreshRejected);
          return handler.next(err);
        }
      }

      // Missing or malformed refresh credentials cannot restore this session.
      await _invalidateSession(SessionTerminationReason.expiredToken);
    }
    handler.next(err);
  }

  Future<void> _invalidateSession(SessionTerminationReason reason) async {
    final shouldProceed = _sessionInvalidation.requestTermination(reason);
    if (!shouldProceed) return;

    for (final token in _authSessionCancelTokens) {
      if (!token.isCancelled) {
        token.cancel('Session terminated');
      }
    }
    _authSessionCancelTokens.clear();

    await _storage.deleteAll();
  }
}

// ── Error formatting ───────────────────────────────────────────────────────

/// Legacy helper kept for existing screen code.
/// Prefer [ErrorHandler.message] for new code.
String formatDioError(Object e) => ErrorHandler.message(e);

/// Converts any caught exception to a typed [AppException].
AppException toAppException(Object e) {
  if (e is AppException) return e;
  if (e is DioException) return ErrorHandler.fromDio(e);
  return UnknownException(e.toString());
}
