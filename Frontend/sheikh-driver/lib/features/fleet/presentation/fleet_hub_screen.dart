import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../auth/data/auth_repository.dart';
import 'fleet_hub_notifier.dart';
import 'widgets/fleet_kpi_strip.dart';
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
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => ref.read(fleetHubProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Row(
                      children: [
                        Text(
                          '${hub.kpis.totalVehicles} vehicles · '
                          '${hub.kpis.online} online · '
                          '${hub.kpis.moving} moving · '
                          '${hub.kpis.idle} idle',
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 16,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const Spacer(),
                        TextButton.icon(
                          onPressed: () => context.push('/fleet/map'),
                          icon: const Icon(Icons.map_rounded, size: 18),
                          label: const Text('Live map'),
                        ),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: FleetKpiStrip(
                    kpis: hub.kpis,
                    selected: hub.filter,
                    onSelect: (s) =>
                        ref.read(fleetHubProvider.notifier).setFilter(s),
                  ),
                ),
                const SliverToBoxAdapter(child: SizedBox(height: 8)),
                SliverToBoxAdapter(child: FleetOpsSummaryRow(ops: hub.ops)),
                const SliverToBoxAdapter(child: SizedBox(height: 4)),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                    child: TextField(
                      onChanged: (v) =>
                          ref.read(fleetHubProvider.notifier).setSearch(v),
                      decoration: InputDecoration(
                        hintText: 'Search plate, name, driver…',
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
                        'No vehicles match this filter',
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                    ),
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
