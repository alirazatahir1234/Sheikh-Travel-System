import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/trip_model.dart';
import 'trips_notifier.dart';

class TripDetailScreen extends ConsumerWidget {
  const TripDetailScreen({super.key, required this.tripId});
  final int tripId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);
    return tripsAsync.when(
      loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
      error: (e, _) => Scaffold(body: Center(child: Text(e.toString()))),
      data: (trips) {
        final trip = trips.where((t) => t.id == tripId).firstOrNull;
        if (trip == null) {
          return const Scaffold(body: Center(child: Text('Trip not found')));
        }
        return _TripDetailContent(trip: trip);
      },
    );
  }
}

class _TripDetailContent extends ConsumerWidget {
  const _TripDetailContent({required this.trip});
  final Trip trip;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fmt = DateFormat('dd MMM yyyy, HH:mm');
    final moneyFmt = NumberFormat('#,##0.00');
    final notifier = ref.read(tripsProvider.notifier);

    return Scaffold(
      appBar: AppBar(title: Text(trip.bookingNumber)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _section('Route', [
            _DetailRow('From → To', trip.routeName),
            _DetailRow('Pickup', fmt.format(trip.pickupTime.toLocal())),
            if (trip.dropoffTime != null)
              _DetailRow('Dropoff', fmt.format(trip.dropoffTime!.toLocal())),
          ]),
          const SizedBox(height: 12),
          _section('Customer', [
            _DetailRow('Name', trip.customerName),
          ]),
          const SizedBox(height: 12),
          if (trip.vehicleName != null)
            _section('Vehicle', [
              _DetailRow('Assigned Vehicle', trip.vehicleName!),
            ]),
          const SizedBox(height: 12),
          _section('Fare', [
            _DetailRow('Total Amount', 'PKR ${moneyFmt.format(trip.totalAmount)}'),
            _DetailRow('Status', trip.statusName),
          ]),
          const SizedBox(height: 24),
          if (trip.isConfirmed) ...[
            OutlinedButton(
              onPressed: () => _confirmReject(context, ref),
              style: OutlinedButton.styleFrom(foregroundColor: AppColors.error),
              child: const Text('Reject Trip'),
            ),
            const SizedBox(height: 10),
            FilledButton.icon(
              onPressed: () async {
                await notifier.startTrip(trip.id);
                if (context.mounted) Navigator.pop(context);
              },
              icon: const Icon(Icons.play_arrow_rounded),
              label: const Text('Start Trip'),
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            ),
          ],
          if (trip.isStarted)
            FilledButton.icon(
              onPressed: () async {
                await notifier.completeTrip(trip.id);
                if (context.mounted) Navigator.pop(context);
              },
              icon: const Icon(Icons.check_circle_outline),
              label: const Text('Complete Trip'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.success,
                minimumSize: const Size.fromHeight(48),
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _confirmReject(BuildContext context, WidgetRef ref) async {
    final ctrl = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Reject Trip'),
        content: TextField(
          controller: ctrl,
          decoration: const InputDecoration(labelText: 'Reason for rejection'),
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
    ctrl.dispose();
    if (reason != null && reason.isNotEmpty && context.mounted) {
      await ref.read(tripsProvider.notifier).rejectTrip(trip.id, reason);
      if (context.mounted) Navigator.pop(context);
    }
  }

  Widget _section(String title, List<Widget> rows) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title,
                style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                    fontSize: 14)),
            const SizedBox(height: 10),
            const Divider(height: 1),
            const SizedBox(height: 10),
            ...rows,
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(label,
                style: const TextStyle(color: AppColors.textSecondary, fontSize: 13)),
          ),
          Expanded(
            child: Text(value,
                style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 13,
                    fontWeight: FontWeight.w500)),
          ),
        ],
      ),
    );
  }
}
