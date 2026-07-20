import 'dart:async';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'core/constants/app_theme.dart';
import 'core/providers/theme_provider.dart';
import 'core/router/app_router.dart';
import 'features/gps/services/location_queue.dart';
import 'features/gps/services/gps_background_service.dart';
import 'features/gps/services/gps_session_store.dart';
import 'features/gps/services/background_gps_tracker.dart';
import 'features/notifications/presentation/foreground_banner.dart';
import 'features/notifications/presentation/notifications_notifier.dart';
import 'features/notifications/services/fcm_service.dart';
import 'core/offline/offline_outbox.dart';
import 'core/offline/trips_cache.dart';
import 'core/api/dio_client.dart';
import 'core/security/security_block_screen.dart';
import 'core/analytics/analytics_service.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  try {
    await Firebase.initializeApp();
    FlutterError.onError = FirebaseCrashlytics.instance.recordFlutterFatalError;
    PlatformDispatcher.instance.onError = (error, stack) {
      FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
      return true;
    };
    await FirebaseCrashlytics.instance
        .setCrashlyticsCollectionEnabled(!kDebugMode);
    await AnalyticsService.instance.init();
  } catch (e) {
    debugPrint('[Firebase] Init skipped: $e');
  }

  await Hive.initFlutter();
  final prefsBox = await Hive.openBox('prefs');
  await LocationQueue.init();
  await OfflineOutbox.init();
  await TripsCache.init();
  await GpsSessionStore.init();
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

class SheikhGoDriverApp extends ConsumerStatefulWidget {
  const SheikhGoDriverApp({super.key});

  @override
  ConsumerState<SheikhGoDriverApp> createState() => _SheikhGoDriverAppState();
}

class _SheikhGoDriverAppState extends ConsumerState<SheikhGoDriverApp> {
  StreamSubscription? _bannerSub;
  StreamSubscription? _refreshSub;

  @override
  void initState() {
    super.initState();
    _bannerSub = FcmService.instance.foregroundBanners.listen((event) {
      ref.read(foregroundBannerProvider.notifier).show(event);
    });
    _refreshSub = FcmService.instance.inboxRefresh.listen((_) {
      ref.invalidate(notificationsListProvider);
      ref.invalidate(unreadNotificationsCountProvider);
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      final router = ref.read(routerProvider);
      FcmService.handleMessageTaps(onTap: (route) {
        if (route == null || route.isEmpty) {
          router.go('/notifications');
          return;
        }
        router.push(route);
      });

      final dio = ref.read(dioProvider);
      BackgroundGpsTracker.instance.bindDio(dio);
      BackgroundGpsTracker.instance.resumeIfNeeded(dio: dio);
    });
  }

  @override
  void dispose() {
    _bannerSub?.cancel();
    _refreshSub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final integrity = ref.watch(deviceIntegrityProvider);
    final router = ref.watch(routerProvider);
    final isDark = ref.watch(darkModeProvider);

    return integrity.when(
      loading: () => MaterialApp(
        theme: AppTheme.light,
        darkTheme: AppTheme.dark,
        themeMode: isDark ? ThemeMode.dark : ThemeMode.light,
        home: const Scaffold(
          body: Center(child: CircularProgressIndicator()),
        ),
        debugShowCheckedModeBanner: false,
      ),
      error: (_, __) => _buildRouterApp(router, isDark),
      data: (report) {
        if (report.hasBlockingIssue) {
          return MaterialApp(
            theme: AppTheme.light,
            darkTheme: AppTheme.dark,
            themeMode: isDark ? ThemeMode.dark : ThemeMode.light,
            home: SecurityBlockScreen(report: report),
            debugShowCheckedModeBanner: false,
          );
        }
        return _buildRouterApp(router, isDark);
      },
    );
  }

  Widget _buildRouterApp(GoRouter router, bool isDark) {
    return MaterialApp.router(
      title: 'SheikhGo Driver',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: isDark ? ThemeMode.dark : ThemeMode.light,
      routerConfig: router,
      debugShowCheckedModeBanner: false,
      builder: (context, child) => Stack(
        children: [
          child ?? const SizedBox.shrink(),
          const ForegroundNotificationBanner(),
        ],
      ),
    );
  }
}
