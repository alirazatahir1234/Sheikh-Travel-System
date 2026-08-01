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
  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.resolvedBaseUrl,
    connectTimeout: const Duration(seconds: 20),
    receiveTimeout: const Duration(seconds: 20),
    headers: {
      'X-Tenant-Slug': AppConfig.tenantSlug,
      'Content-Type': 'application/json',
      'User-Agent': 'SheikhGoFleet/${AppConfig.appVersion} Flutter',
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
    return e.type == DioExceptionType.connectionError ||
        e.type == DioExceptionType.connectionTimeout ||
        e.type == DioExceptionType.receiveTimeout ||
        (status != null && status >= 500);
  }

  @override
  Future<void> onError(
      DioException err, ErrorInterceptorHandler handler) async {
    // Skip retry for auth endpoints to avoid refresh-token loops
    if (err.requestOptions.path.contains('/auth/')) {
      return handler.next(err);
    }

    final retryCount = err.requestOptions.extra['retryCount'] as int? ?? 0;

    if (_shouldRetry(err) && retryCount < _maxRetries) {
      await Future.delayed(Duration(milliseconds: _delays[retryCount]));
      err.requestOptions.extra['retryCount'] = retryCount + 1;
      try {
        final response = await _dio.fetch(err.requestOptions);
        return handler.resolve(response);
      } on DioException catch (retryErr) {
        return handler.next(retryErr);
      }
    }

    handler.next(err);
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
    handler.next(options);
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
