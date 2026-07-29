import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/auth/presentation/splash_screen.dart';

void main() {
  group('resolveSplashRoute', () {
    test('logged in goes to dashboard', () {
      expect(
        resolveSplashRoute(isLoggedIn: true, sessionExpired: false),
        '/dashboard',
      );
    });

    test('session expired goes to session-expired', () {
      expect(
        resolveSplashRoute(isLoggedIn: false, sessionExpired: true),
        '/session-expired',
      );
    });

    test('logged out goes to login', () {
      expect(
        resolveSplashRoute(isLoggedIn: false, sessionExpired: false),
        '/login',
      );
    });
  });

  testWidgets('SplashBrandPanel shows Smart GPS Operations tagline', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData.dark(),
        home: SplashBrandPanel(
          isDark: true,
          logoFade: const AlwaysStoppedAnimation(1),
          logoScale: const AlwaysStoppedAnimation(1),
          loaderValue: const AlwaysStoppedAnimation(0.5),
        ),
      ),
    );

    expect(find.text(kSplashTagline), findsOneWidget);
    expect(find.text('Loading…'), findsOneWidget);
    expect(find.byType(SplashLoaderBar), findsOneWidget);
  });
}
