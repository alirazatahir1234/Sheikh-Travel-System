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

const _accessTokenKey = 'driver_access_token';
const _refreshTokenKey = 'driver_refresh_token';

final secureStorageProvider = Provider((_) => const FlutterSecureStorage());

final dioProvider = Provider<Dio>((ref) {
  final storage = ref.read(secureStorageProvider);
  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.baseUrl,
    connectTimeout: const Duration(seconds: 20),
    receiveTimeout: const Duration(seconds: 20),
    headers: {
      'X-Tenant-Slug': AppConfig.tenantSlug,
      'Content-Type': 'application/json',
      'User-Agent': 'SheikhGoDriver/${AppConfig.appVersion} Flutter',
    },
  ));

  // Debug-only request/response logging
  if (kDebugMode) {
    dio.interceptors.add(LogInterceptor(
      requestBody: true,
      responseBody: true,
      error: true,
      logPrint: (obj) => debugPrint('[API] $obj'),
    ));
  }

  // Retry on transient failures (before auth so retries still get fresh tokens)
  dio.interceptors.add(_RetryInterceptor(dio));

  // Auth token injection + silent 401 refresh (last so error handling fires first)
  dio.interceptors.add(_AuthInterceptor(dio, storage));

  // Certificate pinning — production only, only when fingerprints are configured
  if (!kDebugMode && AppConfig.certFingerprints.isNotEmpty) {
    _applyCertPinning(dio);
  }

  return dio;
});

void _applyCertPinning(Dio dio) {
  (dio.httpClientAdapter as IOHttpClientAdapter).createHttpClient = () {
    final client = HttpClient();
    client.badCertificateCallback = (cert, host, port) {
      final fingerprint = sha256.convert(cert.der).toString();
      return AppConfig.certFingerprints.contains(fingerprint);
    };
    return client;
  };
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
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
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
  _AuthInterceptor(this._dio, this._storage);

  final Dio _dio;
  final FlutterSecureStorage _storage;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await _storage.read(key: _accessTokenKey);
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode == 401) {
      final refreshToken = await _storage.read(key: _refreshTokenKey);
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
            final retried = await _dio.fetch(err.requestOptions);
            return handler.resolve(retried);
          }
        } catch (_) {
          await _storage.deleteAll();
        }
      }
    }
    handler.next(err);
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
