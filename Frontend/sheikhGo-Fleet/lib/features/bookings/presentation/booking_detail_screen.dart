import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/bookings_api.dart';
import 'bookings_notifier.dart';
import 'dispatch_pickers.dart';

class BookingDetailScreen extends ConsumerWidget {
  const BookingDetailScreen({super.key, required this.bookingId});
  final int bookingId;

  Future<void> _assignDriver(BuildContext context, WidgetRef ref) async {
    final driverId = await pickDriverId(context, ref);
    if (driverId == null) return;
    try {
      await ref.read(bookingsApiProvider).assignDriver(bookingId, driverId);
      ref.invalidate(bookingDetailProvider(bookingId));
      ref.read(bookingsProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Driver assigned')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  Future<void> _assignVehicle(BuildContext context, WidgetRef ref) async {
    final vehicleId = await pickVehicleId(context, ref);
    if (vehicleId == null) return;
    try {
      await ref.read(bookingsApiProvider).assignVehicle(bookingId, vehicleId);
      ref.invalidate(bookingDetailProvider(bookingId));
      ref.read(bookingsProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Vehicle assigned')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  Future<void> _createTrip(BuildContext context, WidgetRef ref) async {
    try {
      final tripId =
          await ref.read(bookingsApiProvider).createTripFromBooking(bookingId);
      if (!context.mounted) return;
      if (tripId > 0) {
        context.push('/trips/$tripId');
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Trip created')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  Future<void> _confirm(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(bookingsApiProvider).updateStatus(bookingId, 'Confirmed');
      ref.invalidate(bookingDetailProvider(bookingId));
      ref.read(bookingsProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Booking confirmed')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(bookingDetailProvider(bookingId));
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Booking detail')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () =>
                    ref.invalidate(bookingDetailProvider(bookingId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (b) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
          children: [
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          b.bookingNumber,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      StatusBadge(b.status),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    b.customerName ?? 'Customer #${b.customerId}',
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  Text(
                    df.format(b.pickupTime.toLocal()),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textMuted,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            const SgSectionTitle('Trip info'),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                children: [
                  _Row('Route', b.routeName ?? 'Route #${b.routeId}'),
                  _Row('Passengers', '${b.passengerCount}'),
                  _Row('Amount', 'PKR ${b.totalAmount.toStringAsFixed(0)}'),
                  if (b.notes != null && b.notes!.isNotEmpty)
                    _Row('Notes', b.notes!),
                ],
              ),
            ),
            const SizedBox(height: 12),
            const SgSectionTitle('Dispatch'),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                children: [
                  _Row('Driver', b.driverName ?? 'Unassigned'),
                  _Row('Vehicle', b.vehicleName ?? 'Unassigned'),
                ],
              ),
            ),
            const SizedBox(height: 16),
            if (b.needsDispatch) ...[
              if (b.status.toLowerCase() == 'pending')
                FilledButton(
                  onPressed: () => _confirm(context, ref),
                  child: const Text('Confirm booking'),
                ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _assignDriver(context, ref),
                      icon: const Icon(Icons.person_add_alt_1_outlined),
                      label: const Text('Driver'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _assignVehicle(context, ref),
                      icon: const Icon(Icons.local_shipping_outlined),
                      label: const Text('Vehicle'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              FilledButton.tonalIcon(
                onPressed: () => _createTrip(context, ref),
                icon: const Icon(Icons.route_outlined),
                label: const Text('Create ops trip'),
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
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(fontSize: 13, color: AppColors.textMuted),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
