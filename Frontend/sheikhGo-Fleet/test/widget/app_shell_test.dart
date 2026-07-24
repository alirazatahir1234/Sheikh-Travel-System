import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/features/auth/data/auth_repository.dart';
import 'package:sheikh_go_driver/features/auth/domain/auth_models.dart';
import 'package:sheikh_go_driver/features/more/presentation/more_screen.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import 'package:sheikh_go_driver/shared/widgets/app_shell.dart';

FleetSession _managerSession() => FleetSession(
      accessToken: 'token',
      refreshToken: 'refresh',
      userId: 1,
      tenantId: 1,
      displayName: 'Ops Manager',
      roles: const [FleetRole.fleetManager],
      permissions: const [
        FleetPermissions.tripView,
        FleetPermissions.gpsView,
        FleetPermissions.vehicleView,
        FleetPermissions.driverView,
        FleetPermissions.reportView,
      ],
      authMode: AuthMode.staff,
    );

FleetSession _driverSession() => FleetSession(
      accessToken: 'token',
      refreshToken: 'refresh',
      userId: 2,
      tenantId: 1,
      displayName: 'Driver One',
      roles: const [FleetRole.driver],
      permissions: FleetPermissions.driverDefaults,
      authMode: AuthMode.driver,
      driverId: 99,
    );

Widget _app(GoRouter router, {required FleetSession session}) {
  return ProviderScope(
    overrides: [
      fleetSessionProvider.overrideWithValue(session),
    ],
    child: MaterialApp.router(
      routerConfig: router,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
    ),
  );
}

void main() {
  testWidgets('App shell shows role-filtered tabs for manager', (tester) async {
    final router = GoRouter(
      initialLocation: '/dashboard',
      routes: [
        ShellRoute(
          builder: (_, __, child) => AppShell(child: child),
          routes: [
            GoRoute(
              path: '/dashboard',
              builder: (_, __) => const Scaffold(body: Text('Dashboard body')),
            ),
            GoRoute(
              path: '/fleet',
              builder: (_, __) => const Scaffold(body: Text('Fleet body')),
            ),
            GoRoute(
              path: '/more',
              builder: (_, __) => const MoreScreen(),
            ),
          ],
        ),
      ],
    );

    await tester.pumpWidget(_app(router, session: _managerSession()));
    await tester.pumpAndSettle();

    expect(find.text('Dashboard'), findsWidgets);
    expect(find.text('Fleet'), findsWidgets);
    expect(find.text('Trips'), findsWidgets);
    expect(find.text('AI'), findsWidgets);
    expect(find.text('More'), findsWidgets);
    expect(find.text('Live'), findsNothing);
  });

  testWidgets('App shell hides fleet tab for driver-only session', (tester) async {
    final router = GoRouter(
      initialLocation: '/dashboard',
      routes: [
        ShellRoute(
          builder: (_, __, child) => AppShell(child: child),
          routes: [
            GoRoute(
              path: '/dashboard',
              builder: (_, __) => const Scaffold(body: Text('Dashboard body')),
            ),
          ],
        ),
      ],
    );

    await tester.pumpWidget(_app(router, session: _driverSession()));
    await tester.pumpAndSettle();

    expect(find.text('Dashboard'), findsWidgets);
    expect(find.text('Trips'), findsWidgets);
    expect(find.text('More'), findsWidgets);
    expect(find.text('Fleet'), findsNothing);
    expect(find.text('AI'), findsNothing);
  });

  testWidgets('More screen lists manager entries', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          fleetSessionProvider.overrideWithValue(_managerSession()),
        ],
        child: const MaterialApp(home: MoreScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Fleet live map'), findsOneWidget);
    expect(find.text('Drivers'), findsOneWidget);
    expect(find.text('Settings'), findsOneWidget);
    expect(find.text('Attendance'), findsNothing);
  });
}
