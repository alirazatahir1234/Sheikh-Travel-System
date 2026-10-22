import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/notifications/services/push_setup_issue.dart';

void main() {
  group('classifyPushFailure', () {
    test('maps missing Firebase config', () {
      final issue = classifyPushFailure(
        StateError(
          'Firebase is not initialized. Add android/app/google-services.json',
        ),
        showSnackBar: false,
      );
      expect(issue.reason, PushSetupReason.firebaseNotInitialized);
      expect(issue.showSnackBar, isFalse);
      expect(issue.message, contains('google-services.json'));
    });

    test('maps Play Services / SERVICE_NOT_AVAILABLE', () {
      final issue = classifyPushFailure(
        Exception('FCM getToken failed: java.io.IOException: SERVICE_NOT_AVAILABLE'),
        showSnackBar: true,
      );
      expect(issue.reason, PushSetupReason.playServicesUnavailable);
      expect(issue.message, contains('Google Play'));
    });

    test('falls back for unknown errors', () {
      final issue = classifyPushFailure(
        Exception('boom'),
        showSnackBar: true,
      );
      expect(issue.reason, PushSetupReason.initFailed);
      expect(issue.message, contains('boom'));
    });
  });
}
