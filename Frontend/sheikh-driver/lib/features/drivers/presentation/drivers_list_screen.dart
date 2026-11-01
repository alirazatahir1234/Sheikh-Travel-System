import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
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
  }

  @override
  void dispose() {
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
    final visible = hub.visible;
    final listBottom =
        24.0 + MediaQuery.of(context).padding.bottom + (canCreate ? 72.0 : 0.0);
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
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: _RosterHeader(
                count: visible.length,
                softLoading: softLoading,
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: SizedBox(
              height: 40,
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
                        visualDensity: VisualDensity.compact,
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
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
              child: SgSearchField(
                controller: searchCtrl,
                hintText: 'Search name, phone, plate…',
                onChanged: (v) =>
                    ref.read(driversHubProvider.notifier).setSearch(v),
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
      height: 76,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
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
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
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
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.md),
            border: Border.all(
              color: selected
                  ? color.withValues(alpha: 0.55)
                  : p.border.withValues(alpha: 0.75),
              width: selected ? 1.5 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(Icons.badge_outlined, size: 17, color: color),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 12.5,
                    letterSpacing: -0.1,
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

class _RosterHeader extends StatelessWidget {
  const _RosterHeader({
    required this.count,
    required this.softLoading,
  });

  final int count;
  final bool softLoading;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.10),
            borderRadius: BorderRadius.circular(AppRadii.sm),
            border: Border.all(
              color: AppColors.primary.withValues(alpha: 0.20),
            ),
          ),
          child: const Icon(
            Icons.groups_rounded,
            size: 22,
            color: AppColors.primary,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Roster',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: p.textPrimary,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.35,
                      height: 1.15,
                    ),
              ),
              const SizedBox(height: 3),
              Text(
                softLoading ? 'Updating…' : 'Tap a driver for profile & docs',
                style: TextStyle(
                  fontSize: 12,
                  color: p.textSecondary,
                  height: 1.2,
                ),
              ),
            ],
          ),
        ),
        Container(
          constraints: const BoxConstraints(minWidth: 36),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.10),
            borderRadius: BorderRadius.circular(AppRadii.pill),
            border: Border.all(
              color: AppColors.primary.withValues(alpha: 0.22),
            ),
          ),
          alignment: Alignment.center,
          child: Text(
            softLoading ? '…' : '$count',
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w800,
              color: AppColors.primary,
              letterSpacing: -0.2,
            ),
          ),
        ),
      ],
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

  static Color _rankColor(int index) => switch (index) {
        0 => const Color(0xFFC9A227),
        1 => const Color(0xFF7A8796),
        2 => const Color(0xFFB87333),
        _ => AppColors.primary,
      };

  static IconData _rankIcon(int index) => switch (index) {
        0 => Icons.emoji_events_rounded,
        1 => Icons.military_tech_rounded,
        2 => Icons.workspace_premium_rounded,
        _ => Icons.tag_rounded,
      };

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      AppColors.primary.withValues(alpha: 0.20),
                      AppColors.accent.withValues(alpha: 0.12),
                    ],
                  ),
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                  border: Border.all(
                    color: AppColors.primary.withValues(alpha: 0.22),
                  ),
                ),
                child: const Icon(
                  Icons.leaderboard_rounded,
                  size: 22,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Top drivers',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: p.textPrimary,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.35,
                            height: 1.15,
                          ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      'By rating / compliance score',
                      style: TextStyle(
                        fontSize: 12,
                        color: p.textSecondary,
                        height: 1.2,
                      ),
                    ),
                  ],
                ),
              ),
              if (!unavailable && items.isNotEmpty)
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(AppRadii.pill),
                    border: Border.all(
                      color: AppColors.primary.withValues(alpha: 0.22),
                    ),
                  ),
                  child: Text(
                    'Top ${items.length}',
                    style: const TextStyle(
                      fontSize: 11.5,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.1,
                      color: AppColors.primary,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 12),
          SgCard(
            padding: EdgeInsets.zero,
            child: unavailable || items.isEmpty
                ? const Padding(
                    padding: EdgeInsets.fromLTRB(16, 18, 16, 18),
                    child: SgInlineEmpty(
                      message:
                          'No ranking available yet. Top Drivers blend GPS safety (60%) with compliance (40%), same as ERP.',
                      icon: Icons.leaderboard_outlined,
                    ),
                  )
                : Column(
                    children: [
                      for (var i = 0; i < items.length; i++) ...[
                        if (i > 0)
                          Divider(
                            height: 1,
                            thickness: 1,
                            indent: 68,
                            endIndent: 16,
                            color: p.divider.withValues(alpha: 0.9),
                          ),
                        _RankingRow(
                          rank: i + 1,
                          item: items[i],
                          accent: _rankColor(i),
                          icon: _rankIcon(i),
                          highlight: i == 0,
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

class _RankingRow extends StatelessWidget {
  const _RankingRow({
    required this.rank,
    required this.item,
    required this.accent,
    required this.icon,
    this.highlight = false,
  });

  final int rank;
  final DriverRankItem item;
  final Color accent;
  final IconData icon;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final subtitle =
        item.isPartial ? '${item.rating} · partial score' : item.rating;

    return Material(
      color: highlight ? accent.withValues(alpha: 0.06) : Colors.transparent,
      child: InkWell(
        onTap: () => context.push('/more/drivers/${item.driverId}'),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 11, 8, 11),
          child: Row(
            children: [
              Container(
                width: 36,
                height: 36,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.16),
                  borderRadius: BorderRadius.circular(11),
                  border: Border.all(
                    color: accent.withValues(alpha: 0.36),
                    width: 1.2,
                  ),
                ),
                child: rank <= 3
                    ? Icon(icon, size: 19, color: accent)
                    : Text(
                        '$rank',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w800,
                          color: accent,
                        ),
                      ),
              ),
              const SizedBox(width: 12),
              SgAvatar(label: item.driverName, size: 42, color: accent),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      item.driverName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 14.5,
                        letterSpacing: -0.25,
                        color: p.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      subtitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 12,
                        height: 1.2,
                        color: p.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Container(
                constraints: const BoxConstraints(minWidth: 40),
                padding:
                    const EdgeInsets.symmetric(horizontal: 11, vertical: 7),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.pill),
                  border: Border.all(
                    color: AppColors.primary.withValues(alpha: 0.28),
                  ),
                ),
                alignment: Alignment.center,
                child: Text(
                  '${item.score}',
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 13.5,
                    letterSpacing: -0.25,
                    color: AppColors.primaryDark,
                  ),
                ),
              ),
              Icon(
                Icons.chevron_right_rounded,
                size: 22,
                color: p.textMuted.withValues(alpha: 0.9),
              ),
            ],
          ),
        ),
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
    final phone = driver.phone.trim();
    final plate = driver.assignedVehicleRegistration?.trim();

    return SgCard(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.fromLTRB(12, 12, 8, 12),
      onTap: () => context.push('/more/drivers/${driver.id}'),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          SgAvatar(label: name, size: 48),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 15.5,
                          letterSpacing: -0.3,
                          height: 1.15,
                          color: p.textPrimary,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    StatusBadge(driver.status),
                  ],
                ),
                if (phone.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      Icon(
                        Icons.phone_rounded,
                        size: 14,
                        color: p.textMuted,
                      ),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          phone,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 13,
                            height: 1.2,
                            color: p.textSecondary,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
                if (plate != null && plate.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 3,
                    ),
                    decoration: BoxDecoration(
                      color: p.chipBg,
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                      border: Border.all(
                        color: p.border.withValues(alpha: 0.75),
                      ),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.directions_car_filled_rounded,
                          size: 13,
                          color: p.textSecondary,
                        ),
                        const SizedBox(width: 5),
                        Flexible(
                          child: Text(
                            plate,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 11.5,
                              fontWeight: FontWeight.w800,
                              letterSpacing: 0.35,
                              color: p.textPrimary,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                if (driver.gpsOnline ||
                    driver.licenseExpired ||
                    driver.licenseExpiringSoon) ...[
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      if (driver.gpsOnline)
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.success.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(AppRadii.pill),
                            border: Border.all(
                              color: AppColors.success.withValues(alpha: 0.28),
                            ),
                          ),
                          child: Row(
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
                                'GPS online',
                                style: TextStyle(
                                  fontSize: 11,
                                  fontWeight: FontWeight.w800,
                                  color:
                                      AppColors.success.withValues(alpha: 0.98),
                                ),
                              ),
                            ],
                          ),
                        ),
                      if (driver.licenseExpired || driver.licenseExpiringSoon)
                        StatusBadge(
                          driver.licenseExpired
                              ? 'License expired'
                              : 'License expiring',
                          color: driver.licenseExpired
                              ? AppColors.error
                              : AppColors.warning,
                        ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 2),
          Icon(
            Icons.chevron_right_rounded,
            size: 22,
            color: p.textMuted.withValues(alpha: 0.9),
          ),
        ],
      ),
    );
  }
}
