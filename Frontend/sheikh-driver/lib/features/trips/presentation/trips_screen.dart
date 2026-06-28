import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/trip_model.dart';
import 'trips_notifier.dart';

class TripsScreen extends ConsumerWidget {
  const TripsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('My Trips'),
          actions: [
            IconButton(
              icon: const Icon(Icons.refresh),
              onPressed: () => ref.read(tripsProvider.notifier).refresh(),
            ),
          ],
          bottom: const TabBar(
            labelColor: Colors.white,
            unselectedLabelColor: Colors.white70,
            indicatorColor: Colors.white,
            tabs: [
              Tab(text: 'Active'),
              Tab(text: 'Completed'),
            ],
          ),
        ),
        body: tripsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 48, color: AppColors.error),
                const SizedBox(height: 8),
                Text(e.toString(), textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => ref.read(tripsProvider.notifier).refresh(),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (trips) {
            final active = trips.where((t) => t.isActionable).toList();
            final completed = trips.where((t) => t.isCompleted || t.isCancelled).toList();
            return TabBarView(
              children: [
                _TripList(trips: active, emptyLabel: 'No active trips'),
                _TripList(trips: completed, emptyLabel: 'No completed trips'),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _TripList extends StatelessWidget {
  const _TripList({required this.trips, required this.emptyLabel});
  final List<Trip> trips;
  final String emptyLabel;

  @override
  Widget build(BuildContext context) {
    if (trips.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.directions_car_outlined, size: 56, color: AppColors.textSecondary),
            const SizedBox(height: 12),
            Text(emptyLabel, style: const TextStyle(color: AppColors.textSecondary)),
          ],
        ),
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: trips.length,
      separatorBuilder: (_, __) => const SizedBox(height: 10),
      itemBuilder: (context, i) => _TripCard(trip: trips[i]),
    );
  }
}

class _TripCard extends ConsumerWidget {
  const _TripCard({required this.trip});
  final Trip trip;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fmt = DateFormat('dd MMM, HH:mm');
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.go('/trips/${trip.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      trip.bookingNumber,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  _StatusChip(trip: trip),
                ],
              ),
              const SizedBox(height: 8),
              _InfoRow(icon: Icons.person_outline, text: trip.customerName),
              const SizedBox(height: 4),
              _InfoRow(icon: Icons.route_outlined, text: trip.routeName),
              const SizedBox(height: 4),
              _InfoRow(icon: Icons.schedule_outlined, text: fmt.format(trip.pickupTime.toLocal())),
              if (trip.vehicleName != null) ...[
                const SizedBox(height: 4),
                _InfoRow(icon: Icons.directions_car_outlined, text: trip.vehicleName!),
              ],
              if (trip.isActionable) ...[
                const SizedBox(height: 12),
                _ActionButtons(trip: trip),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ActionButtons extends ConsumerWidget {
  const _ActionButtons({required this.trip});
  final Trip trip;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notifier = ref.read(tripsProvider.notifier);
    if (trip.isStarted) {
      return SizedBox(
        width: double.infinity,
        child: FilledButton.icon(
          onPressed: () => notifier.completeTrip(trip.id),
          icon: const Icon(Icons.check_circle_outline),
          label: const Text('Complete Trip'),
          style: FilledButton.styleFrom(backgroundColor: AppColors.success),
        ),
      );
    }
    return Row(
      children: [
        Expanded(
          child: OutlinedButton(
            onPressed: () => _showRejectDialog(context, ref),
            style: OutlinedButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Reject'),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: FilledButton.icon(
            onPressed: () => notifier.startTrip(trip.id),
            icon: const Icon(Icons.play_arrow_rounded),
            label: const Text('Start'),
          ),
        ),
      ],
    );
  }

  Future<void> _showRejectDialog(BuildContext context, WidgetRef ref) async {
    final ctrl = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Reject Trip'),
        content: TextField(
          controller: ctrl,
          decoration: const InputDecoration(labelText: 'Reason'),
          autofocus: true,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(context, ctrl.text),
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            child: const Text('Reject'),
          ),
        ],
      ),
    );
    if (reason != null && reason.isNotEmpty) {
      await ref.read(tripsProvider.notifier).rejectTrip(trip.id, reason);
    }
    ctrl.dispose();
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.trip});
  final Trip trip;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (trip.status) {
      2 => ('Confirmed', AppColors.primary),
      3 => ('In Progress', AppColors.success),
      4 => ('Completed', AppColors.textSecondary),
      5 => ('Cancelled', AppColors.error),
      _ => (trip.statusName, AppColors.warning),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label,
          style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w600)),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.icon, required this.text});
  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 15, color: AppColors.textSecondary),
        const SizedBox(width: 6),
        Expanded(
          child: Text(text,
              style: const TextStyle(fontSize: 13, color: AppColors.textSecondary)),
        ),
      ],
    );
  }
}
