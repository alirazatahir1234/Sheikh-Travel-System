import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/errors/app_exception.dart';
import 'package:sheikh_go_driver/core/errors/error_handler.dart';

DioException _makeDioException({
  DioExceptionType type = DioExceptionType.unknown,
  int? statusCode,
  dynamic responseData,
}) {
  final response = statusCode != null
      ? Response(
          requestOptions: RequestOptions(path: '/test'),
          statusCode: statusCode,
          data: responseData,
        )
      : null;
  return DioException(
    requestOptions: RequestOptions(path: '/test'),
    type: type,
    response: response,
  );
}

void main() {
  group('ErrorHandler.fromDio', () {
    test('connection error → NetworkException', () {
      final e = _makeDioException(type: DioExceptionType.connectionError);
      expect(ErrorHandler.fromDio(e), isA<NetworkException>());
    });

    test('connection timeout → NetworkException', () {
      final e = _makeDioException(type: DioExceptionType.connectionTimeout);
      expect(ErrorHandler.fromDio(e), isA<NetworkException>());
    });

    test('401 response → AuthException', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 401,
      );
      expect(ErrorHandler.fromDio(e), isA<AuthException>());
    });

    test('404 response → NotFoundException', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 404,
      );
      expect(ErrorHandler.fromDio(e), isA<NotFoundException>());
    });

    test('400 response with message → ValidationException with that message', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 400,
        responseData: {'message': 'Phone is required'},
      );
      final ex = ErrorHandler.fromDio(e);
      expect(ex, isA<ValidationException>());
      expect(ex.message, 'Phone is required');
    });

    test('422 response with errors map → ValidationException with first error', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 422,
        responseData: {
          'errors': {'Phone': ['Phone is invalid']}
        },
      );
      final ex = ErrorHandler.fromDio(e);
      expect(ex, isA<ValidationException>());
      expect(ex.message, 'Phone is invalid');
    });

    test('500 response → ServerException', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 500,
      );
      expect(ErrorHandler.fromDio(e), isA<ServerException>());
    });

    test('503 response → ServerException', () {
      final e = _makeDioException(
        type: DioExceptionType.badResponse,
        statusCode: 503,
      );
      expect(ErrorHandler.fromDio(e), isA<ServerException>());
    });
  });

  group('ErrorHandler.message', () {
    test('returns AppException message directly', () {
      const e = NetworkException('Custom network message');
      expect(ErrorHandler.message(e), 'Custom network message');
    });

    test('converts DioException message', () {
      final e = _makeDioException(type: DioExceptionType.connectionTimeout);
      final msg = ErrorHandler.message(e);
      expect(msg, isA<String>());
      expect(msg, isNotEmpty);
    });

    test('falls back to toString for unknown errors', () {
      expect(ErrorHandler.message(Exception('boom')), contains('boom'));
    });
  });

  group('ErrorHandler.isTransient', () {
    test('NetworkException is transient', () {
      expect(ErrorHandler.isTransient(const NetworkException()), isTrue);
    });

    test('ServerException is transient', () {
      expect(ErrorHandler.isTransient(const ServerException()), isTrue);
    });

    test('AuthException is not transient', () {
      expect(ErrorHandler.isTransient(const AuthException()), isFalse);
    });

    test('ValidationException is not transient', () {
      expect(ErrorHandler.isTransient(const ValidationException('bad')), isFalse);
    });
  });
}
