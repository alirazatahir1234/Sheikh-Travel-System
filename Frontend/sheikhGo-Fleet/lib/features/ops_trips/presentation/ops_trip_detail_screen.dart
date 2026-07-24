import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/bookings/presentation/dispatch_pickers.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/ops_trips_api.dart';
import 'ops_trips_notifier.dart';

class OpsTripDetailScreen extends ConsumerWidget {
  const OpsTripDetailScreen({super.key, required this.tripId});
  final int tripId;

  Future<void> _assignDriver(BuildContext context, WidgetRef ref) async {
    final driverId = await pickDriverId(context, ref);
    if (driverId == null) return;
    try {
      await ref.read(opsTripsApiProvider).assignDriver(tripId, driverId);
      ref.invalidate(opsTripDetailProvider(tripId));
      ref.read(opsTripsProvider.notifier).refresh();
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
      await ref.read(opsTripsApiProvider).assignVehicle(tripId, vehicleId);
      ref.invalidate(opsTripDetailProvider(tripId));
      ref.read(opsTripsProvider.notifier).refresh();
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

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(opsTripDetailProvider(tripId));
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Trip detail')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () =>
                    ref.invalidate(opsTripDetailProvider(tripId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (t) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
          children: [
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          t.tripNumber,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      StatusBadge(t.status),
                    ],
                  ),
                  if (t.tripName.isNotEmpty) ...[
                    const SizedBox(height: 4),
                    Text(t.tripName),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    t.customerName ?? 'Customer',
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  Text(
                    df.format(t.plannedStart.toLocal()),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textMuted,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            const SgSectionTitle('Route'),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                children: [
                  _Row('Pickup', t.pickupAddress ?? '—'),
                  _Row('Destination', t.destinationAddress ?? '—'),
                  _Row('Route', t.routeName ?? '—'),
                  _Row('Type', t.tripType),
                  _Row('Priority', t.priority),
                  _Row('Passengers', '${t.passengerCount}'),
                ],
              ),
            ),
            const SizedBox(height: 12),
            const SgSectionTitle('Assignment'),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                children: [
                  _Row(
                    'Driver',
                    t.driverName ?? 'Unassigned',
                    onTap: t.driverId != null
                        ? () => context.push('/more/drivers/${t.driverId}')
                        : null,
                  ),
                  _Row(
                    'Vehicle',
                    t.vehicleName ?? 'Unassigned',
                    onTap: t.vehicleId != null
                        ? () => context.push('/fleet/vehicles/${t.vehicleId}')
                        : null,
                  ),
                  _Row('GPS', t.gpsOnline ? 'Online' : 'Offline'),
                  _Row('Open alerts', '${t.openAlertCount}'),
                  if (t.driverNotes != null && t.driverNotes!.isNotEmpty)
                    _Row('Notes', t.driverNotes!),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () => _assignDriver(context, ref),
                    icon: const Icon(Icons.person_add_alt_1_outlined),
                    label: const Text('Assign driver'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () => _assignVehicle(context, ref),
                    icon: const Icon(Icons.local_shipping_outlined),
                    label: const Text('Assign vehicle'),
                  ),
                ),
              ],
            ),
            if (t.timeline.isNotEmpty) ...[
              const SizedBox(height: 12),
              const SgSectionTitle('Timeline'),
              const SizedBox(height: 8),
              SgCard(
                child: Column(
                  children: [
                    for (final e in t.timeline)
                      ListTile(
                        dense: true,
                        contentPadding: EdgeInsets.zero,
                        title: Text(
                          e.toStatus,
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        subtitle: Text(
                          [
                            df.format(e.changedAtUtc.toLocal()),
                            if (e.changedBy != null) e.changedBy!,
                            if (e.note != null) e.note!,
                          ].join(' · '),
                          style: const TextStyle(fontSize: 12),
                        ),
                      ),
                  ],
                ),
              ),
            ],
            if (t.openAlertCount > 0) ...[
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: () => context.push('/alerts'),
                icon: const Icon(Icons.warning_amber_rounded),
                label: Text('View ${t.openAlertCount} alerts'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value, {this.onTap});
  final String label;
  final String value;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
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
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: onTap != null ? AppColors.primary : null,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
