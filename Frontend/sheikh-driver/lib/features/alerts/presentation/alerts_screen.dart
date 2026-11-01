import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../auth/data/auth_repository.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../notifications/presentation/notifications_notifier.dart';
import 'alerts_notifier.dart';
import 'widgets/alert_empty_state.dart';
import 'widgets/alert_error_state.dart';
import 'widgets/alert_loading_state.dart';
import 'widgets/alert_severity_filters.dart';
import 'widgets/alert_status_tabs.dart';
import 'widgets/fleet_alert_card.dart';
import 'widgets/fleet_alerts_header.dart';

import '../domain/alert_display.dart';

export 'widgets/incident_detail_sheet.dart' show AlertDetailScreen;

class AlertsScreen extends ConsumerStatefulWidget {
  const AlertsScreen({super.key, this.vehicleId});

  /// Optional deep-link / vehicle-detail entry filter.
  final int? vehicleId;

  @override
  ConsumerState<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends ConsumerState<AlertsScreen> {
  bool _refreshing = false;
  bool _appliedVehicleFilter = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_appliedVehicleFilter) return;
    final id = widget.vehicleId;
    if (id == null) return;
    _appliedVehicleFilter = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(alertsProvider.notifier).setVehicleFilter(id);
    });
  }

  Future<void> _refresh() async {
    setState(() => _refreshing = true);
    try {
      await ref.read(alertsProvider.notifier).refresh();
    } finally {
      if (mounted) setState(() => _refreshing = false);
    }
  }

  void _openAlert(int id) {
    context.push('/alerts/$id');
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(alertsProvider);
    final session = ref.watch(fleetSessionProvider);
    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final unread = ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;
    final company = session?.companyName?.trim().isNotEmpty == true
        ? session!.companyName!.trim()
        : 'Sheikh Travel';
    final vehicleCount =
        hub?.kpis.totalVehicles ?? hub?.locations.length ?? 0;

    // No nested Scaffold — AppShell already provides bottom navigation.
    return ColoredBox(
      color: const Color(0xFFF3F7F9),
      child: SafeArea(
        bottom: false,
        child: Column(
          children: [
            FleetAlertsHeader(
              companyName: company,
              vehicleCount: vehicleCount,
              unreadNotifications: unread,
              onBell: () => context.push('/notifications'),
            ),
            Expanded(
              child: async.when(
                skipLoadingOnReload: true,
                skipLoadingOnRefresh: true,
                loading: () => const Column(
                  children: [
                    _ChromePlaceholder(),
                    Expanded(child: AlertLoadingState()),
                  ],
                ),
                error: (_, __) => AlertErrorState(
                  onRetry: _refresh,
                  retrying: _refreshing,
                ),
                data: (state) => _AlertsBody(
                  state: state,
                  refreshing: _refreshing,
                  onRefresh: _refresh,
                  onStatusTab: (tab) =>
                      ref.read(alertsProvider.notifier).setStatusTab(tab),
                  onToggleSeverity: (sev) => ref
                      .read(alertsProvider.notifier)
                      .toggleSeverityFilter(sev),
                  onClearFilters: () =>
                      ref.read(alertsProvider.notifier).clearSeverityFilters(),
                  onOpen: _openAlert,
                  onLoadMore: () =>
                      ref.read(alertsProvider.notifier).loadMore(),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ChromePlaceholder extends StatelessWidget {
  const _ChromePlaceholder();

  @override
  Widget build(BuildContext context) {
    return const Column(
      children: [
        SizedBox(height: 8),
        Padding(
          padding: EdgeInsets.symmetric(horizontal: 16),
          child: SizedBox(
            height: 46,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: Color(0xFFE8EEF2),
                borderRadius: BorderRadius.all(Radius.circular(14)),
              ),
            ),
          ),
        ),
        SizedBox(height: 12),
        SizedBox(height: 40),
      ],
    );
  }
}

class _AlertsBody extends StatelessWidget {
  const _AlertsBody({
    required this.state,
    required this.refreshing,
    required this.onRefresh,
    required this.onStatusTab,
    required this.onToggleSeverity,
    required this.onClearFilters,
    required this.onOpen,
    required this.onLoadMore,
  });

  final AlertsState state;
  final bool refreshing;
  final Future<void> Function() onRefresh;
  final ValueChanged<FleetAlertStatusTab> onStatusTab;
  final ValueChanged<AlertDisplaySeverity> onToggleSeverity;
  final VoidCallback onClearFilters;
  final ValueChanged<int> onOpen;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final summary = state.tabSeveritySummary;
    final visible = state.visibleIncidents;
    final isResolvedTab = state.statusTab == FleetAlertStatusTab.resolved;
    final filtered = state.hasSeverityFilters;
    final activeCount = state.statusTab == FleetAlertStatusTab.active
        ? state.statusScopedIncidents.length
        : null;
    final resolvedCount = state.statusTab == FleetAlertStatusTab.resolved
        ? state.statusScopedIncidents.length
        : null;

    return Column(
      children: [
        AlertStatusTabs(
          selected: state.statusTab,
          activeCount: activeCount,
          resolvedCount: resolvedCount,
          onChanged: onStatusTab,
        ),
        AlertSeverityFilters(
          selected: state.severityFilters,
          criticalCount: summary.critical,
          warningCount: summary.warning,
          infoCount: summary.info,
          onToggle: onToggleSeverity,
        ),
        const SizedBox(height: 8),
        Expanded(
          child: RefreshIndicator(
            color: const Color(0xFF0D9488),
            onRefresh: onRefresh,
            child: refreshing && visible.isEmpty
                ? const AlertLoadingState()
                : visible.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        children: [
                          SizedBox(
                            height: MediaQuery.sizeOf(context).height * 0.55,
                            child: AlertEmptyState(
                              kind: filtered
                                  ? AlertEmptyKind.filtered
                                  : (isResolvedTab
                                      ? AlertEmptyKind.resolvedEmpty
                                      : AlertEmptyKind.allClear),
                              selectedFilters: state.severityFilters,
                              onClearFilters:
                                  filtered ? onClearFilters : null,
                            ),
                          ),
                        ],
                      )
                    : NotificationListener<ScrollNotification>(
                        onNotification: (n) {
                          if (n.metrics.pixels >=
                              n.metrics.maxScrollExtent - 200) {
                            onLoadMore();
                          }
                          return false;
                        },
                        child: ListView.builder(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.fromLTRB(16, 4, 16, 28),
                          itemCount: visible.length,
                          itemBuilder: (context, i) {
                            final inc = visible[i];
                            return FleetAlertCard(
                              incident: inc,
                              showAsResolved: isResolvedTab,
                              onTap: () => onOpen(inc.primary.id),
                            );
                          },
                        ),
                      ),
          ),
        ),
      ],
    );
  }
}
