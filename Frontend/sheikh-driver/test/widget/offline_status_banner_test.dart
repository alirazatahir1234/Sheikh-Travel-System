import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/offline/connectivity_provider.dart';
import 'package:sheikh_go_driver/core/offline/offline_sync_service.dart';
import 'package:sheikh_go_driver/features/offline/presentation/offline_status_banner.dart';

import '../helpers/test_fixtures.dart';

void main() {
  testWidgets('hides when online with empty queue', (tester) async {
    await tester.pumpWidget(
      wrapWithRouter(
        child: const OfflineStatusBanner(),
        overrides: [
          isOnlineProvider.overrideWith((ref) => true),
          offlinePendingCountProvider.overrideWith((ref) => 0),
        ],
      ),
    );
    await tester.pump();

    expect(find.textContaining('offline'), findsNothing);
    expect(find.textContaining('waiting to sync'), findsNothing);
  });

  testWidgets('shows offline message', (tester) async {
    await tester.pumpWidget(
      wrapWithRouter(
        child: const OfflineStatusBanner(),
        overrides: [
          isOnlineProvider.overrideWith((ref) => false),
          offlinePendingCountProvider.overrideWith((ref) => 2),
        ],
      ),
    );
    await tester.pump();

    expect(find.textContaining('You are offline'), findsOneWidget);
    expect(find.textContaining('2 queued'), findsOneWidget);
  });

  testWidgets('shows pending sync count when online', (tester) async {
    await tester.pumpWidget(
      wrapWithRouter(
        child: const OfflineStatusBanner(),
        overrides: [
          isOnlineProvider.overrideWith((ref) => true),
          offlinePendingCountProvider.overrideWith((ref) => 1),
        ],
      ),
    );
    await tester.pump();

    expect(find.text('1 action waiting to sync'), findsOneWidget);
    expect(find.text('Sync'), findsOneWidget);
  });
}
