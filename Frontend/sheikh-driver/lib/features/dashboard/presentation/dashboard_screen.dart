import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../notifications/presentation/notifications_notifier.dart';
import '../../trips/presentation/trips_notifier.dart';
import '../domain/dashboard_layout.dart';
import '../domain/dashboard_models.dart';
import '../domain/dashboard_role.dart';
import 'dashboard_layout_registry.dart';
import 'dashboard_notifier.dart';
import 'widgets/command_dashboard_widgets.dart';
import 'widgets/dashboard_widgets.dart';

// Enforced order for command/fleet roles.
const _commandOrder = [
  DashboardWidgetId.opsHeader,
  DashboardWidgetId.universalSearchBar,
  DashboardWidgetId.primaryKpis,
  DashboardWidgetId.fleetStatusStrip,
  DashboardWidgetId.criticalAlertsList,
  DashboardWidgetId.attentionVehicles,
  DashboardWidgetId.quickActions,
  DashboardWidgetId.aiAttention,
];

// Enforced order for driver role.
const _driverOrder = [
  DashboardWidgetId.opsHeader,
  DashboardWidgetId.myVehicle,
  DashboardWidgetId.driverTripKpis,
  DashboardWidgetId.earnings,
  DashboardWidgetId.recentActivities,
  DashboardWidgetId.quickActions,
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
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.settings_outlined),
          tooltip: 'Settings',
          onPressed: () => context.push('/settings'),
        ),
        title: Text(
          session?.companyName?.isNotEmpty == true
              ? session!.companyName!
              : 'SheikhGo Tracking',
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        centerTitle: false,
        actions: [
          if (forceDriver)
            PopupMenuButton<String>(
              tooltip: 'Set availability',
              onSelected: (v) =>
                  ref.read(dashboardProvider.notifier).setStatus(v),
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'Online', child: Text('Online')),
                PopupMenuItem(value: 'Busy', child: Text('Busy (On Trip)')),
                PopupMenuItem(value: 'Break', child: Text('Break')),
                PopupMenuItem(
                    value: 'Unavailable', child: Text('Unavailable')),
              ],
              icon: const Icon(Icons.toggle_on_outlined),
            ),
          _InboxBellButton(
            onPressed: () => context.go('/notifications'),
          ),
        ],
      ),
      body: dashAsync.when(
        loading: () => ListView(
          padding: const EdgeInsets.all(16),
          children: const [
            SgSkeleton(height: 72),
            SizedBox(height: 12),
            SgSkeleton(height: 44),
            SizedBox(height: 12),
            SgSkeleton(height: 110),
            SizedBox(height: 12),
            SgSkeleton(height: 80),
            SizedBox(height: 12),
            SgSkeleton(height: 140),
          ],
        ),
        error: (e, _) => _ErrorView(
          message: formatDioError(e),
          onRetry: () => ref.read(dashboardProvider.notifier).refresh(),
        ),
        data: (data) {
          // Stale fleet payload while session is driver-only — refetch once.
          if (forceDriver && !data.isDriver) {
            WidgetsBinding.instance.addPostFrameCallback((_) {
              ref.read(dashboardProvider.notifier).refresh();
            });
            return ListView(
              padding: const EdgeInsets.all(16),
              children: const [
                SgSkeleton(height: 72),
                SizedBox(height: 12),
                SgSkeleton(height: 110),
                SizedBox(height: 12),
                SgSkeleton(height: 80),
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
                16,
                8,
                16,
                100 + MediaQuery.of(context).padding.bottom,
              ),
              children: [
                ..._buildOrdered(
                  context,
                  data,
                  session?.canSeeAiTab ?? false,
                  forceDriver: forceDriver,
                ),
                if (data.sectionErrors.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: SectionErrorHint(
                      message: _sectionErrorMessage(data),
                    ),
                  ),
                if (_fleetDataMissing(data, forceDriver: forceDriver))
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: SectionErrorHint(
                      message:
                          'Fleet data could not be loaded. Check that the API is reachable '
                          'Pull to refresh, or check your connection.',
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  List<Widget> _buildOrdered(
    BuildContext context,
    RoleDashboardData data,
    bool canOpenAi, {
    required bool forceDriver,
  }) {
    final isDriverLayout = forceDriver || data.isDriver;
    final order = isDriverLayout ? _driverOrder : _commandOrder;

    // Only include items that have data (skip if driver info missing for driver items).
    final active = order.where((id) {
      if (id == DashboardWidgetId.myVehicle ||
          id == DashboardWidgetId.driverTripKpis ||
          id == DashboardWidgetId.earnings) {
        return data.driver != null;
      }
      // Drivers without AI access must not see a dead Copilot teaser.
      if (id == DashboardWidgetId.aiAttention) {
        return canOpenAi;
      }
      return true;
    }).toList();

    final out = <Widget>[];
    for (final id in active) {
      final w = _buildWidget(
        context,
        data,
        id,
        canOpenAi,
        forceDriver: forceDriver,
      );
      out.add(w);
      out.add(const SizedBox(height: 14));
    }
    return out;
  }

  Widget _buildWidget(
    BuildContext context,
    RoleDashboardData data,
    DashboardWidgetId id,
    bool canOpenAi, {
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
        );
      case DashboardWidgetId.universalSearchBar:
        return const UniversalSearchBarCard();
      case DashboardWidgetId.primaryKpis:
        return PrimaryKpiStrip(cells: data.primaryKpis);
      case DashboardWidgetId.fleetStatusStrip:
        return KpiStrip(
          title: 'Fleet Status Overview',
          viewAllLabel: 'View full report',
          viewAllRoute: '/fleet/map',
          items: [
            // Mutually exclusive buckets (online KPI elsewhere = moving + idle).
            ('Moving', '${data.gps?.moving ?? 0}', AppColors.success),
            ('Idle', '${data.gps?.idle ?? 0}', AppColors.warning),
            ('Offline', '${data.gps?.offline ?? 0}', AppColors.error),
            (
              'Never Seen',
              '${data.gps?.neverSeen ?? 0}',
              AppColors.textMuted
            ),
          ],
        );
      case DashboardWidgetId.criticalAlertsList:
      case DashboardWidgetId.recentAlerts:
        return CriticalAlertsCard(
          events: data.alertEvents,
          criticalCount: data.alerts?.critical ?? 0,
        );
      case DashboardWidgetId.attentionVehicles:
        return AttentionVehiclesCard(data: data);
      case DashboardWidgetId.quickActions:
        final actions = forceDriver && !data.isDriver
            ? DashboardLayoutRegistry.quickActionsFor(DashboardRole.driver)
            : data.quickActions;
        final isDriverLayout = forceDriver || data.isDriver;
        return QuickActionsGrid(
          actions: actions,
          // Drivers should not land on staff /more hub from this CTA.
          viewAllRoute: isDriverLayout ? '/profile' : '/more',
        );
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
      case DashboardWidgetId.earnings:
        return EarningsCard(driver: data.driver!);
      case DashboardWidgetId.recentActivities:
        if (data.activities.isNotEmpty) {
          return RecentActivitiesCard(items: data.activities.take(5).toList());
        }
        return SgCard(
          onTap: () => context.push('/timeline'),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.timeline_outlined,
                    color: AppColors.primary),
              ),
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
              Icon(Icons.chevron_right, color: context.sg.textMuted),
            ],
          ),
        );

      // Everything else is suppressed — return nothing.
      default:
        return const SizedBox.shrink();
    }
  }

  static String _sectionErrorMessage(RoleDashboardData data) {
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

class _InboxBellButton extends ConsumerWidget {
  const _InboxBellButton({required this.onPressed});
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread =
        ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;
    return IconButton(
      tooltip: 'Inbox',
      onPressed: onPressed,
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text(unread > 99 ? '99+' : '$unread'),
        child: const Icon(Icons.notifications_none_rounded),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off_outlined,
                size: 48, color: context.sg.textSecondary),
            const SizedBox(height: 12),
            Text(message,
                textAlign: TextAlign.center,
                style: TextStyle(color: context.sg.textSecondary)),
            const SizedBox(height: 16),
            SgPrimaryButton(label: 'Retry', onPressed: onRetry),
          ],
        ),
      ),
    );
  }
}
