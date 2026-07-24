import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/driver_models.dart';
import 'drivers_notifier.dart';

class DriversListScreen extends ConsumerWidget {
  const DriversListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driversHubProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Drivers'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(driversHubProvider.notifier).refresh(),
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
                Text('$e', textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: () =>
                      ref.read(driversHubProvider.notifier).refresh(),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (hub) {
          final visible = hub.visible;
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => ref.read(driversHubProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(child: _StatsRow(stats: hub.stats)),
                if (hub.stats.licensesExpired > 0 ||
                    hub.stats.licensesExpiringSoon > 0)
                  SliverToBoxAdapter(
                    child: _LicenseAlerts(
                      stats: hub.stats,
                      selected: hub.licenseAlert,
                      onSelect: (f) => ref
                          .read(driversHubProvider.notifier)
                          .setLicenseAlert(f),
                    ),
                  ),
                if (hub.ranking.isNotEmpty)
                  SliverToBoxAdapter(child: _RankingStrip(items: hub.ranking)),
                SliverToBoxAdapter(
                  child: SizedBox(
                    height: 48,
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
                              label: Text(s ?? 'All'),
                              selected: hub.statusFilter == s,
                              onSelected: (_) => ref
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
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                    child: TextField(
                      onChanged: (v) =>
                          ref.read(driversHubProvider.notifier).setSearch(v),
                      decoration: InputDecoration(
                        hintText: 'Search name, phone, plate…',
                        prefixIcon: const Icon(Icons.search_rounded),
                        filled: true,
                        fillColor: Colors.white,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadii.md),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                  ),
                ),
                if (visible.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: Center(
                      child: Text(
                        'No drivers found',
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) => _DriverTile(driver: visible[i]),
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

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.stats});
  final DriverStats stats;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: Row(
        children: [
          _Stat('Total', '${stats.totalDrivers}'),
          _Stat('Available', '${stats.available}'),
          _Stat('On trip', '${stats.onTrip}'),
          _Stat('GPS', '${stats.gpsOnline}'),
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
    return Material(
      color: selected ? color.withValues(alpha: 0.18) : Colors.white,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.md),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
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
  const _RankingStrip({required this.items});
  final List<DriverRankItem> items;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SgSectionTitle('Top drivers'),
          const SizedBox(height: 8),
          SgCard(
            child: Column(
              children: [
                for (var i = 0; i < items.length; i++)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    leading: CircleAvatar(
                      radius: 14,
                      backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                      child: Text(
                        '${i + 1}',
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w800,
                          color: AppColors.primary,
                        ),
                      ),
                    ),
                    title: Text(
                      items[i].driverName,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    subtitle: Text(
                      items[i].isPartial
                          ? '${items[i].rating} · partial score'
                          : items[i].rating,
                      style: const TextStyle(fontSize: 12),
                    ),
                    trailing: Text(
                      '${items[i].score}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: AppColors.primary,
                      ),
                    ),
                    onTap: () =>
                        context.push('/more/drivers/${items[i].driverId}'),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(
            value,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            ),
          ),
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
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
    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      onTap: () => context.push('/more/drivers/${driver.id}'),
      child: Row(
        children: [
          CircleAvatar(
            backgroundColor: AppColors.primary.withValues(alpha: 0.12),
            child: Text(
              driver.fullName.isNotEmpty
                  ? driver.fullName[0].toUpperCase()
                  : '?',
              style: const TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  driver.fullName,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  driver.phone,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                if (driver.assignedVehicleRegistration != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    driver.assignedVehicleRegistration!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textMuted,
                    ),
                  ),
                ],
                if (driver.licenseExpired || driver.licenseExpiringSoon) ...[
                  const SizedBox(height: 4),
                  Text(
                    driver.licenseExpired
                        ? 'License expired'
                        : 'License expiring soon',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: driver.licenseExpired
                          ? AppColors.error
                          : AppColors.warning,
                    ),
                  ),
                ],
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              StatusBadge(driver.status),
              if (driver.gpsOnline) ...[
                const SizedBox(height: 6),
                const Text(
                  'GPS on',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w600,
                    color: AppColors.success,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
