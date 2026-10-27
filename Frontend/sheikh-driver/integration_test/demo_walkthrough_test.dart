import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:sheikh_go_driver/main.dart' as app;

/// Automated UI pacing for `tools/record-ios-mobile-demo.sh`.
/// Credentials: `--dart-define=DEMO_EMAIL=...` `--dart-define=DEMO_PASSWORD=...`
const _email = String.fromEnvironment(
  'DEMO_EMAIL',
  defaultValue: 'admin@sheikhtravel.com',
);
const _password = String.fromEnvironment(
  'DEMO_PASSWORD',
  defaultValue: 'Pass@123',
);

Future<void> _pause(WidgetTester tester, Duration d) async {
  await tester.pump(d);
  // Avoid pumpAndSettle — maps / SignalR keep frames scheduled indefinitely.
  await tester.pump(const Duration(milliseconds: 400));
}

Future<void> _tapNavLabel(WidgetTester tester, String label) async {
  final tab = find.text(label);
  if (tab.evaluate().isEmpty) return;
  await tester.tap(tab.last);
  await _pause(tester, const Duration(seconds: 3));
}

void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  binding.framePolicy = LiveTestWidgetsFlutterBindingFramePolicy.fullyLive;
  // Demo capture tolerates benign layout overflows on small simulator widths.
  FlutterError.onError = (FlutterErrorDetails details) {
    final message = details.exceptionAsString();
    if (message.contains('overflowed') || message.contains('RenderFlex')) {
      return;
    }
    FlutterError.presentError(details);
  };

  testWidgets('Nexus iOS demo walkthrough', (tester) async {
    app.main();
    await _pause(tester, const Duration(seconds: 4));

    if (find.text('SheikhGo Tracking').evaluate().isEmpty) {
      await _pause(tester, const Duration(seconds: 2));
    }

    final fields = find.byType(TextFormField);
    expect(fields, findsAtLeastNWidgets(2));
    await tester.enterText(fields.at(0), _email);
    await tester.enterText(fields.at(1), _password);
    await tester.tap(find.text('Continue'));
    await _pause(tester, const Duration(seconds: 8));

    if (find.byType(BottomNavigationBar).evaluate().isEmpty) {
      // Stay on login / error — still gives usable splash+auth footage for the reel.
      await _pause(tester, const Duration(seconds: 4));
      return;
    }

    await _tapNavLabel(tester, 'Live Map');
    await _tapNavLabel(tester, 'Vehicles');

    final vehicleCard = find.byType(ListTile);
    if (vehicleCard.evaluate().isNotEmpty) {
      await tester.tap(vehicleCard.first);
      await _pause(tester, const Duration(seconds: 4));
      await tester.pageBack();
      await _pause(tester, const Duration(seconds: 2));
    }

    await _tapNavLabel(tester, 'Trips');
    await _tapNavLabel(tester, 'More');

    for (final label in ['Alerts', 'Reports', 'Settings']) {
      final entry = find.text(label);
      if (entry.evaluate().isNotEmpty) {
        await tester.ensureVisible(entry.first);
        await tester.tap(entry.first);
        await _pause(tester, const Duration(seconds: 3));
        if (label != 'Settings') {
          await tester.pageBack();
          await _pause(tester, const Duration(seconds: 1));
        }
      }
    }

    await _tapNavLabel(tester, 'Dashboard');
    await _pause(tester, const Duration(seconds: 2));
  });
}
