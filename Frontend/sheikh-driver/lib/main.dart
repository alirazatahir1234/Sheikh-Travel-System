import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'core/constants/app_theme.dart';
import 'core/providers/theme_provider.dart';
import 'core/router/app_router.dart';
import 'features/gps/services/location_queue.dart';
import 'features/gps/services/gps_background_service.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Firebase — requires google-services.json (Android) and
  // GoogleService-Info.plist (iOS) from your Firebase console project.
  try {
    await Firebase.initializeApp();
    FlutterError.onError = FirebaseCrashlytics.instance.recordFlutterFatalError;
    PlatformDispatcher.instance.onError = (error, stack) {
      FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
      return true;
    };
    // Disable Crashlytics in debug so test crashes don't pollute the dashboard
    await FirebaseCrashlytics.instance
        .setCrashlyticsCollectionEnabled(!kDebugMode);
  } catch (e) {
    // Firebase config files not yet added — continue without crash reporting.
    debugPrint('[Firebase] Init skipped: $e');
  }

  await Hive.initFlutter();
  final prefsBox = await Hive.openBox('prefs');
  await LocationQueue.init();
  await GpsBackgroundService.initialize();
  await GpsBackgroundService.registerDrainTask();
  final isDark = prefsBox.get('darkMode', defaultValue: false) as bool;
  runApp(ProviderScope(
    overrides: [
      darkModeProvider.overrideWith((ref) => isDark),
    ],
    child: const SheikhGoDriverApp(),
  ));
}

class SheikhGoDriverApp extends ConsumerWidget {
  const SheikhGoDriverApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final isDark = ref.watch(darkModeProvider);
    return MaterialApp.router(
      title: 'SheikhGo Driver',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: isDark ? ThemeMode.dark : ThemeMode.light,
      routerConfig: router,
      debugShowCheckedModeBanner: false,
    );
  }
}
