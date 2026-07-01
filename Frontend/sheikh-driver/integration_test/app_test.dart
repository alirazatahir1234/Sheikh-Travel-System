import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:sheikh_go_driver/main.dart' as app;

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('App launches and shows login screen', (tester) async {
    app.main();
    await tester.pumpAndSettle(const Duration(seconds: 3));

    // The login screen has a phone field — verify the app booted
    expect(find.text('SheikhGo Driver'), findsAny);
  });
}
