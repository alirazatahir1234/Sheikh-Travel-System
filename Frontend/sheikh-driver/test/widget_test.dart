import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/auth/presentation/login_screen.dart';

void main() {
  testWidgets('SheikhGo Driver app smoke test', (WidgetTester tester) async {
    // Full app boot needs Firebase/Hive; login screen covers the brand smoke check.
    await tester.pumpWidget(
      const ProviderScope(
        child: MaterialApp(home: LoginScreen()),
      ),
    );
    await tester.pump();

    expect(find.text('SheikhGo Driver'), findsOneWidget);
    expect(find.text('Sign in'), findsWidgets);
  });
}
