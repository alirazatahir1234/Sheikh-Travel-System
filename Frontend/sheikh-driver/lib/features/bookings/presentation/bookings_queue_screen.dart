import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import 'bookings_notifier.dart';

class BookingsQueueScreen extends ConsumerWidget {
  const BookingsQueueScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(bookingsProvider);
    final df = DateFormat('dd MMM · HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Bookings'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(bookingsProvider.notifier).refresh(),
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
                onPressed: () => ref.read(bookingsProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (state) {
          final visible = state.visible;
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => ref.read(bookingsProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Row(
                      children: [
                        _Mini('Queue', '${state.pendingCount + state.confirmedCount}'),
                        _Mini('Pending', '${state.pendingCount}'),
                        _Mini('Confirmed', '${state.confirmedCount}'),
                        _Mini('Unassigned', '${state.unassignedCount}'),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: SizedBox(
                    height: 48,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      children: [
                        Padding(
                          padding: const EdgeInsets.only(right: 8),
                          child: FilterChip(
                            label: const Text('Dispatch queue'),
                            selected: state.queueOnly,
                            onSelected: (v) => ref
                                .read(bookingsProvider.notifier)
                                .setQueueOnly(v),
                          ),
                        ),
                        for (final s in const [
                          'Pending',
                          'Confirmed',
                          'Started',
                          'Completed',
                          'Cancelled',
                        ])
                          Padding(
                            padding: const EdgeInsets.only(right: 8),
                            child: FilterChip(
                              label: Text(s),
                              selected: state.statusFilter == s,
                              onSelected: (_) => ref
                                  .read(bookingsProvider.notifier)
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
                          ref.read(bookingsProvider.notifier).setSearch(v),
                      decoration: InputDecoration(
                        hintText: 'Search booking, customer, route…',
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
                    child: Center(child: Text('No bookings in queue')),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final b = visible[i];
                        return SgCard(
                          margin: const EdgeInsets.only(bottom: 10),
                          onTap: () => context.push('/bookings/${b.id}'),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      b.bookingNumber,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w800,
                                        fontSize: 15,
                                      ),
                                    ),
                                  ),
                                  StatusBadge(b.status),
                                ],
                              ),
                              const SizedBox(height: 4),
                              Text(
                                b.customerName ?? 'Customer #${b.customerId}',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              Text(
                                '${b.routeName ?? 'Route'} · ${df.format(b.pickupTime.toLocal())}',
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.textSecondary,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                [
                                  b.driverName ?? 'No driver',
                                  b.vehicleName ?? 'No vehicle',
                                  '${b.passengerCount} pax',
                                ].join(' · '),
                                style: TextStyle(
                                  fontSize: 12,
                                  color: b.isUnassigned
                                      ? AppColors.error
                                      : AppColors.textMuted,
                                  fontWeight: b.isUnassigned
                                      ? FontWeight.w600
                                      : FontWeight.w400,
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

class _Mini extends StatelessWidget {
  const _Mini(this.label, this.value);
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
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.primary,
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
