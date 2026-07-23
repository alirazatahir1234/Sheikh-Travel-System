import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/staff_fuel_api.dart';
import '../domain/staff_fuel_models.dart';

final staffFuelListProvider =
    AsyncNotifierProvider<StaffFuelListNotifier, List<StaffFuelLog>>(
  StaffFuelListNotifier.new,
);

class StaffFuelListNotifier extends AsyncNotifier<List<StaffFuelLog>> {
  @override
  Future<List<StaffFuelLog>> build() =>
      ref.read(staffFuelApiProvider).list();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(staffFuelApiProvider).list());
  }
}

final staffFuelDetailProvider =
    FutureProvider.family<StaffFuelLog, int>((ref, id) {
  return ref.read(staffFuelApiProvider).getById(id);
});

class StaffFuelListScreen extends ConsumerStatefulWidget {
  const StaffFuelListScreen({super.key});

  @override
  ConsumerState<StaffFuelListScreen> createState() =>
      _StaffFuelListScreenState();
}

class _StaffFuelListScreenState extends ConsumerState<StaffFuelListScreen> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(staffFuelListProvider);
    final df = DateFormat('dd MMM yyyy');
    final currency = NumberFormat.compactCurrency(symbol: '');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Fuel logs'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(staffFuelListProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () =>
                    ref.read(staffFuelListProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (logs) {
          final q = _query.trim().toLowerCase();
          final visible = q.isEmpty
              ? logs
              : logs
                  .where((l) =>
                      '${l.vehicleId}'.contains(q) ||
                      (l.station?.toLowerCase().contains(q) ?? false) ||
                      l.fuelType.toLowerCase().contains(q))
                  .toList();
          final totalCost =
              visible.fold<double>(0, (sum, l) => sum + l.totalCost);
          final totalLiters =
              visible.fold<double>(0, (sum, l) => sum + l.liters);

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () =>
                ref.read(staffFuelListProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            '${visible.length} fills · ${totalLiters.toStringAsFixed(0)} L · ${currency.format(totalCost)}',
                            style: const TextStyle(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
                    child: TextField(
                      onChanged: (v) => setState(() => _query = v),
                      decoration: InputDecoration(
                        hintText: 'Search vehicle, station…',
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
                    child: Center(child: Text('No fuel logs')),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final l = visible[i];
                        return SgCard(
                          margin: const EdgeInsets.only(bottom: 10),
                          onTap: () => context.push('/fuel/${l.id}'),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Vehicle #${l.vehicleId}',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      '${l.liters.toStringAsFixed(1)} L · ${l.fuelType}',
                                      style: const TextStyle(
                                        color: AppColors.textSecondary,
                                        fontSize: 13,
                                      ),
                                    ),
                                    Text(
                                      [
                                        df.format(l.fuelDate.toLocal()),
                                        if (l.station != null) l.station!,
                                      ].join(' · '),
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textMuted,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Text(
                                currency.format(l.totalCost),
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                  color: AppColors.primary,
                                ),
                              ),
                            ],
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

class StaffFuelDetailScreen extends ConsumerWidget {
  const StaffFuelDetailScreen({super.key, required this.logId});
  final int logId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(staffFuelDetailProvider(logId));
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Fuel log')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (l) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SgCard(
              child: Column(
                children: [
                  _Row('Vehicle', '#${l.vehicleId}'),
                  _Row('Driver', l.driverId != null ? '#${l.driverId}' : '—'),
                  _Row('Liters', l.liters.toStringAsFixed(2)),
                  _Row('Price / L', l.pricePerLiter.toStringAsFixed(2)),
                  _Row('Total', l.totalCost.toStringAsFixed(2)),
                  _Row('Odometer', l.odometerReading.toStringAsFixed(0)),
                  _Row('Fuel type', l.fuelType),
                  _Row('Station', l.station ?? '—'),
                  _Row('Date', df.format(l.fuelDate.toLocal())),
                ],
              ),
            ),
            if (l.vehicleId > 0) ...[
              const SizedBox(height: 12),
              TextButton(
                onPressed: () =>
                    context.push('/fleet/vehicles/${l.vehicleId}'),
                child: const Text('Open vehicle'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(color: AppColors.textMuted, fontSize: 13),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
