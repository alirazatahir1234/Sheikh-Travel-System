import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/auth/domain/auth_models.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/splash_screen.dart';
import '../../features/auth/presentation/session_expired_screen.dart';
import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/reset_password_screen.dart';
import '../../features/dashboard/presentation/dashboard_screen.dart';
import '../../features/dashboard/presentation/activities_screen.dart';
import '../../features/gps_operator/presentation/gps_trips_screen.dart';
import '../../features/gps_operator/presentation/gps_fuel_dashboard_screen.dart';
import '../../features/gps_operator/presentation/gps_mileage_dashboard_screen.dart';
import '../../features/gps_operator/presentation/device_health_screen.dart';
import '../../features/gps_operator/presentation/gps_commands_hub_screen.dart';
import '../../features/gps_operator/presentation/incident_center_screen.dart';
import '../../features/gps_operator/presentation/geofence_operator_screen.dart';
import '../../features/gps_operator/presentation/parking_screen.dart';
import '../../features/gps_operator/presentation/operator_ai_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/performance/presentation/driver_performance_screen.dart';
import '../../features/notifications/presentation/notifications_screen.dart';
import '../../features/gps/presentation/live_map_screen.dart';
import '../../features/attendance/presentation/attendance_screen.dart';
import '../../features/timeline/presentation/timeline_screen.dart';
import '../../features/settings/presentation/settings_screen.dart';
import '../../features/settings/presentation/help_support_screen.dart';
import '../../features/settings/presentation/location_tracking_screen.dart';
import '../../features/settings/presentation/security_permissions_screen.dart';
import '../../features/settings/domain/settings_access.dart';
import '../../features/earnings/presentation/earnings_screen.dart';
import '../../features/settlements/presentation/settlements_screen.dart';
import '../../features/inspection/presentation/inspection_screen.dart';
import '../../features/navigation/presentation/trip_navigation_screen.dart';
import '../../features/offline/presentation/offline_queue_screen.dart';
import '../../features/voice/presentation/voice_command_screen.dart';
import '../../features/fatigue/presentation/fatigue_monitor_screen.dart';
import '../../features/fatigue/presentation/fatigue_response_screen.dart';
import '../../features/sos/presentation/sos_screen.dart';
import '../../features/security/presentation/security_status_screen.dart';
import '../../features/legal/presentation/legal_document_screen.dart';
import '../../features/payments/presentation/collect_payment_screen.dart';
import '../../features/fleet/presentation/fleet_hub_screen.dart';
import '../../features/fleet/presentation/fleet_assignments_screen.dart';
import '../../features/fleet/presentation/fleet_live_map_screen.dart';
import '../../features/fleet/presentation/vehicle_detail_screen.dart';
import '../../features/fleet/presentation/vehicle_history_screen.dart';
import '../../features/ai/presentation/ai_assistant_chat_screen.dart';
import '../../features/ai/presentation/ai_assistant_home_screen.dart';
import '../../features/more/presentation/more_screen.dart';
import '../../features/drivers/presentation/drivers_list_screen.dart';
import '../../features/drivers/presentation/driver_detail_screen.dart';
import '../../features/drivers/presentation/driver_form_screen.dart';
import '../../features/ops_trips/presentation/trips_entry_screen.dart';
import '../../features/ops_trips/presentation/trip_detail_entry_screen.dart';
import '../../features/bookings/presentation/bookings_queue_screen.dart';
import '../../features/bookings/presentation/booking_detail_screen.dart';
import '../../features/alerts/presentation/alerts_screen.dart';
import '../../features/maintenance/presentation/maintenance_hub_screen.dart';
import '../../features/maintenance/presentation/maintenance_detail_screens.dart';
import '../../features/staff_fuel/presentation/fuel_entry_screen.dart';
import '../../features/staff_fuel/presentation/staff_fuel_list_screen.dart';
import '../../features/compliance/presentation/documents_entry_screen.dart';
import '../../features/ev/presentation/ev_charging_screen.dart';
import '../../features/reports/presentation/reports_hub_screen.dart';
import '../../core/navigation/fleet_nav_config.dart';
import '../../shared/widgets/app_shell.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final authNotifier = ref.watch(authRepositoryProvider);

  return GoRouter(
    initialLocation: '/splash',
    redirect: (context, state) {
      final session = authNotifier.session;
      final isLoggedIn = authNotifier.isLoggedIn;
      // Prefer the full URI path over matchedLocation so nested pushes like
      // `/more/drivers/:id` are permission-checked on the real destination
      // (matchedLocation can lag/parent-match during ShellRoute transitions).
      final loc = state.uri.path.isNotEmpty
          ? state.uri.path
          : state.matchedLocation;
      final onLogin = loc == '/login';
      final onSplash = loc == '/splash';
      final onAuthFlow = onLogin ||
          onSplash ||
          loc == '/forgot-password' ||
          loc == '/reset-password' ||
          loc == '/session-expired';

      if (authNotifier.isLoading && onSplash) return null;

      if (onSplash) {
        if (isLoggedIn) return '/dashboard';
        return null;
      }

      if (!isLoggedIn && !onAuthFlow) {
        return authNotifier.sessionExpired ? '/session-expired' : '/login';
      }
      if (isLoggedIn && (onLogin || loc == '/session-expired' || onSplash)) {
        return '/dashboard';
      }

      if (session != null &&
          (loc.startsWith('/trips/') &&
              (loc.endsWith('/navigate') ||
                  loc.endsWith('/collect-payment')))) {
        if (!session.isDriverSession && !session.canSeeTripsTab) {
          return '/dashboard';
        }
      }

      if (session != null && FleetNavConfig.isShellRoute(loc)) {
        if (loc.startsWith('/fleet') && !session.canSeeFleetTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/fleet/assignments') &&
            !session.canSeeFleetAssignments) {
          return '/dashboard';
        }
        if (RegExp(r'^/fleet/vehicles/\d+/device').hasMatch(loc) &&
            !session.canSeeTrackerHardware) {
          return '/fleet';
        }
        if (loc.startsWith('/trips') &&
            (session.isGpsOperator || !session.canSeeTripsTab)) {
          return '/dashboard';
        }
        if (loc.startsWith('/ai') && !session.canSeeAiTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/bookings') && !session.canSeeBookingsTab) {
          return '/dashboard';
        }
        if (loc.startsWith('/finance') &&
            !session.canSeeFinanceTab &&
            !session.canSeeOwnerEarnings) {
          return '/dashboard';
        }
        if (loc.startsWith('/earnings') &&
            !session.isDriverSession &&
            !session.canSeeOwnerEarnings) {
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
                session.isGpsOperator ||
                !session.hasPermission(FleetPermissions.driverView))) {
          return '/more';
        }
        if ((loc.startsWith('/fleet/maintenance') ||
                loc.startsWith('/more/maintenance')) &&
            (session.isDriverOnly ||
                session.isGpsOperator ||
                !session.hasPermission(FleetPermissions.maintenanceView))) {
          return '/more';
        }
        // Legacy More path → Fleet maintenance hub.
        if (loc == '/more/maintenance' ||
            loc.startsWith('/more/maintenance/')) {
          return loc.replaceFirst('/more/maintenance', '/fleet/maintenance');
        }
        if (loc.startsWith('/more/reports') &&
            (session.isDriverOnly ||
                session.isGpsOperator ||
                !session.hasPermission(FleetPermissions.reportView))) {
          return '/more';
        }
        if (loc == '/gps/commands' && !session.canUseGpsCommands) {
          return '/dashboard';
        }
        if (loc.startsWith('/gps/') &&
            loc != '/gps/commands' &&
            !session.canSeeGpsOperatorScreens) {
          return '/dashboard';
        }
        if (loc.startsWith('/fuel') &&
            (session.isGpsOperator ||
                (!session.isDriverSession &&
                    !session.hasPermission(FleetPermissions.fuelView)))) {
          return '/dashboard';
        }
        if (loc.startsWith('/documents') &&
            (session.isGpsOperator ||
                (!session.isDriverSession &&
                    !session.hasPermission(FleetPermissions.vehicleView)))) {
          return '/dashboard';
        }
        if (loc == '/attendance' && !SettingsAccess.canSeeAttendance(session)) {
          return '/dashboard';
        }
        if ((loc == '/inspection' ||
                loc == '/sos' ||
                loc == '/voice' ||
                loc == '/performance' ||
                loc.startsWith('/fatigue/')) &&
            !session.isDriverSession) {
          return '/dashboard';
        }
        // Trip history / playback requires GPS.View (UI also gates the screen).
        if (RegExp(r'^/fleet/vehicles/\d+/history').hasMatch(loc) &&
            (session.isDriverOnly ||
                !session.hasPermission(FleetPermissions.gpsView))) {
          return '/fleet';
        }
        if (loc == '/live' && !session.isDriverSession) {
          return '/dashboard';
        }
        if (loc == '/security' &&
            !SettingsAccess.canSeeSecurityDiagnostics(session)) {
          return '/settings/security-permissions';
        }
        if (loc == '/settings/location' &&
            !SettingsAccess.canSeeLocationTracking(session)) {
          return '/settings';
        }
      }

      return null;
    },
    refreshListenable: authNotifier,
    routes: [
      GoRoute(
        path: '/splash',
        builder: (_, __) => const SplashScreen(),
      ),
      GoRoute(
        path: '/',
        redirect: (_, __) => '/splash',
      ),
      GoRoute(
        path: '/login',
        builder: (_, __) => const LoginScreen(),
      ),
      GoRoute(
        path: '/forgot-password',
        builder: (_, __) => const ForgotPasswordScreen(),
      ),
      GoRoute(
        path: '/reset-password',
        builder: (_, state) => ResetPasswordScreen(
          initialToken: state.uri.queryParameters['token'],
        ),
      ),
      GoRoute(
        path: '/session-expired',
        builder: (_, __) => const SessionExpiredScreen(),
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
                path: 'assignments',
                builder: (_, __) => const FleetAssignmentsScreen(),
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
              // History is a sibling of vehicle detail (not nested under it).
              // Nesting kept VehicleDetailScreen (Live/tracker tabs) in the stack
              // when opening playback from Live Map, which felt like a redirect.
              GoRoute(
                path: 'vehicles/:id/history',
                builder: (_, state) {
                  final q = state.uri.queryParameters;
                  return VehicleHistoryScreen(
                    vehicleId: int.parse(state.pathParameters['id']!),
                    tripKey: q['tripKey'],
                    initialPreset: q['preset'],
                    initialFrom: DateTime.tryParse(q['from'] ?? ''),
                    initialTo: DateTime.tryParse(q['to'] ?? ''),
                    autoPlayFull: (q['play'] ?? '').toLowerCase() == 'full',
                  );
                },
              ),
              GoRoute(
                path: 'vehicles/:id',
                builder: (_, state) => VehicleDetailScreen(
                  vehicleId: int.parse(state.pathParameters['id']!),
                ),
                routes: [
                  GoRoute(
                    path: 'device',
                    builder: (_, state) => DeviceHealthScreen(
                      vehicleId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/gps/trips',
            builder: (_, __) => const GpsTripsScreen(),
          ),
          GoRoute(
            path: '/gps/fuel',
            builder: (_, __) => const GpsFuelDashboardScreen(),
          ),
          GoRoute(
            path: '/gps/mileage',
            builder: (_, __) => const GpsMileageDashboardScreen(),
          ),
          GoRoute(
            path: '/gps/commands',
            builder: (_, __) => const GpsCommandsHubScreen(),
          ),
          GoRoute(
            path: '/gps/incidents',
            builder: (_, __) => const IncidentCenterScreen(),
          ),
          GoRoute(
            path: '/gps/geofences',
            builder: (_, __) => const GeofenceOperatorScreen(),
          ),
          GoRoute(
            path: '/gps/parking',
            builder: (_, __) => const ParkingScreen(),
          ),
          GoRoute(
            path: '/gps/operator-ai',
            builder: (_, __) => const OperatorAiScreen(),
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
            builder: (_, state) {
              final q = state.uri.queryParameters['q']?.trim();
              if (q != null && q.isNotEmpty) {
                return AiAssistantChatScreen(initialPrompt: q);
              }
              return const AiAssistantHomeScreen();
            },
            routes: [
              GoRoute(
                path: 'chat',
                builder: (_, state) => AiAssistantChatScreen(
                  initialPrompt: state.uri.queryParameters['q'],
                ),
              ),
            ],
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
            builder: (_, __) => const SettlementsScreen(),
          ),
          GoRoute(
            path: '/users',
            builder: (_, __) => const ComingSoonScreen(
              title: 'Users',
            ),
          ),
          GoRoute(
            path: '/alerts',
            builder: (_, state) {
              final raw = state.uri.queryParameters['vehicleId'];
              final vehicleId = raw == null ? null : int.tryParse(raw);
              return AlertsScreen(vehicleId: vehicleId);
            },
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => AlertDetailScreen(
                  alertId: int.parse(state.pathParameters['id']!),
                ),
              ),
            ],
          ),
          // Drivers are shell siblings of `/more` (not nested under MoreScreen).
          // Nesting under `/more` made detail pushes resolve to the More hub on
          // iOS ShellRoute transitions — same class of bug as vehicle history.
          GoRoute(
            path: '/more/drivers',
            builder: (_, __) => const DriversListScreen(),
            routes: [
              GoRoute(
                path: 'new',
                builder: (_, __) => const DriverFormScreen(),
              ),
              GoRoute(
                path: ':id',
                builder: (_, state) => DriverDetailScreen(
                  driverId: int.parse(state.pathParameters['id']!),
                ),
                routes: [
                  GoRoute(
                    path: 'edit',
                    builder: (_, state) => DriverFormScreen(
                      driverId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/more',
            builder: (_, __) => const MoreScreen(),
            routes: [
              // Legacy: redirect handled in top-level redirect above.
              GoRoute(
                path: 'maintenance',
                redirect: (_, state) {
                  final rest = state.uri.path.replaceFirst(
                    '/more/maintenance',
                    '',
                  );
                  return '/fleet/maintenance$rest';
                },
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
            path: '/sos',
            builder: (_, __) => const SosScreen(),
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
            path: '/performance',
            builder: (_, __) => const DriverPerformanceScreen(),
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
            path: '/ev-charging',
            builder: (_, __) => const EvChargingScreen(),
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
            path: '/activities',
            builder: (_, __) => const ActivitiesScreen(),
          ),
          GoRoute(
            path: '/settings',
            builder: (_, __) => const SettingsScreen(),
          ),
          GoRoute(
            path: '/settings/location',
            builder: (_, __) => const LocationTrackingScreen(),
          ),
          GoRoute(
            path: '/settings/security-permissions',
            builder: (_, __) => const SecurityPermissionsScreen(),
          ),
          GoRoute(
            path: '/offline-queue',
            builder: (_, __) => const OfflineQueueScreen(),
          ),
          GoRoute(
            path: '/voice',
            builder: (_, __) => const VoiceCommandScreen(),
          ),
          GoRoute(
            path: '/fatigue/monitor',
            builder: (_, __) => const FatigueMonitorScreen(),
          ),
          GoRoute(
            path: '/fatigue/response',
            builder: (_, __) => const FatigueResponseScreen(),
          ),
          GoRoute(
            path: '/security',
            builder: (_, __) => const SecurityStatusScreen(),
          ),
          GoRoute(
            path: '/help-support',
            builder: (_, __) => const HelpSupportScreen(),
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
