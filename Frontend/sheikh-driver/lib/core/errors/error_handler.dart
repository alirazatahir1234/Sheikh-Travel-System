import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'app_exception.dart';

class ErrorHandler {
  /// Maps a [DioException] to a typed [AppException].
  static AppException fromDio(DioException e) {
    debugPrint(
      'API ERROR type=${e.type} msg=${e.message} err=${e.error}',
    );
    if (kDebugMode) {
      debugPrint('$e\n${e.stackTrace}');
    }

    switch (e.type) {
      case DioExceptionType.badCertificate:
        return const NetworkException('Secure connection failed.');

      case DioExceptionType.connectionError:
        return _mapConnectionError(e);

      case DioExceptionType.connectionTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.sendTimeout:
        return const NetworkException(
          'Server is taking too long. Please try again.',
        );

      case DioExceptionType.badResponse:
        final status = e.response?.statusCode;
        final path = e.requestOptions.path.toLowerCase();
        final isAuthPath = path.contains('/auth/');
        if (status == 401) {
          // Login / refresh failures should surface the API message, not
          // "Session expired" (that copy is for mid-session 401s).
          final msg = _bodyMessage(e.response?.data);
          final sessionEnded = e.response?.headers.value('x-session-ended');
          if (isAuthPath) {
            return ValidationException(
              msg ?? 'Invalid email or password.',
            );
          }
          if (sessionEnded == 'superseded' ||
              (msg != null && msg.toLowerCase().contains('another login'))) {
            // Legacy kick-oldest path (no longer issued on new logins).
            return AuthException(
              msg ?? 'Your session is no longer valid. Please log in again.',
            );
          }
          return AuthException(msg ?? 'Session expired. Please log in again.');
        }
        if (status == 403) {
          final msg = _bodyMessage(e.response?.data);
          if (isAuthPath && msg != null) {
            return ValidationException(msg);
          }
          if (path.contains('attendance')) {
            return ForbiddenException.attendance;
          }
          return const ForbiddenException();
        }
        if (status == 404) {
          // Dead Railway / trycloudflare / wrong host → 404 on /auth/*.
          // Prefer our copy over host platform messages like "Application not found".
          if (isAuthPath) {
            return const ValidationException(
              'Cannot reach login API. Update the app or check network.',
            );
          }
          return const NotFoundException();
        }
        if (status == 408) {
          return const NetworkException(
            'Server is taking too long. Please try again.',
          );
        }
        if (status == 409) {
          final msg = _bodyMessage(e.response?.data);
          return ConflictException(
            msg ?? 'This action conflicts with the current state.',
          );
        }
        if (status == 429) {
          final msg = _bodyMessage(e.response?.data);
          return RateLimitException(
            msg ?? 'Too many attempts. Please wait a minute and try again.',
          );
        }
        if (status == 400 || status == 422) {
          final body = e.response?.data;
          if (body is Map) {
            final msg = body['message'] ?? body['Message'] ?? body['title'];
            final errors = body['errors'];
            if (errors is Map && errors.isNotEmpty) {
              final first = errors.values.first;
              final detail =
                  first is List ? first.first?.toString() : first?.toString();
              if (detail != null) return ValidationException(detail);
            }
            if (errors is List && errors.isNotEmpty) {
              final detail = errors.first?.toString();
              if (detail != null && detail.trim().isNotEmpty) {
                return ValidationException(detail.trim());
              }
            }
            if (msg != null) return ValidationException(msg.toString());
          }
          return const ValidationException(
            'Invalid request. Please check your input.',
          );
        }
        if (status != null && status >= 500) return const ServerException();
        final fallback = _bodyMessage(e.response?.data);
        if (fallback != null) return UnknownException(fallback);
        return UnknownException('Server error ($status).');

      case DioExceptionType.cancel:
        return const UnknownException('Request cancelled.');

      default:
        final detail = _combinedDetail(e);
        if (_isTlsFailure(detail)) {
          return const NetworkException('Secure connection failed.');
        }
        if (_isDnsFailure(detail)) {
          return const NetworkException('Cannot reach server.');
        }
        final msg = e.message;
        return UnknownException(msg ?? 'An unexpected error occurred.');
    }
  }

  static AppException _mapConnectionError(DioException e) {
    final detail = _combinedDetail(e);
    if (_isTlsFailure(detail)) {
      return const NetworkException('Secure connection failed.');
    }
    if (_isDnsFailure(detail)) {
      return const NetworkException('Cannot reach server.');
    }
    if (detail.contains('connection refused')) {
      return const NetworkException(
        'Server unavailable right now. Please try again shortly.',
      );
    }
    return const NetworkException();
  }

  static String? _bodyMessage(Object? data) {
    if (data is Map) {
      final msg = data['message'] ?? data['Message'] ?? data['title'];
      if (msg != null && msg.toString().trim().isNotEmpty) {
        return msg.toString().trim();
      }
    }
    return null;
  }

  static String _combinedDetail(DioException e) {
    final parts = <String>[
      e.message ?? '',
      e.error?.toString() ?? '',
    ];
    return parts.join(' ').toLowerCase();
  }

  static bool _isTlsFailure(String detail) =>
      detail.contains('handshakeexception') ||
      detail.contains('certificate_verify_failed') ||
      detail.contains('certificateverifyfailed') ||
      detail.contains('bad certificate') ||
      detail.contains('ssl');

  static bool _isDnsFailure(String detail) =>
      detail.contains('failed host lookup') ||
      detail.contains('nodename nor servname') ||
      detail.contains('name or service not known') ||
      detail.contains('getaddrinfo failed');

  /// Returns a user-friendly message from any caught exception.
  static String message(Object error) {
    if (error is AppException) return error.message;
    if (error is DioException) return fromDio(error).message;
    return error.toString();
  }

  /// Returns true if the exception is transient (worth retrying).
  static bool isTransient(AppException e) =>
      e is NetworkException || e is ServerException;
}
