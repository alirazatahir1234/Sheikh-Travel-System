import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/core/security/device_integrity_service.dart';
import 'package:sheikh_go_driver/core/security/security_block_screen.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';

DeviceIntegrityReport cleanIntegrityReport({
  bool emulator = false,
  bool rooted = false,
  bool jailbroken = false,
  bool tampered = false,
  bool pinningConfigured = true,
}) =>
    DeviceIntegrityReport(
      isEmulator: emulator,
      isRooted: rooted,
      isJailbroken: jailbroken,
      isTampered: tampered,
      pinningConfigured: pinningConfigured,
      deviceId: 'test-device',
      platform: 'android',
      model: 'Pixel Test',
      osVersion: 'Android 14',
      appVersion: '1.0.0+1',
      packageName: 'com.sheikhgo.fleet',
      installerStore: 'com.android.vending',
    );

Override integrityOverride([DeviceIntegrityReport? report]) =>
    deviceIntegrityProvider.overrideWith(
      (ref) async => report ?? cleanIntegrityReport(),
    );

Widget wrapWithRouter({
  required Widget child,
  List<Override> overrides = const [],
  String initialLocation = '/',
}) {
  final router = GoRouter(
    initialLocation: initialLocation,
    routes: [
      GoRoute(path: '/', builder: (_, __) => child),
      GoRoute(
        path: '/offline-queue',
        builder: (_, __) => const Scaffold(body: Text('Offline queue')),
      ),
    ],
  );

  return ProviderScope(
    overrides: overrides,
    child: MaterialApp.router(
      routerConfig: router,
      locale: const Locale('en'),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
    ),
  );
}
