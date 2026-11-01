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
import 'package:sheikh_go_driver/shared/widgets/sg_ui.dart';

FleetSession _managerSession() => const FleetSession(
      accessToken: 'token',
      refreshToken: 'refresh',
      userId: 1,
      tenantId: 1,
      displayName: 'Ops Manager',
      roles: [FleetRole.fleetManager],
      permissions: [
        FleetPermissions.tripView,
        FleetPermissions.gpsView,
        FleetPermissions.vehicleView,
        FleetPermissions.driverView,
        FleetPermissions.reportView,
      ],
      authMode: AuthMode.staff,
    );

FleetSession _driverSession() => const FleetSession(
      accessToken: 'token',
      refreshToken: 'refresh',
      userId: 2,
      tenantId: 1,
      displayName: 'Driver One',
      roles: [FleetRole.driver],
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

    expect(find.text('Overview'), findsWidgets);
    expect(find.text('Map'), findsWidgets);
    expect(find.text('Vehicles'), findsWidgets);
    expect(find.text('Drivers'), findsWidgets);
    expect(find.text('More'), findsWidgets);
    expect(find.text('AI'), findsNothing);
  });

  testWidgets('App shell hides fleet tab for driver-only session',
      (tester) async {
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

    expect(find.text('Overview'), findsWidgets);
    expect(find.text('My Trips'), findsWidgets);
    expect(find.text('Attendance'), findsWidgets);
    expect(find.text('Fuel'), findsWidgets);
    expect(find.text('More'), findsWidgets);
    expect(find.text('Vehicles'), findsNothing);
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

    // Live Map + Drivers are bottom tabs — omitted from More grid.
    expect(find.text('Fleet live map'), findsNothing);
    expect(find.text('Drivers'), findsNothing);
    expect(find.text('Trips'), findsOneWidget);
    expect(find.text('Settings'), findsOneWidget);
    expect(find.text('Attendance'), findsNothing);
  });

  testWidgets('SgErrorView sanitizes raw DioException strings', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SgErrorView(
            message: 'DioException [bad response]: status code 500',
            onRetry: () {},
          ),
        ),
      ),
    );

    expect(find.text('Something went wrong'), findsOneWidget);
    expect(find.textContaining('DioException'), findsNothing);
    expect(
      find.text(
        'Could not complete the request. Please check your connection and try again.',
      ),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
  });

  testWidgets(
      'SgNotificationBellIcon renders 99+ overflow without Material Badge',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: SgNotificationBellIcon(unreadCount: 142),
        ),
      ),
    );

    expect(find.text('99+'), findsOneWidget);
    expect(find.byType(Badge), findsNothing);
  });
}
