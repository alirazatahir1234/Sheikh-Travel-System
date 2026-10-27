import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
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

    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
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
        loading: () => Center(child: SgBrandLoader()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  ErrorHandler.message(e),
                  textAlign: TextAlign.center,
                ),
              ),
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
                      l.vehicleLabel.toLowerCase().contains(q) ||
                      '${l.vehicleId}'.contains(q) ||
                      (l.driverName?.toLowerCase().contains(q) ?? false) ||
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
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 12,
                      ),
                      decoration: BoxDecoration(
                        color: p.card,
                        borderRadius: BorderRadius.circular(AppRadii.md),
                        border: Border.all(
                          color: p.border.withValues(alpha: 0.75),
                        ),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.local_gas_station_rounded,
                            size: 18,
                            color: AppColors.primary.withValues(alpha: 0.9),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              '${visible.length} fills · ${totalLiters.toStringAsFixed(0)} L · ${currency.format(totalCost)}',
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                fontSize: 13,
                                letterSpacing: -0.1,
                                color: p.textPrimary,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
                    child: SgSearchField(
                      hintText: 'Search vehicle, station…',
                      onChanged: (v) => setState(() => _query = v),
                    ),
                  ),
                ),
                if (visible.isEmpty)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: Center(
                      child: Text(
                        'No fuel logs',
                        style: TextStyle(
                          color: p.textSecondary,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final l = visible[i];
                        return SgCard(
                          margin: const EdgeInsets.only(bottom: 8),
                          padding: const EdgeInsets.fromLTRB(14, 12, 12, 12),
                          onTap: () => context.push('/fuel/${l.id}'),
                          child: Row(
                            children: [
                              Container(
                                width: 40,
                                height: 40,
                                decoration: BoxDecoration(
                                  color: AppColors.primary.withValues(alpha: 0.10),
                                  borderRadius:
                                      BorderRadius.circular(AppRadii.sm),
                                ),
                                child: const Icon(
                                  Icons.local_gas_station_rounded,
                                  size: 20,
                                  color: AppColors.primary,
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      l.vehicleLabel,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: TextStyle(
                                        fontWeight: FontWeight.w800,
                                        fontSize: 15,
                                        letterSpacing: -0.2,
                                        color: p.textPrimary,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      '${l.liters.toStringAsFixed(1)} L · ${l.fuelType}',
                                      style: TextStyle(
                                        color: p.textSecondary,
                                        fontSize: 13,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      [
                                        df.format(l.fuelDate.toLocal()),
                                        if (l.station != null) l.station!,
                                      ].join(' · '),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: p.textMuted,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(width: 8),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 6,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.primary.withValues(alpha: 0.10),
                                  borderRadius:
                                      BorderRadius.circular(AppRadii.pill),
                                  border: Border.all(
                                    color: AppColors.primary
                                        .withValues(alpha: 0.22),
                                  ),
                                ),
                                child: Text(
                                  currency.format(l.totalCost),
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                    fontSize: 13,
                                    color: AppColors.primaryDark,
                                  ),
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

    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: const Text('Fuel log')),
      body: async.when(
        loading: () => Center(child: SgBrandLoader()),
        error: (e, _) => Center(child: Text(ErrorHandler.message(e))),
        data: (l) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            SgCard(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
              child: Column(
                children: [
                  _Row('Vehicle', l.vehicleLabel),
                  _Row('Driver', l.driverLabel),
                  _Row('Liters', l.liters.toStringAsFixed(2)),
                  _Row('Price / L', l.pricePerLiter.toStringAsFixed(2)),
                  _Row('Total', l.totalCost.toStringAsFixed(2), emphasize: true),
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
                child: const Text(
                  'Open vehicle',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value, {this.emphasize = false});
  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 7),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: TextStyle(
                color: p.textMuted,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                fontWeight: emphasize ? FontWeight.w800 : FontWeight.w700,
                fontSize: emphasize ? 14 : 13.5,
                letterSpacing: -0.15,
                color: emphasize ? AppColors.primaryDark : p.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
