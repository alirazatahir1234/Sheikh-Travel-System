import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/driver_models.dart';
import 'drivers_notifier.dart';

class DriversListScreen extends ConsumerStatefulWidget {
  const DriversListScreen({super.key});

  @override
  ConsumerState<DriversListScreen> createState() => _DriversListScreenState();
}

class _DriversListScreenState extends ConsumerState<DriversListScreen> {
  late final TextEditingController _searchCtrl;

  @override
  void initState() {
    super.initState();
    _searchCtrl = TextEditingController();
    _searchCtrl.addListener(_onSearchTextChanged);
  }

  void _onSearchTextChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _searchCtrl.removeListener(_onSearchTextChanged);
    _searchCtrl.dispose();
    super.dispose();
  }

  Future<void> _clearFilters() async {
    _searchCtrl.clear();
    await ref.read(driversHubProvider.notifier).clearFilters();
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(driversHubProvider);
    final session = ref.watch(fleetSessionProvider);
    final canCreate = !(session?.isDriverOnly ?? true) &&
        (session?.hasPermission(FleetPermissions.driverCreate) ?? false);
    final p = context.sg;
    final hub = async.valueOrNull;
    final softLoading = async.isLoading && hub != null;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: const Text('Drivers'),
        actions: [
          if (softLoading)
            const Padding(
              padding: EdgeInsets.only(right: 4),
              child: Center(
                child: SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            ),
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: softLoading
                ? null
                : () => ref.read(driversHubProvider.notifier).refresh(),
          ),
        ],
      ),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () => context.push('/more/drivers/new'),
              icon: const Icon(Icons.person_add_alt_1_rounded),
              label: const Text('Add driver'),
            )
          : null,
      body: async.when(
        skipLoadingOnReload: true,
        skipLoadingOnRefresh: true,
        loading: () => const _DriversListSkeleton(),
        error: (e, _) => SgErrorView(
          message: '$e',
          onRetry: () => ref.read(driversHubProvider.notifier).refresh(),
        ),
        data: (hub) => _DriversListBody(
          hub: hub,
          searchCtrl: _searchCtrl,
          canCreate: canCreate,
          softLoading: softLoading,
          onClearFilters: _clearFilters,
        ),
      ),
    );
  }
}

class _DriversListSkeleton extends StatelessWidget {
  const _DriversListSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: const [
        SgSkeleton(height: 72, radius: AppRadii.lg),
        SizedBox(height: 12),
        SgSkeleton(height: 120, radius: AppRadii.lg),
        SizedBox(height: 12),
        SgSkeleton(height: 48, radius: AppRadii.md),
        SizedBox(height: 12),
        SgSkeleton(height: 80, radius: AppRadii.lg),
        SizedBox(height: 10),
        SgSkeleton(height: 80, radius: AppRadii.lg),
        SizedBox(height: 10),
        SgSkeleton(height: 80, radius: AppRadii.lg),
      ],
    );
  }
}

class _DriversListBody extends ConsumerWidget {
  const _DriversListBody({
    required this.hub,
    required this.searchCtrl,
    required this.canCreate,
    required this.softLoading,
    required this.onClearFilters,
  });

  final DriversHubState hub;
  final TextEditingController searchCtrl;
  final bool canCreate;
  final bool softLoading;
  final Future<void> Function() onClearFilters;

