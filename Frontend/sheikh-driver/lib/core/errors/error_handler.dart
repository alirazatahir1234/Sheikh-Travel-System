import 'package:dio/dio.dart';
import 'app_exception.dart';

class ErrorHandler {
  /// Maps a [DioException] to a typed [AppException].
  static AppException fromDio(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionError:
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.sendTimeout:
        return const NetworkException();

      case DioExceptionType.badResponse:
        final status = e.response?.statusCode;
        if (status == 401) return const AuthException();
        if (status == 404) return const NotFoundException();
        if (status == 400 || status == 422) {
          final body = e.response?.data;
          if (body is Map) {
            final msg = body['message'] ?? body['Message'] ?? body['title'];
            final errors = body['errors'];
            if (errors is Map && errors.isNotEmpty) {
              final first = errors.values.first;
              final detail = first is List ? first.first?.toString() : first?.toString();
              if (detail != null) return ValidationException(detail);
            }
            if (msg != null) return ValidationException(msg.toString());
          }
          return const ValidationException('Invalid request. Please check your input.');
        }
        if (status != null && status >= 500) return const ServerException();
        return UnknownException('Error $status');

      case DioExceptionType.cancel:
        return const UnknownException('Request cancelled.');

      default:
        final msg = e.message;
        return UnknownException(msg ?? 'An unexpected error occurred.');
    }
  }

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
