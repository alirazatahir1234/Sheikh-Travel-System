import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../trips/presentation/trips_notifier.dart';
import '../domain/dashboard_layout.dart';
import '../domain/dashboard_models.dart';
import '../domain/dashboard_role.dart';
import 'dashboard_notifier.dart';
import 'widgets/command_dashboard_widgets.dart';
import 'widgets/dashboard_widgets.dart';
import '../../performance/presentation/driver_performance_card.dart';

/// Enterprise fleet command order — matches Ali’s mockup (no Quick Actions).
const _commandOrder = [
  DashboardWidgetId.opsHeader,
  DashboardWidgetId.universalSearchBar,
  DashboardWidgetId.primaryKpis,
  DashboardWidgetId.fleetStatusStrip,
  DashboardWidgetId.liveMapPreview,
  DashboardWidgetId.criticalAlertsList,
  DashboardWidgetId.recentActivities,
];

// Enforced order for driver role.
const _driverOrder = [
  DashboardWidgetId.opsHeader,
  DashboardWidgetId.myVehicle,
  DashboardWidgetId.driverTripKpis,
  DashboardWidgetId.driverPerformance,
  DashboardWidgetId.earnings,
  DashboardWidgetId.recentActivities,
  DashboardWidgetId.aiAttention,
];

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authRepositoryProvider).session;
    final dashAsync = ref.watch(dashboardProvider);
    final forceDriver = session != null &&
        (session.isDriverOnly || session.authMode == AuthMode.driver);

    return Scaffold(
      backgroundColor: context.sg.surface,
      // Header is composed inside OpsHeaderCard (logo / tenant / bell).
      body: SafeArea(
        bottom: false,
        child: dashAsync.when(
          loading: () => ListView(
            padding: AppSpacing.screen,
            children: const [
              SgSkeleton(height: 120, radius: AppRadii.md),
              SizedBox(height: 14),
              SgSkeleton(height: 48, radius: AppRadii.lg),
              SizedBox(height: 12),
              SgSkeleton(height: 160, radius: AppRadii.lg),
              SizedBox(height: 12),
              SgSkeleton(height: 88, radius: AppRadii.lg),
              SizedBox(height: 12),
              SgSkeleton(height: 140, radius: AppRadii.lg),
            ],
          ),
          error: (e, _) => SgErrorView(
            message: formatDioError(e),
            onRetry: () => ref.read(dashboardProvider.notifier).refresh(),
          ),
          data: (data) {
            if (forceDriver && !data.isDriver) {
              WidgetsBinding.instance.addPostFrameCallback((_) {
                ref.read(dashboardProvider.notifier).refresh();
              });
              return ListView(
                padding: AppSpacing.screen,
                children: const [
                  SgSkeleton(height: 120, radius: AppRadii.md),
                  SizedBox(height: 14),
                  SgSkeleton(height: 110, radius: AppRadii.lg),
                  SizedBox(height: 12),
                  SgSkeleton(height: 80, radius: AppRadii.lg),
                ],
              );
            }
            return RefreshIndicator(
              color: AppColors.primary,
              onRefresh: () async {
                await ref.read(dashboardProvider.notifier).refresh();
                await ref.read(offlineSyncProvider).syncNow();
                ref.invalidate(tripsProvider);
              },
              child: ListView(
                padding: EdgeInsets.fromLTRB(
                  AppSpacing.lg,
                  AppSpacing.sm,
                  AppSpacing.lg,
                  100 + MediaQuery.of(context).padding.bottom,
                ),
                children: [
                  ..._buildOrdered(
                    context,
                    ref,
                    data,
                    session,
                    forceDriver: forceDriver,
                  ),
                  if (data.sectionErrors.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.xs),
                      child: SectionErrorHint(
                        message: _sectionErrorMessage(data),
                      ),
                    )
                  else if (_fleetDataMissing(data, forceDriver: forceDriver))
                    const Padding(
                      padding: EdgeInsets.only(top: AppSpacing.sm),
                      child: SectionErrorHint(
                        message:
                            'Server unavailable — check your connection and pull to refresh.',
                      ),
                    ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

  List<Widget> _buildOrdered(
    BuildContext context,
    WidgetRef ref,
    RoleDashboardData data,
    FleetSession? session, {
    required bool forceDriver,
  }) {
    final isDriverLayout = forceDriver || data.isDriver;
    final order = isDriverLayout ? _driverOrder : _commandOrder;
    final canOpenAi = session?.canSeeAiTab ?? false;

    final active = order.where((id) {
      if (id == DashboardWidgetId.myVehicle ||
          id == DashboardWidgetId.driverTripKpis ||
          id == DashboardWidgetId.earnings) {
        return data.driver != null;
      }
      if (id == DashboardWidgetId.aiAttention) {
        return canOpenAi;
      }
      if (id == DashboardWidgetId.liveMapPreview) {
        return !isDriverLayout;
      }
      return true;
    }).toList();

    final out = <Widget>[];
    for (final id in active) {
      final w = _buildWidget(
        context,
        ref,
        data,
        id,
        session,
        canOpenAi: canOpenAi,
        forceDriver: forceDriver,
      );
      out.add(w);
      out.add(const SizedBox(height: AppSpacing.md));
    }

    // Mockup has no Create Trip / Assign Vehicle strip on Overview.
    // Keep ContextualOpsActions available for other shells; do not append here.
    return out;
  }

  Widget _buildWidget(
    BuildContext context,
    WidgetRef ref,
    RoleDashboardData data,
    DashboardWidgetId id,
    FleetSession? session, {
    required bool canOpenAi,
    required bool forceDriver,
  }) {
    final displayRole =
        forceDriver || data.isDriver ? DashboardRole.driver : data.role;
    switch (id) {
      case DashboardWidgetId.opsHeader:
      case DashboardWidgetId.greeting:
        return OpsHeaderCard(
          name: data.displayName,
          role: displayRole,
          tenantId: data.tenantId,
          lastSyncedAt: data.lastSyncedAt,
          companyName:
              session?.companyName ?? session?.companyContext?.companyName,
          branchName: session?.companyContext?.branchName,
        );
      case DashboardWidgetId.universalSearchBar:
        return const UniversalSearchBarCard();
      case DashboardWidgetId.primaryKpis:
        return PrimaryKpiStrip(cells: data.primaryKpis);
      case DashboardWidgetId.fleetStatusStrip:
        final gps = data.gps;
        final moving = gps?.moving ?? 0;
        // Activity strip is 4 buckets (no Parked/SOS tiles) — fold into Idle.
        final idle =
            (gps?.idle ?? 0) + (gps?.parked ?? 0) + (gps?.sos ?? 0);
        final offline = gps?.offline ?? 0;
        final neverSeen = gps?.neverSeen ?? 0;
        return FleetActivityDistributionCard(
          moving: moving,
          idle: idle,
          offline: offline,
          neverSeen: neverSeen,
        );
      case DashboardWidgetId.liveMapPreview:
      case DashboardWidgetId.liveFleetCard:
        return LiveMapPreviewCard(
          positions: data.livePositions,
          movingCount: data.gps?.moving ?? 0,
        );
      case DashboardWidgetId.criticalAlertsList:
      case DashboardWidgetId.recentAlerts:
        return CriticalAlertsCard(
          events: data.alertEvents,
          criticalCount: data.alerts?.critical ?? 0,
          activeCount: data.alerts?.active ?? data.alertEvents.length,
        );
      case DashboardWidgetId.recentActivities:
        if (data.activities.isNotEmpty) {
          return RecentActivitiesCard(
            items: data.activities.take(3).toList(),
            totalCount: data.activities.length,
          );
        }
        if (forceDriver || data.isDriver) {
          return SgCard(
            onTap: () => context.push('/timeline'),
            child: Row(
              children: [
                const SgIconWell(icon: Icons.timeline_outlined),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Activity',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: context.sg.textPrimary,
                        ),
                      ),
                      Text(
                        'View your trip and attendance timeline',
                        style: TextStyle(
                          fontSize: 12,
                          color: context.sg.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(Icons.chevron_right_rounded, color: context.sg.textMuted),
              ],
            ),
          );
        }
        return const RecentActivitiesCard(items: []);
      case DashboardWidgetId.aiAttention:
        return AiCopilotSummaryCard(
          items: data.aiItems,
          canOpenAi: canOpenAi,
        );

      // Driver-specific widgets
      case DashboardWidgetId.myVehicle:
        return MyVehicleCard(driver: data.driver!);
      case DashboardWidgetId.driverTripKpis:
        final d = data.driver!;
        final remaining =
            (d.assignedTripsToday - d.completedToday).clamp(0, 999);
        return KpiStrip(
          items: [
            ('Trips Today', '${d.assignedTripsToday}', AppColors.primary),
            ('Completed', '${d.completedToday}', AppColors.success),
            ('Remaining', '$remaining', AppColors.warning),
            ('Inbox', '${d.unreadNotifications}', AppColors.error),
          ],
          itemRoutes: const [null, null, null, '/notifications'],
        );
      case DashboardWidgetId.driverPerformance:
        return const DriverPerformanceCard();
      case DashboardWidgetId.earnings:
        return EarningsCard(driver: data.driver!);

      default:
        return const SizedBox.shrink();
    }
  }

  static String _sectionErrorMessage(RoleDashboardData data) {
    for (final msg in data.sectionErrors.values) {
      if (ErrorHandler.classifyMessage(msg) == ApiErrorKind.unauthorized) {
        return msg;
      }
    }
    for (final msg in data.sectionErrors.values) {
      if (ErrorHandler.classifyMessage(msg) == ApiErrorKind.forbidden) {
        return msg;
      }
    }
    for (final msg in data.sectionErrors.values) {
      if (ErrorHandler.classifyMessage(msg) == ApiErrorKind.network) {
        return 'Server unavailable — check your connection. ($msg)';
      }
    }
    final keys = data.sectionErrors.keys.toList()..sort();
    final critical = keys.where((k) => k == 'fleet' || k == 'gps').toList();
    if (critical.isNotEmpty) {
      return 'Could not load ${critical.join(' & ')}: '
          '${keys.join(', ')}. Pull to refresh.';
    }
    return 'Some sections could not load: ${keys.join(', ')}. Pull to refresh.';
  }

  static bool _fleetDataMissing(
    RoleDashboardData data, {
    required bool forceDriver,
  }) {
    if (forceDriver || data.isDriver) return false;
    final total = data.gps?.totalVehicles ?? data.fleet?.totalVehicles ?? 0;
    if (total > 0) return false;
    return data.sectionErrors.containsKey('fleet') ||
        data.sectionErrors.containsKey('gps');
  }
}

/// Compact Create Trip / Assign Vehicle — only when permissioned.
class ContextualOpsActions extends StatelessWidget {
  const ContextualOpsActions({super.key, required this.session});
  final FleetSession? session;

  bool get canCreateTrip =>
      session != null &&
      !session!.isDriverOnly &&
      session!.hasPermission(FleetPermissions.tripView) &&
      (session!.hasAnyRole(const [
            FleetRole.dispatcher,
            FleetRole.fleetManager,
            FleetRole.tenantAdmin,
            FleetRole.superAdmin,
            FleetRole.owner,
          ]) ||
          session!.hasPermission(FleetPermissions.bookingView));

  bool get canAssignVehicle =>
      session != null &&
      !session!.isDriverOnly &&
      (session!.hasPermission(FleetPermissions.driverAssign) ||
          session!.hasPermission(FleetPermissions.vehicleUpdate) ||
          session!.canSeeFleetTab);

  bool get hasAny => canCreateTrip || canAssignVehicle;

  @override
  Widget build(BuildContext context) {
    if (!hasAny) return const SizedBox.shrink();
    return Row(
      children: [
        if (canCreateTrip)
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => context.push('/trips'),
              icon: const Icon(Icons.add_road_rounded, size: 18),
              label: const Text('Create Trip'),
            ),
          ),
        if (canCreateTrip && canAssignVehicle) const SizedBox(width: 10),
        if (canAssignVehicle)
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => context.push('/fleet/assignments'),
              icon: const Icon(Icons.event_available_outlined, size: 18),
              label: const Text('Assign Vehicle'),
            ),
          ),
      ],
    );
  }
}
