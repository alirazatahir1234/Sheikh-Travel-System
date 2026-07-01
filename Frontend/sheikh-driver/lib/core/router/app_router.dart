import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/dashboard/presentation/dashboard_screen.dart';
import '../../features/trips/presentation/trips_screen.dart';
import '../../features/trips/presentation/trip_detail_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/notifications/presentation/notifications_screen.dart';
import '../../features/gps/presentation/live_map_screen.dart';
import '../../features/attendance/presentation/attendance_screen.dart';
import '../../features/fuel/presentation/fuel_screen.dart';
import '../../features/timeline/presentation/timeline_screen.dart';
import '../../features/settings/presentation/settings_screen.dart';
import '../../shared/widgets/app_shell.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final authNotifier = ref.watch(authRepositoryProvider);

  return GoRouter(
    initialLocation: '/dashboard',
    redirect: (context, state) {
      final isLoggedIn = authNotifier.isLoggedIn;
      final onLogin = state.matchedLocation == '/login';
      if (!isLoggedIn && !onLogin) return '/login';
      if (isLoggedIn && onLogin) return '/dashboard';
      return null;
    },
    refreshListenable: authNotifier,
    routes: [
      GoRoute(
        path: '/login',
        builder: (_, __) => const LoginScreen(),
      ),
      ShellRoute(
        builder: (context, state, child) => AppShell(child: child),
        routes: [
          GoRoute(
            path: '/dashboard',
            builder: (_, __) => const DashboardScreen(),
          ),
          GoRoute(
            path: '/trips',
            builder: (_, __) => const TripsScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => TripDetailScreen(
                  tripId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/live',
            builder: (_, __) => const LiveMapScreen(),
          ),
          GoRoute(
            path: '/notifications',
            builder: (_, __) => const NotificationsScreen(),
          ),
          GoRoute(
            path: '/profile',
            builder: (_, __) => const ProfileScreen(),
          ),
          GoRoute(
            path: '/attendance',
            builder: (_, __) => const AttendanceScreen(),
          ),
          GoRoute(
            path: '/fuel',
            builder: (_, __) => const FuelScreen(),
          ),
          GoRoute(
            path: '/timeline',
            builder: (_, __) => const TimelineScreen(),
          ),
          GoRoute(
            path: '/settings',
            builder: (_, __) => const SettingsScreen(),
          ),
        ],
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: Center(child: Text('Page not found: ${state.uri}')),
    ),
  );
});