  static String _statusLabel(String? s) => switch (s) {
        null => 'All',
        'OnTrip' => 'On trip',
        'OffDuty' => 'Off duty',
        'OnLeave' => 'On leave',
        _ => s,
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final p = context.sg;
    final visible = hub.visible;
    final listBottom = 24.0 +
        MediaQuery.of(context).padding.bottom +
        (canCreate ? 72.0 : 0.0);
    final hasQuery = searchCtrl.text.trim().isNotEmpty;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => ref.read(driversHubProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverToBoxAdapter(child: _StatsRow(stats: hub.stats)),
          if (hub.stats.licensesExpired > 0 ||
              hub.stats.licensesExpiringSoon > 0)
            SliverToBoxAdapter(
              child: _LicenseAlerts(
                stats: hub.stats,
                selected: hub.licenseAlert,
                onSelect: (f) =>
                    ref.read(driversHubProvider.notifier).setLicenseAlert(f),
              ),
            ),
          SliverToBoxAdapter(
            child: _RankingStrip(
              items: hub.ranking,
              unavailable: hub.rankingUnavailable,
            ),
          ),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 4),
              child: SgSectionTitle(
                'Roster',
                subtitle: softLoading
                    ? 'Updating…'
                    : '${visible.length} shown',
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: SizedBox(
              height: 44,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                children: [
                  for (final s in const [
                    null,
                    'Available',
                    'OnTrip',
                    'OffDuty',
                    'OnLeave',
                    'Suspended',
                  ])
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: FilterChip(
                        label: Text(_statusLabel(s)),
                        selected: hub.statusFilter == s,
                        onSelected: softLoading
                            ? null
                            : (_) => ref
                                .read(driversHubProvider.notifier)
                                .setStatusFilter(s),
                      ),
                    ),
                ],
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
              child: TextField(
                controller: searchCtrl,
                onChanged: (v) =>
                    ref.read(driversHubProvider.notifier).setSearch(v),
                decoration: InputDecoration(
                  hintText: 'Search name, phone, plate…',
                  prefixIcon: Icon(
                    Icons.search_rounded,
                    color: p.textMuted,
                  ),
                  suffixIcon: hasQuery
                      ? IconButton(
                          tooltip: 'Clear search',
                          icon: Icon(
                            Icons.close_rounded,
                            color: p.textMuted,
                          ),
                          onPressed: () {
                            searchCtrl.clear();
                            ref
                                .read(driversHubProvider.notifier)
                                .setSearch('');
                          },
                        )
                      : null,
                  filled: true,
                  fillColor: p.card,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                    borderSide: BorderSide(color: p.border),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                    borderSide: BorderSide(
                      color: p.border.withValues(alpha: 0.85),
                    ),
                  ),
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 14,
                  ),
                ),
              ),
            ),
          ),
          if (visible.isEmpty)
            SliverFillRemaining(
              hasScrollBody: false,
              child: hub.hasActiveFilters
                  ? SgEmptyState(
                      icon: Icons.filter_alt_off_outlined,
                      title: 'No matching drivers',
                      subtitle:
                          'Try another status, license alert, or clear search.',
                      actionLabel: 'Clear filters',
                      onAction: onClearFilters,
                    )
                  : SgEmptyState(
                      icon: Icons.groups_outlined,
                      title: 'No drivers yet',
                      subtitle: canCreate
                          ? 'Add your first driver to build the roster.'
                          : 'Drivers will appear here once added to the fleet.',
                      actionLabel: canCreate ? 'Add driver' : null,
                      onAction: canCreate
                          ? () => context.push('/more/drivers/new')
                          : null,
                    ),
            )
          else
            SliverPadding(
              padding: EdgeInsets.fromLTRB(16, 0, 16, listBottom),
              sliver: SliverList.builder(
                itemCount: visible.length,
                itemBuilder: (_, i) => _DriverTile(driver: visible[i]),
              ),
            ),
        ],
      ),
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.stats});
  final DriverStats stats;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 78,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
        children: [
          SgMetricPill(label: 'Total', value: '${stats.totalDrivers}'),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'Available',
            value: '${stats.available}',
            accent: AppColors.success,
          ),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'On trip',
            value: '${stats.onTrip}',
            accent: AppColors.warning,
          ),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'GPS online',
            value: '${stats.gpsOnline}',
            accent: AppColors.cyan,
          ),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'Expired',
            value: '${stats.licensesExpired}',
            accent: AppColors.error,
          ),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'Pending',
            value: '${stats.pendingVerification}',
            accent: AppColors.info,
          ),
          const SizedBox(width: 8),
          SgMetricPill(
            label: 'Incidents',
            value: '${stats.openViolations}',
            accent: AppColors.error,
          ),
        ],
      ),
    );
  }
}

class _LicenseAlerts extends StatelessWidget {
  const _LicenseAlerts({
    required this.stats,
    required this.selected,
    required this.onSelect,
  });

  final DriverStats stats;
  final DriverLicenseAlertFilter selected;
  final ValueChanged<DriverLicenseAlertFilter> onSelect;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Row(
        children: [
          if (stats.licensesExpired > 0)
            Expanded(
              child: _AlertChip(
                label: '${stats.licensesExpired} expired',
                color: AppColors.error,
                selected: selected == DriverLicenseAlertFilter.expired,
                onTap: () => onSelect(DriverLicenseAlertFilter.expired),
              ),
            ),
          if (stats.licensesExpired > 0 && stats.licensesExpiringSoon > 0)
            const SizedBox(width: 8),
          if (stats.licensesExpiringSoon > 0)
            Expanded(
              child: _AlertChip(
                label: '${stats.licensesExpiringSoon} expiring',
                color: AppColors.warning,
                selected: selected == DriverLicenseAlertFilter.expiring,
                onTap: () => onSelect(DriverLicenseAlertFilter.expiring),
              ),
            ),
        ],
      ),
    );
  }
}

