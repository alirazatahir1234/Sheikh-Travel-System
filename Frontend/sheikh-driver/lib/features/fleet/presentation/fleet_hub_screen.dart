import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../auth/data/auth_repository.dart';
import 'fleet_hub_notifier.dart';
import 'widgets/fleet_kpi_strip.dart';
import 'widgets/fleet_vehicle_filter_sheet.dart';
import 'widgets/fleet_vehicle_tile.dart';

class FleetHubScreen extends ConsumerWidget {
  const FleetHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(fleetHubProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Text(
          ref.watch(fleetSessionProvider)?.isGpsOperator == true
              ? 'Vehicles'
              : 'Fleet',
        ),
        actions: [
          IconButton(
            tooltip: 'Live map',
            icon: const Icon(Icons.map_outlined),
            onPressed: () => context.push('/fleet/map'),
          ),
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(fleetHubProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.cloud_off_outlined,
                    size: 48, color: AppColors.textMuted),
                const SizedBox(height: 12),
                Text(
                  e.toString(),
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () =>
                      ref.read(fleetHubProvider.notifier).refresh(),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (hub) {
          final visible = hub.visible;
          final filterCount = hub.filters.activeCount;
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => ref.read(fleetHubProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                'Vehicles (${hub.kpis.totalVehicles})',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                  fontSize: 18,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                            ),
                            TextButton.icon(
                              onPressed: () => context.push('/fleet/map'),
                              icon: const Icon(Icons.map_rounded, size: 18),
                              label: const Text('View live map'),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        _LiveTrackingHeader(
                          liveVehicleCount: hub.liveVehicleCount,
                          lastFleetRefreshAt: hub.lastFleetRefreshAt,
                          lastGpsAt: hub.lastGpsAt,
                          realtimeStatus: hub.realtimeStatus,
                        ),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: FleetKpiStrip(
                    kpis: hub.kpis,
                    selected: hub.statusFilter,
                    onSelect: (s) =>
                        ref.read(fleetHubProvider.notifier).setStatusFilter(s),
                  ),
                ),
                const SliverToBoxAdapter(child: SizedBox(height: 8)),
                SliverToBoxAdapter(child: FleetOpsSummaryRow(ops: hub.ops)),
                const SliverToBoxAdapter(child: SizedBox(height: 4)),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                    child: Row(
                      children: [
                        Expanded(
                          child: TextField(
                            onChanged: (v) => ref
                                .read(fleetHubProvider.notifier)
                                .setSearch(v),
                            decoration: InputDecoration(
                              hintText: 'Search plate, name, driver…',
                              isDense: true,
                              contentPadding:
                                  const EdgeInsets.symmetric(vertical: 12),
                              prefixIcon:
                                  const Icon(Icons.search_rounded, size: 20),
                              prefixIconConstraints: const BoxConstraints(
                                minWidth: 40,
                                minHeight: 40,
                              ),
                              filled: true,
                              fillColor: Colors.white,
                              border: OutlineInputBorder(
                                borderRadius:
                                    BorderRadius.circular(AppRadii.md),
                                borderSide: BorderSide.none,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        // Badge must not wrap a button under unbounded width —
                        // that crashes layout (infinite width) and blanks the list.
                        Badge(
                          isLabelVisible: filterCount > 0,
                          label: Text('$filterCount'),
                          child: IntrinsicWidth(
                            child: OutlinedButton.icon(
                              onPressed: () async {
                                final next = await showFleetVehicleFilterSheet(
                                  context,
                                  initial: hub.filters,
                                );
                                if (next == null) return;
                                ref
                                    .read(fleetHubProvider.notifier)
                                    .setFilters(next);
                              },
                              icon: const Icon(Icons.tune_rounded, size: 18),
                              label: Text(
                                filterCount > 0
                                    ? 'Filter $filterCount'
                                    : 'Filter',
                              ),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: filterCount > 0
                                    ? AppColors.primary
                                    : AppColors.textPrimary,
                                side: BorderSide(
                                  color: filterCount > 0
                                      ? AppColors.primary
                                      : AppColors.border,
                                ),
                                backgroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                  vertical: 12,
                                ),
                                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                if (hub.liveFeedWarning != null)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                      child: Material(
                        color: const Color(0xFFF59E0B).withValues(alpha: 0.10),
                        borderRadius: BorderRadius.circular(AppRadii.md),
                        child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Row(
                            children: [
                              const Icon(Icons.warning_amber_rounded,
                                  color: Color(0xFFB45309), size: 20),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  hub.liveFeedWarning!,
                                  style: const TextStyle(
                                    color: AppColors.textSecondary,
                                    fontSize: 13,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                if (hub.loadError != null)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                      child: Material(
                        color: AppColors.error.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(AppRadii.md),
                        child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Row(
                            children: [
                              const Icon(Icons.cloud_off_outlined,
                                  color: AppColors.error, size: 20),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  hub.loadError!,
                                  style: const TextStyle(
                                    color: AppColors.textSecondary,
                                    fontSize: 13,
                                  ),
                                ),
                              ),
                              TextButton(
                                onPressed: () => ref
                                    .read(fleetHubProvider.notifier)
                                    .refresh(),
                                child: const Text('Retry'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                if (visible.isEmpty)
                  _EmptyVehiclesSliver(
                    hasFilters: !hub.filters.isDefault || hub.search.trim().isNotEmpty,
                    loadFailed: hub.loadError != null,
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) =>
                          FleetVehicleTile(vehicle: visible[i]),
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

class _EmptyVehiclesSliver extends StatelessWidget {
  const _EmptyVehiclesSliver({
    this.hasFilters = false,
    this.loadFailed = false,
  });

  final bool hasFilters;
  final bool loadFailed;

  @override
  Widget build(BuildContext context) {
    final title = loadFailed
        ? 'Could not load vehicles'
        : hasFilters
            ? 'No Vehicles Found'
            : 'No vehicles in fleet';
    final subtitle = loadFailed
        ? 'Check your connection and pull to refresh.'
        : hasFilters
            ? 'Try clearing search or filters.'
            : 'Vehicles for this tenant will appear here once added.';
    return SliverFillRemaining(
      hasScrollBody: false,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                loadFailed
                    ? Icons.cloud_off_outlined
                    : Icons.directions_car_outlined,
                size: 40,
                color: AppColors.textMuted,
              ),
              const SizedBox(height: 10),
              Text(
                title,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                subtitle,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LiveTrackingHeader extends StatelessWidget {
  const _LiveTrackingHeader({
    required this.liveVehicleCount,
    required this.lastFleetRefreshAt,
    required this.lastGpsAt,
    required this.realtimeStatus,
  });

  final int liveVehicleCount;
  final DateTime? lastFleetRefreshAt;
  final DateTime? lastGpsAt;
  final String realtimeStatus;

  @override
  Widget build(BuildContext context) {
    final hasLive = liveVehicleCount > 0;
    final color = hasLive
        ? const Color(0xFF16A34A)
        : const Color(0xFF64748B);
    final title = hasLive ? 'Live tracking' : 'No live vehicles';
    final subtitle = hasLive
        ? '$liveVehicleCount vehicle${liveVehicleCount == 1 ? '' : 's'} connected'
            '${lastGpsAt != null ? ' · GPS ${_relative(lastGpsAt!)}' : ''}'
        : 'No vehicles currently reporting';

    final fleetLine = lastFleetRefreshAt == null
        ? 'Fleet data not loaded yet'
        : 'Fleet data updated ${_relative(lastFleetRefreshAt!)}';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            _LiveBadge(
              label: title,
              color: color,
              pulse: hasLive && realtimeStatus == 'connected',
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                subtitle,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: hasLive
                      ? AppColors.textSecondary
                      : const Color(0xFF64748B),
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          fleetLine,
          style: const TextStyle(
            fontSize: 11,
            color: AppColors.textMuted,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }

  static String _relative(DateTime at) {
    var sec = DateTime.now().difference(at).inSeconds;
    if (sec < 0) sec = 0;
    if (sec < 5) return 'just now';
    if (sec < 60) return '${sec}s ago';
    if (sec < 3600) return '${sec ~/ 60}m ago';
    return '${sec ~/ 3600}h ago';
  }
}

class _LiveBadge extends StatefulWidget {
  const _LiveBadge({
    required this.label,
    required this.color,
    this.pulse = true,
  });

  final String label;
  final Color color;
  final bool pulse;

  @override
  State<_LiveBadge> createState() => _LiveBadgeState();
}

class _LiveBadgeState extends State<_LiveBadge>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _scale;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1200),
    );
    _scale = Tween<double>(begin: 0.85, end: 1.25).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeInOut),
    );
    if (widget.pulse) _controller.repeat(reverse: true);
  }

  @override
  void didUpdateWidget(covariant _LiveBadge oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.pulse && !_controller.isAnimating) {
      _controller.repeat(reverse: true);
    } else if (!widget.pulse && _controller.isAnimating) {
      _controller.stop();
      _controller.value = 1;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: widget.color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(AppRadii.pill),
        border: Border.all(color: widget.color.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          ScaleTransition(
            scale: _scale,
            child: Icon(
              Icons.circle,
              size: 8,
              color: widget.color,
            ),
          ),
          const SizedBox(width: 6),
          Text(
            widget.label,
            style: TextStyle(
              color: widget.color,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
