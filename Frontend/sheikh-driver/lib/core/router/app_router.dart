import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/auth/domain/auth_models.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/dashboard/presentation/dashboard_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/notifications/presentation/notifications_screen.dart';
import '../../features/gps/presentation/live_map_screen.dart';
import '../../features/attendance/presentation/attendance_screen.dart';
import '../../features/timeline/presentation/timeline_screen.dart';
import '../../features/settings/presentation/settings_screen.dart';
import '../../features/earnings/presentation/earnings_screen.dart';
import '../../features/inspection/presentation/inspection_screen.dart';
import '../../features/navigation/presentation/trip_navigation_screen.dart';
import '../../features/offline/presentation/offline_queue_screen.dart';
import '../../features/security/presentation/security_status_screen.dart';
import '../../features/legal/presentation/legal_document_screen.dart';
import '../../features/payments/presentation/collect_payment_screen.dart';
import '../../features/fleet/presentation/fleet_hub_screen.dart';
import '../../features/fleet/presentation/fleet_live_map_screen.dart';
import '../../features/fleet/presentation/vehicle_detail_screen.dart';
import '../../features/fleet/presentation/vehicle_history_screen.dart';
import '../../features/ai/presentation/ai_copilot_screen.dart';
import '../../features/more/presentation/more_screen.dart';
import '../../features/drivers/presentation/drivers_list_screen.dart';
import '../../features/drivers/presentation/driver_detail_screen.dart';
import '../../features/ops_trips/presentation/trips_entry_screen.dart';
import '../../features/ops_trips/presentation/trip_detail_entry_screen.dart';
import '../../features/bookings/presentation/bookings_queue_screen.dart';
import '../../features/bookings/presentation/booking_detail_screen.dart';
import '../../features/alerts/presentation/alerts_screen.dart';
import '../../features/maintenance/presentation/maintenance_hub_screen.dart';
import '../../features/staff_fuel/presentation/fuel_entry_screen.dart';
import '../../features/staff_fuel/presentation/staff_fuel_list_screen.dart';
import '../../features/compliance/presentation/documents_entry_screen.dart';
import '../../features/reports/presentation/reports_hub_screen.dart';
import '../../core/navigation/fleet_nav_config.dart';
import '../../shared/widgets/app_shell.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final authNotifier = ref.watch(authRepositoryProvider);

  return GoRouter(
    initialLocation: '/dashboard',
    redirect: (context, state) {
      final session = authNotifier.session;
      final isLoggedIn = authNotifier.isLoggedIn;
      final loc = state.matchedLocation;
      final onLogin = loc == '/login';

      if (!isLoggedIn && !onLogin) return '/login';
      if (isLoggedIn && onLogin) return '/dashboard';

      if (session != null && FleetNavConfig.isShellRoute(loc)) {
        if (loc.startsWith('/fleet') && !session.canSeeFleetTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/trips') && !session.canSeeTripsTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/ai') && !session.canSeeAiTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/bookings') && !session.canSeeBookingsTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/finance') && !session.canSeeFinanceTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/users') && !session.canSeeUsersTab) {
          return '/more';
        }
        if (loc.startsWith('/alerts') &&
            (session.isDriverOnly ||
                !session.hasPermission(FleetPermissions.gpsView))) {
          return '/notifications';
        }
        if (loc.startsWith('/more/drivers') &&
            (session.isDriverOnly ||
                !session.hasPermission(FleetPermissions.driverView))) {
          return '/more';
        }
        if (loc.startsWith('/more/maintenance') &&
            (session.isDriverOnly ||
                !session.hasPermission(FleetPermissions.maintenanceView))) {
          return '/more';
        }
        if (loc.startsWith('/more/reports') &&
            (session.isDriverOnly ||
                !session.hasPermission(FleetPermissions.reportView))) {
          return '/more';
        }
        if (loc == '/live' && !session.isDriverSession) {
          return '/dashboard';
        }
      }

      return null;
    },
    refreshListenable: authNotifier,
    routes: [
      GoRoute(
        path: '/login',
        builder: (_, __) => const LoginScreen(),
      ),
      GoRoute(
        path: '/trips/:id/navigate',
        builder: (_, state) => TripNavigationScreen(
          tripId: int.parse(state.pathParameters['id']!),
        ),
      ),
      GoRoute(
        path: '/trips/:id/collect-payment',
        builder: (_, state) => CollectPaymentScreen(
          tripId: int.parse(state.pathParameters['id']!),
        ),
      ),
      ShellRoute(
        builder: (context, state, child) => AppShell(child: child),
        routes: [
          GoRoute(
            path: '/dashboard',
            builder: (_, __) => const DashboardScreen(),
          ),
          GoRoute(
            path: '/fleet',
            builder: (_, __) => const FleetHubScreen(),
            routes: [
              GoRoute(
                path: 'map',
                builder: (_, __) => const FleetLiveMapScreen(),
              ),
              GoRoute(
                path: 'vehicles/:id',
                builder: (_, state) => VehicleDetailScreen(
                  vehicleId: int.parse(state.pathParameters['id']!),
                ),
                routes: [
                  GoRoute(
                    path: 'history',
                    builder: (_, state) => VehicleHistoryScreen(
                      vehicleId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/trips',
            builder: (_, __) => const TripsEntryScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => TripDetailEntryScreen(
                  tripId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/ai',
            builder: (_, state) => AiCopilotScreen(
              initialPrompt: state.uri.queryParameters['q'],
            ),
          ),
          GoRoute(
            path: '/bookings',
            builder: (_, __) => const BookingsQueueScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => BookingDetailScreen(
                  bookingId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/finance',
            builder: (_, __) => const ComingSoonScreen(
              title: 'Finance',
              sprintLabel: 'Phase 6 — Accountant',
            ),
          ),
          GoRoute(
            path: '/users',
            builder: (_, __) => const ComingSoonScreen(
              title: 'Users',
              sprintLabel: 'Phase 7 — Tenant Admin',
            ),
          ),
          GoRoute(
            path: '/alerts',
            builder: (_, __) => const AlertsScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => AlertDetailScreen(
                  alertId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/more',
            builder: (_, __) => const MoreScreen(),
            routes: [
              GoRoute(
                path: 'drivers',
                builder: (_, __) => const DriversListScreen(),
                routes: [
                  GoRoute(
                    path: ':id',
                    builder: (_, state) => DriverDetailScreen(
                      driverId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
              GoRoute(
                path: 'maintenance',
                builder: (_, __) => const MaintenanceHubScreen(),
                routes: [
                  GoRoute(
                    path: 'requests/:id',
                    builder: (_, state) => MaintenanceRequestDetailScreen(
                      requestId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                  GoRoute(
                    path: 'work-orders/:id',
                    builder: (_, state) => WorkOrderDetailScreen(
                      workOrderId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
              GoRoute(
                path: 'reports',
                builder: (_, __) => const ReportsHubScreen(),
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
            builder: (_, __) => const FuelEntryScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => StaffFuelDetailScreen(
                  logId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/inspection',
            builder: (_, __) => const InspectionScreen(),
          ),
          GoRoute(
            path: '/documents',
            builder: (_, __) => const DocumentsEntryScreen(),
          ),
          GoRoute(
            path: '/earnings',
            builder: (_, __) => const EarningsScreen(),
          ),
          GoRoute(
            path: '/timeline',
            builder: (_, __) => const TimelineScreen(),
          ),
          GoRoute(
            path: '/settings',
            builder: (_, __) => const SettingsScreen(),
          ),
          GoRoute(
            path: '/offline-queue',
            builder: (_, __) => const OfflineQueueScreen(),
          ),
          GoRoute(
            path: '/security',
            builder: (_, __) => const SecurityStatusScreen(),
          ),
          GoRoute(
            path: '/legal/privacy',
            builder: (_, __) =>
                const LegalDocumentScreen(kind: LegalDocumentKind.privacy),
          ),
          GoRoute(
            path: '/legal/terms',
            builder: (_, __) =>
                const LegalDocumentScreen(kind: LegalDocumentKind.terms),
          ),
        ],
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: Center(child: Text('Page not found: ${state.uri}')),
    ),
  );
});
