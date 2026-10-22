import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../auth/data/auth_repository.dart';
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
    final session = ref.watch(fleetSessionProvider);
    final isOwner = session != null &&
        !session.isDriverSession &&
        session.canSeeOwnerEarnings;
    final money = NumberFormat('#,##0.00');
    final dateFmt = DateFormat.yMMMd();
    final p = context.sg;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: Text(isOwner ? 'Driver settlements' : 'Settlements'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.invalidate(settlementsProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => ListView(
          padding: const EdgeInsets.all(16),
          children: const [
            SgSkeleton(height: 88),
            SizedBox(height: 12),
            SgSkeleton(height: 88),
            SizedBox(height: 12),
            SgSkeleton(height: 88),
          ],
        ),
        error: (e, _) => SgErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(settlementsProvider),
        ),
        data: (items) => RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async => ref.invalidate(settlementsProvider),
          child: items.isEmpty
              ? ListView(
                  children: [
                    SizedBox(
                      height: MediaQuery.sizeOf(context).height * 0.55,
                      child: SgEmptyState(
                        icon: Icons.account_balance_wallet_outlined,
                        title: isOwner
                            ? 'No driver settlements yet'
                            : 'No settlements yet',
                        subtitle: isOwner
                            ? 'Generate drafts in ERP Settlements — this list is payables visibility only.'
                            : 'Settlements will appear here when payroll drafts are generated.',
                        actionLabel: 'Refresh',
                        onAction: () => ref.invalidate(settlementsProvider),
                      ),
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (_, i) {
                    final s = items[i];
                    final title = isOwner &&
                            (s.driverName?.trim().isNotEmpty ?? false)
                        ? s.driverName!
                        : '${dateFmt.format(s.periodStart)} – ${dateFmt.format(s.periodEnd)}';
                    final subtitle = isOwner
                        ? '${dateFmt.format(s.periodStart)} – ${dateFmt.format(s.periodEnd)} · ${s.status}'
                        : s.status;
                    return Card(
                      color: p.card,
                      child: ListTile(
                        title: Text(
                          title,
                          style: TextStyle(
                            fontWeight: FontWeight.w700,
                            color: p.textPrimary,
                          ),
                        ),
                        subtitle: Text(
                          subtitle,
                          style: TextStyle(color: p.textSecondary),
                        ),
                        trailing: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Text(
                              'PKR ${money.format(s.payable)}',
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                color: p.textPrimary,
                              ),
                            ),
                            if (isOwner && s.grossCollected != null)
                              Text(
                                'Gross ${money.format(s.grossCollected)}',
                                style: TextStyle(
                                  fontSize: 11,
                                  color: p.textSecondary,
                                ),
                              ),
                          ],
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
