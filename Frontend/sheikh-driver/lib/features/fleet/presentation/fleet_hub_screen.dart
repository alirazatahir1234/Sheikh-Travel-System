import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../auth/data/auth_repository.dart';
import 'fleet_hub_notifier.dart';
import 'fleet_vehicle_filters.dart';
import 'platform_company_filter.dart';
import 'widgets/fleet_kpi_strip.dart';
import 'widgets/fleet_vehicle_filter_sheet.dart';
import 'widgets/fleet_vehicle_tile.dart';

class FleetHubScreen extends ConsumerWidget {
  const FleetHubScreen({super.key});

  static FleetStatusFilterOption? _statusFromQuery(String? raw) {
    switch ((raw ?? '').toLowerCase().trim()) {
      case 'moving':
        return FleetStatusFilterOption.moving;
      case 'idle':
        return FleetStatusFilterOption.idle;
      case 'offline':
        return FleetStatusFilterOption.offline;
      case 'parked':
        return FleetStatusFilterOption.parked;
      case 'online':
        return FleetStatusFilterOption.online;
      default:
        return null;
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(fleetHubProvider);
    final statusQ = GoRouterState.of(context).uri.queryParameters['status'];
    final deepLinkStatus = _statusFromQuery(statusQ);
    if (deepLinkStatus != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final hub = ref.read(fleetHubProvider).valueOrNull;
        if (hub == null) return;
        if (hub.filters.status == deepLinkStatus) return;
        ref.read(fleetHubProvider.notifier).applyStatusFilter(deepLinkStatus);
      });
    }

