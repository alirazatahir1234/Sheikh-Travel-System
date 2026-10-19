import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/auth/presentation/login_screen.dart';

void main() {
  testWidgets('Login screen shows brand and form fields', (tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: MaterialApp(home: LoginScreen()),
      ),
    );

    expect(find.text('SheikhGo Tracking'), findsOneWidget);
    expect(find.text('Continue'), findsOneWidget);
    expect(find.text('Email or phone'), findsOneWidget);
    expect(find.text('Password'), findsOneWidget);
  });

  testWidgets('Login validates empty fields', (tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: MaterialApp(home: LoginScreen()),
      ),
    );

    await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
    await tester.pump();

    expect(find.text('Email or phone is required'), findsOneWidget);
    expect(find.text('Password is required'), findsOneWidget);
  });
}
