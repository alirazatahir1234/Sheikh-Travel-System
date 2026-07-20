import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:sheikh_go_driver/main.dart';

import '../test/helpers/test_fixtures.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('App launches and shows login screen', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [integrityOverride()],
        child: const SheikhGoDriverApp(),
      ),
    );

    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle(const Duration(seconds: 2));

    expect(find.text('SheikhGo Driver'), findsAny);
    expect(find.text('Sign in'), findsWidgets);
    expect(find.text('Phone number'), findsOneWidget);
  });
}