class _AlertChip extends StatelessWidget {
  const _AlertChip({
    required this.label,
    required this.color,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final Color color;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: selected ? color.withValues(alpha: 0.14) : p.card,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.md),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.md),
            border: Border.all(
              color: selected
                  ? color.withValues(alpha: 0.45)
                  : p.border.withValues(alpha: 0.8),
            ),
          ),
          child: Row(
            children: [
              Icon(Icons.badge_outlined, size: 18, color: color),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                    color: color,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _RankingStrip extends StatelessWidget {
  const _RankingStrip({
    required this.items,
    required this.unavailable,
  });

  final List<DriverRankItem> items;
  final bool unavailable;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SgSectionTitle(
            'Top drivers',
            subtitle: 'By rating / compliance score',
          ),
          const SizedBox(height: 8),
          SgCard(
            padding: const EdgeInsets.fromLTRB(14, 10, 14, 10),
            child: unavailable || items.isEmpty
                ? const SgInlineEmpty(
                    message:
                        'No ranking available yet. Scores use driver rating when set, otherwise a compliance heuristic (same as ERP).',
                    icon: Icons.leaderboard_outlined,
                  )
                : Column(
                    children: [
                      for (var i = 0; i < items.length; i++) ...[
                        if (i > 0) Divider(height: 1, color: p.divider),
                        InkWell(
                          onTap: () => context
                              .push('/more/drivers/${items[i].driverId}'),
                          borderRadius: BorderRadius.circular(AppRadii.sm),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(vertical: 10),
                            child: Row(
                              children: [
                                Container(
                                  width: 28,
                                  height: 28,
                                  alignment: Alignment.center,
                                  decoration: BoxDecoration(
                                    color: AppColors.primary
                                        .withValues(alpha: 0.12),
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Text(
                                    '${i + 1}',
                                    style: const TextStyle(
                                      fontSize: 12,
                                      fontWeight: FontWeight.w800,
                                      color: AppColors.primary,
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        items[i].driverName,
                                        style: TextStyle(
                                          fontWeight: FontWeight.w600,
                                          color: p.textPrimary,
                                        ),
                                      ),
                                      Text(
                                        items[i].isPartial
                                            ? '${items[i].rating} · partial score'
                                            : items[i].rating,
                                        style: TextStyle(
                                          fontSize: 12,
                                          color: p.textSecondary,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                Text(
                                  '${items[i].score}',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                    color: AppColors.primary,
                                  ),
                                ),
                                Icon(
                                  Icons.chevron_right_rounded,
                                  size: 18,
                                  color: p.textMuted,
                                ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

class _DriverTile extends StatelessWidget {
  const _DriverTile({required this.driver});
  final DriverListItem driver;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final name =
        driver.fullName.trim().isEmpty ? 'Driver' : driver.fullName.trim();
    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      onTap: () => context.push('/more/drivers/${driver.id}'),
      child: Row(
        children: [
          SgAvatar(label: name, size: 46),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                    letterSpacing: -0.2,
                    color: p.textPrimary,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  driver.phone,
                  style: TextStyle(
                    fontSize: 12,
                    color: p.textSecondary,
                  ),
                ),
                if (driver.assignedVehicleRegistration != null) ...[
                  const SizedBox(height: 3),
                  Row(
                    children: [
                      Icon(
                        Icons.local_shipping_outlined,
                        size: 13,
                        color: p.textMuted,
                      ),
                      const SizedBox(width: 4),
                      Expanded(
                        child: Text(
                          driver.assignedVehicleRegistration!,
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: p.textMuted,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
                if (driver.licenseExpired || driver.licenseExpiringSoon) ...[
                  const SizedBox(height: 6),
                  StatusBadge(
                    driver.licenseExpired
                        ? 'License expired'
                        : 'License expiring',
                    color: driver.licenseExpired
                        ? AppColors.error
                        : AppColors.warning,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              StatusBadge(driver.status),
              if (driver.gpsOnline) ...[
                const SizedBox(height: 8),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 6,
                      height: 6,
                      decoration: const BoxDecoration(
                        color: AppColors.success,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 5),
                    Text(
                      'GPS',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.success.withValues(alpha: 0.95),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
