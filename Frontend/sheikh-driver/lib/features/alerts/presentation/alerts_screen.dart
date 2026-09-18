import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/alert_display.dart';
import 'alerts_notifier.dart';
import 'widgets/alerts_app_bar.dart';
import 'widgets/alerts_feed_header.dart';
import 'widgets/alerts_filter_sheet.dart';
import 'widgets/in_cab_radio_modal.dart';
import 'widgets/incident_detail_sheet.dart';
import 'widgets/telematics_alert_card.dart';

export 'widgets/incident_detail_sheet.dart' show AlertDetailScreen;

class AlertsScreen extends ConsumerStatefulWidget {
  const AlertsScreen({super.key});

  @override
  ConsumerState<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends ConsumerState<AlertsScreen> {
  final _search = TextEditingController();
  bool _refreshing = false;

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() => _refreshing = true);
    try {
      await ref.read(alertsProvider.notifier).refresh();
    } finally {
      if (mounted) setState(() => _refreshing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(alertsProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AlertsAppBar(
        activeFilterCount: async.valueOrNull?.activeFilterCount ?? 0,
        refreshing: _refreshing || async.isLoading,
        onFilter: () => showAlertsFilterSheet(context, ref),
        onRefresh: _refresh,
      ),
      body: async.when(
        skipLoadingOnReload: true,
        skipLoadingOnRefresh: true,
        loading: () => const Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(color: AppColors.primary),
              SizedBox(height: 12),
              Text(
                'Loading alerts…',
                style: TextStyle(color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline,
                    size: 40, color: AppColors.error),
                const SizedBox(height: 12),
                Text('$e', textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: _refresh,
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (state) {
          final visible = state.visible;
          final summary = state.feedSummary;
          final counts = state.categoryCounts;

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: _refresh,
            child: CustomScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              slivers: [
                SliverToBoxAdapter(
                  child: AlertsSearchBar(
                    controller: _search,
                    onChanged: (q) =>
                        ref.read(alertsProvider.notifier).setSearchQuery(q),
                  ),
                ),
                SliverToBoxAdapter(
                  child: CriticalAttentionBanner(
                    count: summary.critical,
                    onReview: () => ref
                        .read(alertsProvider.notifier)
                        .setCategoryFilter(AlertCategory.critical),
                  ),
                ),
                if (state.loadError != null)
                  const SliverToBoxAdapter(
                    child: Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                      child: Text(
                        'Stats unavailable — showing alert list.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.warning,
                        ),
                      ),
                    ),
                  ),
                SliverToBoxAdapter(
                  child: AlertsSummaryChips(summary: summary),
                ),
                const SliverToBoxAdapter(child: SizedBox(height: 8)),
                SliverToBoxAdapter(
                  child: AlertsCategoryPills(
                    selected: state.categoryFilter,
                    counts: counts,
                    onSelected: (c) => ref
                        .read(alertsProvider.notifier)
                        .setCategoryFilter(c),
                  ),
                ),
                SliverToBoxAdapter(
                  child: AlertsQuickActions(
                    onSimulate: () =>
                        ref.read(alertsProvider.notifier).simulateIncident(),
                    onAcknowledgeAll: () async {
                      await ref
                          .read(alertsProvider.notifier)
                          .acknowledgeAllVisible();
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            content: Text('Acknowledged visible alerts'),
                          ),
                        );
                      }
                    },
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                    child: Row(
                      children: [
                        const Text(
                          'ACTIVE TELEMATICS FEED',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 0.8,
                            color: AppColors.textMuted,
                          ),
                        ),
                        const Spacer(),
                        Text(
                          'Showing ${visible.length} incidents',
                          style: const TextStyle(
                            fontSize: 11,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                if (visible.isEmpty)
                  const SliverToBoxAdapter(
                    child: SizedBox(
                      height: 240,
                      child: Center(
                        child: Text(
                          'No alerts match these filters',
                          style: TextStyle(color: AppColors.textSecondary),
                        ),
                      ),
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final a = visible[i];
                        final demo = state.isDemo(a.id);
                        return TelematicsAlertCard(
                          alert: a,
                          isDemo: demo,
                          onOpen: () =>
                              showIncidentDetailSheet(context, ref, a.id),
                          onAck: () => ref
                              .read(alertsProvider.notifier)
                              .acknowledge(a.id),
                          onCall: a.driverName == null
                              ? null
                              : () => showInCabRadioModal(
                                    context,
                                    driverName: a.driverName!,
                                    vehicleLabel: a.vehicleName,
                                  ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
