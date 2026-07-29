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
import '../../gps_operator/presentation/operator_dashboard_widgets.dart';
import '../../gps_operator/domain/operator_dashboard_models.dart';
import '../../alerts/data/gps_alerts_api.dart';

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
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              session?.companyName?.isNotEmpty == true
                  ? session!.companyName!
                  : 'Fleet',
            ),
            if (session != null && !session.isDriverOnly)
              Text(
                DashboardRoleX.fromNavRole(session.primaryNavRole).subtitle,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w400,
                  color: AppColors.textSecondary,
                ),
              ),
          ],
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
                PopupMenuItem(value: 'Unavailable', child: Text('Unavailable')),
              ],
              icon: const Icon(Icons.toggle_on_outlined),
            ),
          IconButton(
            icon: const Icon(Icons.notifications_none_rounded),
            onPressed: () => context.go('/notifications'),
          ),
        ],
      ),
      body: dashAsync.when(
        loading: () => ListView(
          padding: const EdgeInsets.all(16),
          children: const [
            SgSkeleton(height: 108),
            SizedBox(height: 12),
            SgSkeleton(height: 120),
            SizedBox(height: 12),
            SgSkeleton(height: 200),
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
          child: LayoutBuilder(
            builder: (context, constraints) {
              final wide = constraints.maxWidth >= 700;
              final widgets = data.widgets
                  .where((id) => data.shouldShow(id, session))
                  .toList();

              return ListView(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                children: [
                  ..._buildOrdered(
                    context,
                    data,
                    widgets,
                    session?.canSeeAiTab ?? false,
                    wide,
                  ),
                  if (data.sectionErrors.isNotEmpty)
                    SectionErrorHint(
                      message:
                          'Some sections could not load: ${data.sectionErrors.keys.join(', ')}. Pull to refresh.',
                    ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  /// Renders widgets in order; on wide layouts pairs AI + Critical Alerts.
  List<Widget> _buildOrdered(
    BuildContext context,
    RoleDashboardData data,
    List<DashboardWidgetId> widgets,
    bool canOpenAi,
    bool wide,
  ) {
    final out = <Widget>[];
    var i = 0;
    while (i < widgets.length) {
      final id = widgets[i];
      if (wide &&
          id == DashboardWidgetId.aiAttention &&
          i + 1 < widgets.length &&
          widgets[i + 1] == DashboardWidgetId.criticalAlertsList) {
        out.add(
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _buildWidget(context, data, id, canOpenAi),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildWidget(
                  context,
                  data,
                  DashboardWidgetId.criticalAlertsList,
                  canOpenAi,
                ),
              ),
            ],
          ),
        );
        out.add(const SizedBox(height: 14));
        i += 2;
        continue;
      }

      out.add(_buildWidget(context, data, id, canOpenAi));
      out.add(const SizedBox(height: 14));
      i++;
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
      case DashboardWidgetId.greeting:
      case DashboardWidgetId.opsHeader:
        return OpsHeaderCard(
          name: data.displayName,
          role: data.role,
          tenantId: data.tenantId,
          lastSyncedAt: data.lastSyncedAt,
        );
      case DashboardWidgetId.platformBanner:
        return const PlatformBannerCard();
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
      case DashboardWidgetId.fleetHealthHeader:
        return FleetHealthCard(data: data);
      case DashboardWidgetId.fleetStatsStrip:
        return FleetStatsStrip(data: data);
      case DashboardWidgetId.opsKpiGrid:
        return OpsKpiGridCard(data: data);
      case DashboardWidgetId.primaryKpis:
        return data.primaryKpis.isEmpty
            ? const SizedBox.shrink()
            : KpiStrip(
                items: [
                  for (final c in data.primaryKpis)
                    (
                      c.label,
                      c.value,
                      switch (c.colorKey) {
                        'success' => AppColors.success,
                        'warning' => AppColors.warning,
                        'error' => AppColors.error,
                        'info' => AppColors.info,
                        _ => AppColors.primary,
                      },
                    ),
                ],
              );
      case DashboardWidgetId.fleetKpis:
        return KpiStrip(items: [
          ('Vehicles', '${data.fleet?.totalVehicles ?? 0}', AppColors.primary),
          ('Active', '${data.fleet?.activeVehicles ?? 0}', AppColors.success),
          ('Drivers', '${data.fleet?.driversOnDuty ?? 0}', AppColors.info),
          (
            'Maint.',
            '${data.fleet?.maintenanceDue ?? data.maintenance?.dueForService ?? 0}',
            AppColors.warning
          ),
        ]);
      case DashboardWidgetId.fleetStatusStrip:
        return KpiStrip(items: [
          ('Moving', '${data.gps?.moving ?? 0}', AppColors.success),
          ('Idle', '${data.gps?.idle ?? 0}', AppColors.info),
          ('Offline', '${data.gps?.offline ?? 0}', AppColors.error),
          ('Parked', '${data.gps?.parked ?? 0}', AppColors.warning),
        ]);
      case DashboardWidgetId.liveFleetCard:
      case DashboardWidgetId.liveMapPreview:
        return LiveMapPreviewCard(positions: data.livePositions);
      case DashboardWidgetId.mapSummaryCard:
        return MapSummaryCard(data: data);
      case DashboardWidgetId.universalSearchBar:
        return const UniversalSearchBarCard();
      case DashboardWidgetId.attentionVehicles:
        return AttentionVehiclesCard(data: data);
      case DashboardWidgetId.aiAttention:
        return AiCopilotSummaryCard(
          items: data.aiItems,
          canOpenAi: canOpenAi,
        );
      case DashboardWidgetId.criticalAlertsList:
      case DashboardWidgetId.recentAlerts:
        return CriticalAlertsCard(
          events: data.alertEvents,
          criticalCount: data.alerts?.critical ?? 0,
        );
      case DashboardWidgetId.todayOpsKpis:
        return TodayOpsKpiRow(data: data);
      case DashboardWidgetId.recentActivities:
        return RecentActivitiesCard(items: data.activities);
      case DashboardWidgetId.maintenanceKpis:
        return MaintenanceKpisCard(data: data);
      case DashboardWidgetId.fuelSummary:
      case DashboardWidgetId.fuelCost:
        return FuelAnalyticsCard(data: data);
      case DashboardWidgetId.tripKpis:
        final t = data.trips;
        return KpiStrip(items: [
          ('Today', '${t?.total ?? 0}', AppColors.primary),
          ('In progress', '${t?.inProgress ?? 0}', AppColors.info),
          ('Upcoming', '${t?.scheduled ?? 0}', AppColors.warning),
          ('Done', '${t?.completed ?? 0}', AppColors.success),
        ]);
      case DashboardWidgetId.liveTripsPreview:
        return LiveTripsPreviewCard(trips: data.liveTrips);
      case DashboardWidgetId.pendingAssignments:
        return PendingAssignmentsCard(trips: data.pendingTrips);
      case DashboardWidgetId.driverKpis:
        final s = data.driverStats;
        return KpiStrip(items: [
          ('Drivers', '${s?.totalDrivers ?? 0}', AppColors.primary),
          ('Active', '${s?.active ?? 0}', AppColors.success),
          ('On trip', '${s?.onTrip ?? 0}', AppColors.info),
          ('Off duty', '${s?.offDuty ?? 0}', AppColors.warning),
        ]);
      case DashboardWidgetId.driverPerformance:
        final s = data.driverStats;
        return KpiStrip(items: [
          ('Available', '${s?.available ?? 0}', AppColors.success),
          ('GPS online', '${s?.gpsOnline ?? 0}', AppColors.info),
          ('Lic. soon', '${s?.licensesExpiringSoon ?? 0}', AppColors.warning),
          ('Lic. expired', '${s?.licensesExpired ?? 0}', AppColors.error),
        ]);
      case DashboardWidgetId.complianceDocs:
        return ComplianceDocsCard(data: data);
      case DashboardWidgetId.financeKpis:
        return KpiStrip(items: [
          (
            'Fuel',
            _shortMoney(
              data.fuelAnalytics?.totalCost ?? data.fleet?.monthlyFuelCost ?? 0,
            ),
            AppColors.warning
          ),
          (
            'Maint.',
            _shortMoney(data.maintenance?.monthlyMaintenanceCost ?? 0),
            AppColors.error
          ),
          (
            'Today fuel',
            _shortMoney(data.fuelAnalytics?.todayCost ?? 0),
            AppColors.info
          ),
          ('Alerts', '${data.unreadNotifications}', AppColors.primary),
        ]);
      case DashboardWidgetId.maintenanceCost:
        return MoneySummaryCard(
          title: 'Monthly maintenance cost',
          amount: data.maintenance?.monthlyMaintenanceCost ?? 0,
          subtitle:
              '${data.maintenance?.activeWorkOrders ?? 0} active work orders',
          icon: Icons.build_rounded,
          route: '/more/maintenance',
        );
      case DashboardWidgetId.quickActions:
        return QuickActionsGrid(actions: data.quickActions);
      case DashboardWidgetId.gpsExceptionKpiGrid:
        return GpsExceptionKpiGrid(
          summary: data.operatorSummary ?? GpsOperatorSummary.empty,
        );
      case DashboardWidgetId.trackerHealthCard:
        return TrackerHealthCard(
          summary: data.operatorSummary ?? GpsOperatorSummary.empty,
        );
      case DashboardWidgetId.recentGpsAlertsFeed:
        return Consumer(
          builder: (context, ref, _) => RecentGpsAlertsFeed(
            alerts: data.alertEvents,
            onAcknowledge: (id) async {
              await ref.read(gpsAlertsApiProvider).acknowledge(id);
              await ref.read(dashboardProvider.notifier).silentRefresh();
            },
          ),
        );
    }
  }

  String _shortMoney(double v) {
    if (v >= 1000000) return '${(v / 1000000).toStringAsFixed(1)}M';
    if (v >= 1000) return '${(v / 1000).toStringAsFixed(0)}k';
    return v.toStringAsFixed(0);
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
