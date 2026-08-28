import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../trips/presentation/trips_notifier.dart';
import '../domain/dashboard_layout.dart';
import '../domain/dashboard_models.dart';
import '../domain/dashboard_role.dart';
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
  DashboardWidgetId.quickActions,
  DashboardWidgetId.aiAttention,
];

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authRepositoryProvider).session;
    final dashAsync = ref.watch(dashboardProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.menu_rounded),
          onPressed: () => context.push('/settings'),
        ),
        title: Text(
          session?.companyName?.isNotEmpty == true
              ? session!.companyName!
              : 'Sheikh Travel',
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        centerTitle: true,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(18),
          child: Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: Text(
              session != null && !session.isDriverOnly
                  ? '${DashboardRoleX.fromNavRole(session.primaryNavRole).subtitle} ˅'
                  : 'Driver view',
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
              ),
            ),
          ),
        ),
        actions: [
          if (session?.isDriverOnly ?? true)
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
          Stack(
            alignment: Alignment.center,
            children: [
              IconButton(
                icon: const Icon(Icons.notifications_none_rounded),
                onPressed: () => context.go('/notifications'),
              ),
              const Positioned(
                right: 10,
                top: 10,
                child: CircleAvatar(
                  radius: 4,
                  backgroundColor: AppColors.success,
                ),
              ),
            ],
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
          message: e.toString(),
          onRetry: () => ref.read(dashboardProvider.notifier).refresh(),
        ),
        data: (data) => RefreshIndicator(
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
              ),
              if (data.sectionErrors.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: SectionErrorHint(
                    message: _sectionErrorMessage(data),
                  ),
                ),
              if (_fleetDataMissing(data))
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: SectionErrorHint(
                    message:
                        'Fleet data could not be loaded. Check that the API is reachable '
                        '(use DEV_LAN_HOST for a physical phone, not localhost). Pull to refresh.',
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _buildOrdered(
    BuildContext context,
    RoleDashboardData data,
    bool canOpenAi,
  ) {
    final isDriver = data.isDriver;
    final order = isDriver ? _driverOrder : _commandOrder;

    // Only include items that have data (skip if driver info missing for driver items).
    final active = order.where((id) {
      if (id == DashboardWidgetId.myVehicle ||
          id == DashboardWidgetId.driverTripKpis ||
          id == DashboardWidgetId.earnings) {
        return data.driver != null;
      }
      if (id == DashboardWidgetId.aiAttention && !canOpenAi) {
        return data.aiItems.isNotEmpty;
      }
      return true;
    }).toList();

    final out = <Widget>[];
    for (final id in active) {
      final w = _buildWidget(context, data, id, canOpenAi);
      out.add(w);
      out.add(const SizedBox(height: 14));
    }
    return out;
  }

  Widget _buildWidget(
    BuildContext context,
    RoleDashboardData data,
    DashboardWidgetId id,
    bool canOpenAi,
  ) {
    switch (id) {
      case DashboardWidgetId.opsHeader:
      case DashboardWidgetId.greeting:
        return OpsHeaderCard(
          name: data.displayName,
          role: data.role,
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
        return QuickActionsGrid(actions: data.quickActions);
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
        return KpiStrip(items: [
          ('Trips Today', '${d.assignedTripsToday}', AppColors.primary),
          ('Completed', '${d.completedToday}', AppColors.success),
          ('Remaining', '$remaining', AppColors.warning),
          ('Alerts', '${d.unreadNotifications}', AppColors.error),
        ]);
      case DashboardWidgetId.earnings:
        return EarningsCard(driver: data.driver!);

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

  static bool _fleetDataMissing(RoleDashboardData data) {
    if (data.isDriver) return false;
    final total = data.gps?.totalVehicles ?? data.fleet?.totalVehicles ?? 0;
    if (total > 0) return false;
    return data.sectionErrors.containsKey('fleet') ||
        data.sectionErrors.containsKey('gps');
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
            const Icon(Icons.cloud_off_outlined,
                size: 48, color: AppColors.textSecondary),
            const SizedBox(height: 12),
            Text(message,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.textSecondary)),
            const SizedBox(height: 16),
            SgPrimaryButton(label: 'Retry', onPressed: onRetry),
          ],
        ),
      ),
    );
  }
}
