import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/main.dart';

void main() {
  testWidgets('SheikhGo Driver app smoke test', (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: SheikhGoDriverApp()));
    await tester.pump();
    expect(find.text('SheikhGo Driver'), findsAny);
  });
}
