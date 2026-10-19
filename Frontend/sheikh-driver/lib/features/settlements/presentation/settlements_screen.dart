import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../data/settlements_api.dart';
import '../domain/settlement_models.dart';

final settlementsProvider =
    FutureProvider.autoDispose<List<DriverSettlementItem>>(
  (ref) => ref.read(settlementsApiProvider).getMySettlements(),
);

class SettlementsScreen extends ConsumerWidget {
  const SettlementsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(settlementsProvider);
    final money = NumberFormat('#,##0.00');
    final dateFmt = DateFormat.yMMMd();

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Settlements'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(settlementsProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e', textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => ref.invalidate(settlementsProvider),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (items) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(settlementsProvider),
          child: items.isEmpty
              ? ListView(
                  children: const [
                    SizedBox(height: 120),
                    Center(child: Text('No settlements yet.')),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (_, i) {
                    final s = items[i];
                    return Card(
                      child: ListTile(
                        title: Text(
                          '${dateFmt.format(s.periodStart)} – ${dateFmt.format(s.periodEnd)}',
                        ),
                        subtitle: Text(s.status),
                        trailing: Text(
                          'PKR ${money.format(s.payable)}',
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                      ),
                    );
                  },
                ),
        ),
      ),
    );
  }
}
