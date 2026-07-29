import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/api/dio_client.dart';

void main() {
  group('SessionInvalidationNotifier', () {
    test('allows only one termination request at a time', () {
      final notifier = SessionInvalidationNotifier();

      final first =
          notifier.requestTermination(SessionTerminationReason.expiredToken);
      final second =
          notifier.requestTermination(SessionTerminationReason.refreshRejected);

      expect(first, isTrue);
      expect(second, isFalse);
      expect(notifier.isTerminating, isTrue);
      expect(
        notifier.reason,
        SessionTerminationReason.expiredToken,
      );
    });

    test('can be reset after termination completes', () {
      final notifier = SessionInvalidationNotifier();

      notifier.requestTermination(SessionTerminationReason.refreshRejected);
      notifier.completeTermination();

      final next = notifier.requestTermination(SessionTerminationReason.manual);
      expect(next, isTrue);
      expect(notifier.reason, SessionTerminationReason.manual);
    });
  });
}