    final session = ref.watch(fleetSessionProvider);
    final crossTenant = session?.canCrossTenantVehicles == true;

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(
        title: Text(
          session?.isGpsOperator == true ? 'Vehicles' : 'Fleet',
        ),
        actions: [
          if (crossTenant)
            IconButton(
              tooltip: 'Company filter',
              icon: const Icon(Icons.business_rounded),
              onPressed: () => _showCompanyFilterSheet(context, ref),
            ),
          if (session?.canSeeFleetAssignments == true)
            IconButton(
              tooltip: 'Assignments',
              icon: const Icon(Icons.event_available_outlined),
              onPressed: () => context.push('/fleet/assignments'),
            ),
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
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () => ref.read(fleetHubProvider.notifier).refresh(),
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
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 6),
                    child: _LiveTrackingHeader(
                      liveVehicleCount: hub.liveVehicleCount,
                      lastFleetRefreshAt: hub.lastFleetRefreshAt,
                      lastGpsAt: hub.lastGpsAt,
                      realtimeStatus: hub.realtimeStatus,
                    ),
                  ),
                ),
                if (crossTenant)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                      child: _PlatformCompanyFilterChip(
                        onTap: () => _showCompanyFilterSheet(context, ref),
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
                          child: SgSearchField(
                            hintText: 'Search plate, name, driver…',
                            onChanged: (v) => ref
                                .read(fleetHubProvider.notifier)
                                .setSearch(v),
                          ),
                        ),
                        const SizedBox(width: 8),
                        IntrinsicWidth(
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
                                  ? 'Filter ($filterCount)'
                                  : 'Filter',
                            ),
                            style: OutlinedButton.styleFrom(
                              foregroundColor: filterCount > 0
                                  ? AppColors.primary
                                  : context.sg.textPrimary,
                              side: BorderSide(
                                color: filterCount > 0
                                    ? AppColors.primary
                                    : context.sg.border,
                              ),
                              backgroundColor: context.sg.card,
                              padding: const EdgeInsets.symmetric(
                                horizontal: 12,
                                vertical: 12,
                              ),
                              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
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
                                  style: TextStyle(
                                    color: context.sg.textSecondary,
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
                              Icon(
                                switch (ErrorHandler.classifyMessage(
                                    hub.loadError)) {
                                  ApiErrorKind.unauthorized =>
                                    Icons.lock_clock_outlined,
                                  ApiErrorKind.forbidden =>
                                    Icons.lock_outline_rounded,
                                  _ => Icons.cloud_off_outlined,
                                },
                                color: AppColors.error,
                                size: 20,
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  hub.loadError!,
                                  style: TextStyle(
                                    color: context.sg.textSecondary,
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
                    hasFilters:
                        !hub.filters.isDefault || hub.search.trim().isNotEmpty,
                    loadFailed: hub.loadError != null,
                    loadError: hub.loadError,
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
    this.loadError,
  });

  final bool hasFilters;
  final bool loadFailed;
  final String? loadError;

  @override
  Widget build(BuildContext context) {
    final kind = ErrorHandler.classifyMessage(loadError);
    final (icon, title, subtitle) = !loadFailed
        ? (
            Icons.directions_car_outlined,
            hasFilters ? 'No Vehicles Found' : 'No vehicles in fleet',
            hasFilters
                ? 'Try clearing search or filters.'
                : '0 vehicles — vehicles for this tenant will appear here once added.',
          )
        : switch (kind) {
            ApiErrorKind.unauthorized => (
                Icons.lock_clock_outlined,
                'Session expired',
                loadError ?? 'Session expired — please sign in again.',
              ),
            ApiErrorKind.forbidden => (
                Icons.lock_outline_rounded,
                'Access restricted',
                loadError ??
                    'Access restricted — your account role or tenant plan does not include this permission. '
                        'Try signing out and back in if permissions were recently updated.',
              ),
            _ => (
                Icons.cloud_off_outlined,
                'Could not load vehicles',
                loadError ??
                    'Server unavailable — check your connection and pull to refresh.',
              ),
          };
    return SliverFillRemaining(
      hasScrollBody: false,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                size: 40,
                color: context.sg.textMuted,
              ),
              const SizedBox(height: 10),
              Text(
                title,
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: context.sg.textPrimary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                subtitle,
                textAlign: TextAlign.center,
                style: TextStyle(color: context.sg.textSecondary),
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
    final color = hasLive ? const Color(0xFF16A34A) : const Color(0xFF64748B);
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
                      ? context.sg.textSecondary
                      : const Color(0xFF64748B),
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          fleetLine,
          style: TextStyle(
            fontSize: 11,
            color: context.sg.textMuted,
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

class _PlatformCompanyFilterChip extends ConsumerWidget {
  const _PlatformCompanyFilterChip({required this.onTap});
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final selectedId = ref.watch(platformCompanyFilterProvider);
    final companies = ref.watch(adminCompaniesProvider).valueOrNull ?? const [];
    String label = 'All Companies';
    if (selectedId != null) {
      final match = companies.where((c) => c.id == selectedId).toList();
      label = match.isEmpty ? 'Company #$selectedId' : match.first.name;
    }
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: context.sg.border),
          ),
          child: Row(
            children: [
              const Icon(Icons.apartment_rounded,
                  size: 18, color: AppColors.primary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                    color: context.sg.textPrimary,
                  ),
                ),
              ),
              Text(
                'Filter',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: context.sg.textMuted,
                ),
              ),
              Icon(Icons.keyboard_arrow_down_rounded,
                  color: context.sg.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

Future<void> _showCompanyFilterSheet(BuildContext context, WidgetRef ref) {
  return showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    builder: (ctx) {
      return Consumer(
        builder: (context, ref, _) {
          final async = ref.watch(adminCompaniesProvider);
          final selected = ref.watch(platformCompanyFilterProvider);
          return SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Filter by company',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: context.sg.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Default: All Companies (cross-tenant)',
                    style: TextStyle(
                      fontSize: 12,
                      color: context.sg.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 12),
                  ListTile(
                    leading: Icon(
                      selected == null
                          ? Icons.check_circle_rounded
                          : Icons.circle_outlined,
                      color: AppColors.primary,
                    ),
                    title: const Text('All Companies'),
                    onTap: () async {
                      await ref
                          .read(platformCompanyFilterProvider.notifier)
                          .select(null);
                      if (ctx.mounted) Navigator.pop(ctx);
                    },
                  ),
                  Flexible(
                    child: async.when(
                      loading: () => const Padding(
                        padding: EdgeInsets.all(24),
                        child: Center(child: SgBrandLoader()),
                      ),
                      error: (e, _) => Padding(
                        padding: const EdgeInsets.all(16),
                        child: Text(ErrorHandler.message(e)),
                      ),
                      data: (companies) {
                        if (companies.isEmpty) {
                          return const Padding(
                            padding: EdgeInsets.all(16),
                            child: Text('No companies available.'),
                          );
                        }
                        return ListView.builder(
                          shrinkWrap: true,
                          itemCount: companies.length,
                          itemBuilder: (_, i) {
                            final c = companies[i];
                            final active = selected == c.id;
                            return ListTile(
                              leading: Icon(
                                active
                                    ? Icons.check_circle_rounded
                                    : Icons.circle_outlined,
                                color: AppColors.primary,
                              ),
                              title: Text(c.name),
                              subtitle: Text(
                                '${c.activeVehicleCount} vehicles'
                                '${c.isActive ? '' : ' · inactive'}',
                              ),
                              onTap: () async {
                                await ref
                                    .read(
                                        platformCompanyFilterProvider.notifier)
                                    .select(c.id);
                                if (ctx.mounted) Navigator.pop(ctx);
                              },
                            );
                          },
                        );
                      },
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      );
    },
  );
}
