import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_brand_loader.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../notifications/presentation/notifications_notifier.dart';
import 'alerts_notifier.dart';
import 'widgets/alerts_filter_sheet.dart';
import 'widgets/fleet_alert_row.dart';
import 'widgets/fleet_alerts_header.dart';
import 'widgets/incident_detail_sheet.dart';

export 'widgets/incident_detail_sheet.dart' show AlertDetailScreen;

class AlertsScreen extends ConsumerStatefulWidget {
  const AlertsScreen({super.key, this.vehicleId});

  /// Optional deep-link / vehicle-detail entry filter.
  final int? vehicleId;

  @override
  ConsumerState<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends ConsumerState<AlertsScreen> {
  final _search = TextEditingController();
  bool _refreshing = false;
  bool _selectMode = false;
  final Set<String> _expandedGroups = {};
  bool _appliedVehicleFilter = false;

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

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

  Future<void> _pickDate() async {
    final cur = ref.read(alertsProvider).valueOrNull?.datePreset ?? 'today';
    final next = await showModalBottomSheet<String>(
      context: context,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final e in const [
              ('today', 'Today'),
              ('last7', 'Last 7 days'),
              ('last30', 'Last 30 days'),
              ('all', 'All time'),
            ])
              ListTile(
                title: Text(e.$2),
                trailing: cur == e.$1
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () => Navigator.pop(ctx, e.$1),
              ),
          ],
        ),
      ),
    );
    if (next != null) {
      await ref.read(alertsProvider.notifier).setDatePreset(next);
    }
  }

  Future<void> _toggleSort() async {
    final cur = ref.read(alertsProvider).valueOrNull?.sort ?? FleetAlertSort.newest;
    await ref.read(alertsProvider.notifier).setSort(
          cur == FleetAlertSort.newest
              ? FleetAlertSort.oldest
              : FleetAlertSort.newest,
        );
  }

  Future<void> _showAssignSheet(List<int> ids) async {
    if (ids.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select alerts to assign')),
      );
      return;
    }
    final session = ref.read(fleetSessionProvider);
    final selfName = (session?.fullName.trim().isNotEmpty == true)
        ? session!.fullName.trim()
        : (session?.email?.trim() ?? '');
    final controller = TextEditingController(text: selfName);
    final result = await showModalBottomSheet<({String name, int? userId})>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        final kb = MediaQuery.viewInsetsOf(ctx).bottom;
        return Padding(
          padding: EdgeInsets.fromLTRB(20, 16, 20, 20 + kb),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Assign alerts',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 6),
              Text(
                '${ids.length} alert(s) — acknowledgement stays separate from assignment.',
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 14),
              TextField(
                controller: controller,
                decoration: const InputDecoration(
                  labelText: 'Assignee name',
                  hintText: 'e.g. Sara',
                  border: OutlineInputBorder(),
                ),
                textCapitalization: TextCapitalization.words,
              ),
              const SizedBox(height: 12),
              if (session != null)
                OutlinedButton(
                  onPressed: () => Navigator.pop(ctx, (
                    name: selfName.isNotEmpty ? selfName : 'Me',
                    userId: session.userId,
                  )),
                  child: const Text('Assign to me'),
                ),
              const SizedBox(height: 8),
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                ),
                onPressed: () {
                  final name = controller.text.trim();
                  if (name.isEmpty) return;
                  Navigator.pop(ctx, (name: name, userId: null));
                },
                child: const Text('Assign'),
              ),
            ],
          ),
        );
      },
    );
    controller.dispose();
    if (result == null) return;
    await ref.read(alertsProvider.notifier).assign(
          ids: ids,
          assignedToUserId: result.userId,
          assignedToName: result.name,
        );
    if (mounted) {
      setState(() => _selectMode = false);
      ref.read(alertsProvider.notifier).clearSelection();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Assigned to ${result.name}')),
      );
    }
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
    final canAck = session?.hasPermission(FleetPermissions.gpsAlertAcknowledge) ??
        session?.hasPermission('Gps.AlertAcknowledge') ??
        false;
    final canAssign = canAck;

    // Prefer server Can* flags when events loaded.
    final sample = async.valueOrNull?.events;
    final showAck = canAck ||
        (sample?.any((e) => e.canAcknowledge) ?? false);
    final showAssign = canAssign ||
        (sample?.any((e) => e.canAssign) ?? false);

    // No nested Scaffold — AppShell already provides one. A nested Scaffold
    // here left the body Expanded with empty paint while the header still
    // drew (chrome-only blank page).
    return ColoredBox(
      color: const Color(0xFFF3F7F8),
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
                loading: () => const ColoredBox(
                  color: Color(0xFFFFE4E6),
                  child: Center(
                    child: SgBrandLoader(message: 'Loading fleet alerts…'),
                  ),
                ),
                error: (e, _) => ColoredBox(
                  color: const Color(0xFFFFEDD5),
                  child: Center(
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
                ),
                data: (state) {
                  final summary = state.feedSummary;
                  final needs = state.needsAttention;
                  final grouped = state.groupedIncidents;
                  final selected = state.selectedIds;

                  return ColoredBox(
                    color: const Color(0xFFCCFBF1),
                    child: Column(
                    children: [
                      Material(
                        color: Colors.amber,
                        child: Padding(
                          padding: const EdgeInsets.all(8),
                          child: Text(
                            'DEBUG alerts loaded: ${state.events.length} '
                            'needs=${needs.length} grouped=${grouped.length}',
                            textAlign: TextAlign.center,
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                        ),
                      ),
                      Expanded(
                        child: ListView(
                          padding: const EdgeInsets.all(16),
                          children: [
                            Text(
                              'STEP1 plain list — events=${state.events.length} '
                              'visible=${state.visibleIncidents.length} '
                              'needs=${needs.length} grouped=${grouped.length}',
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w700,
                                color: Colors.black,
                              ),
                            ),
                            const SizedBox(height: 12),
                            for (final inc in state.visibleIncidents.take(20))
                              Card(
                                child: ListTile(
                                  title: Text(inc.groupTitle),
                                  subtitle: Text(
                                    '${inc.kind.name} · ${inc.allEvents.length} evt · '
                                    '${inc.primary.status} · ${inc.primary.severity}',
                                  ),
                                  dense: true,
                                ),
                              ),
                          ],
                        ),
                      ),
                      if (showAck || showAssign)
                        FleetAlertsBulkBar(
                          selectMode: _selectMode,
                          selectedCount: selected.length,
                          canAcknowledge: showAck,
                          canAssign: showAssign,
                          onToggleSelectMode: () {
                            setState(() {
                              _selectMode = !_selectMode;
                              if (!_selectMode) {
                                ref
                                    .read(alertsProvider.notifier)
                                    .clearSelection();
                              } else {
                                ref
                                    .read(alertsProvider.notifier)
                                    .selectAllVisible();
                              }
                            });
                          },
                          onAcknowledge: showAck
                              ? () async {
                                  final ids = selected.isEmpty
                                      ? state.needsAttention
                                          .expand((i) => i.acknowledgeIds)
                                          .toList()
                                      : selected.toList();
                                  if (ids.isEmpty) {
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                        content: Text(
                                            'Nothing to acknowledge'),
                                      ),
                                    );
                                    return;
                                  }
                                  await ref
                                      .read(alertsProvider.notifier)
                                      .acknowledgeIds(ids);
                                  if (mounted) {
                                    setState(() => _selectMode = false);
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                        content: Text('Alerts acknowledged'),
                                      ),
                                    );
                                  }
                                }
                              : null,
                          onAssign: showAssign
                              ? () => _showAssignSheet(
                                    selected.isEmpty
                                        ? state.visibleIncidents
                                            .expand((i) => i.allIds)
                                            .toList()
                                        : selected.toList(),
                                  )
                              : null,
                        ),
                    ],
                  ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
