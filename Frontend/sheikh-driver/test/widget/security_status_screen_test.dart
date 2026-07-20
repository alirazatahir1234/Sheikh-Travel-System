import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/security/presentation/security_status_screen.dart';

import '../helpers/test_fixtures.dart';

void main() {
  testWidgets('Security status shows Device OK for clean report', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [integrityOverride()],
        child: const MaterialApp(home: SecurityStatusScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Security status'), findsOneWidget);
    expect(find.text('Device OK'), findsOneWidget);
    expect(find.text('com.sheikhgo.driver'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Re-run checks'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Re-run checks'), findsOneWidget);
  });
}
