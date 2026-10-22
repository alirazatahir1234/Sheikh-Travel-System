import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/api/api_response.dart';
import 'package:sheikh_go_driver/core/errors/app_exception.dart';

void main() {
  group('ApiResponseParser', () {
    test('ensureSuccess throws on success:false with message', () {
      expect(
        () => ApiResponseParser.ensureSuccess({
          'success': false,
          'message': 'Invalid email or password.',
          'data': null,
        }),
        throwsA(
          isA<ValidationException>().having(
            (e) => e.message,
            'message',
            'Invalid email or password.',
          ),
        ),
      );
    });

    test('ensureSuccess accepts Success PascalCase failure', () {
      expect(
        () => ApiResponseParser.ensureSuccess({
          'Success': false,
          'Message': 'Locked out',
        }),
        throwsA(isA<ValidationException>()),
      );
    });

    test('requireDataMap does not fall back to envelope on null data', () {
      expect(
        () => ApiResponseParser.requireDataMap({
          'success': false,
          'message': 'Invalid email or password.',
          'data': null,
        }),
        throwsA(
          isA<ValidationException>().having(
            (e) => e.message,
            'message',
            'Invalid email or password.',
          ),
        ),
      );
    });

    test('requireDataMap returns data map on success', () {
      final data = ApiResponseParser.requireDataMap({
        'success': true,
        'message': 'ok',
        'data': {'accessToken': 'a', 'refreshToken': 'r'},
      });
      expect(data['accessToken'], 'a');
    });

    test('requireDataMap fails when success true but data null', () {
      expect(
        () => ApiResponseParser.requireDataMap({
          'success': true,
          'message': 'weird',
          'data': null,
        }),
        throwsA(isA<ValidationException>()),
      );
    });
  });
}
