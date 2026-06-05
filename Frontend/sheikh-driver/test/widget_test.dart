import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:sheikh_driver/main.dart';

void main() {
  testWidgets('login screen smoke test', (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: SheikhDriverApp()));
    await tester.pump();

    expect(find.text('Sheikh Driver'), findsOneWidget);
  });
}
