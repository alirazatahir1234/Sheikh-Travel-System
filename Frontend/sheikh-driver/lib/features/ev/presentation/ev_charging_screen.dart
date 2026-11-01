import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/ev_api.dart';
import '../domain/ev_models.dart';

final _evStatusProvider = FutureProvider.autoDispose<DriverEvStatus?>((ref) {
  return ref.watch(evApiProvider).fetchStatus();
});

final _docExpiriesProvider =
    FutureProvider.autoDispose<List<VehicleDocExpiry>>((ref) {
  return ref.watch(evApiProvider).fetchDocumentExpiries();
});

class EvChargingScreen extends ConsumerWidget {
  const EvChargingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final evAsync = ref.watch(_evStatusProvider);
    final docsAsync = ref.watch(_docExpiriesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('EV & documents')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          evAsync.when(
            data: (ev) {
              if (ev == null) {
                return const Card(
                  child: ListTile(
                    leading: Icon(Icons.electric_car_outlined),
                    title: Text('No electric vehicle assigned'),
                    subtitle: Text(
                        'Battery view appears when your active vehicle is Electric.'),
                  ),
                );
              }
              return Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(ev.vehicleName,
                          style: Theme.of(context).textTheme.titleMedium),
                      const SizedBox(height: 8),
                      if (ev.batteryPercent != null) ...[
                        LinearProgressIndicator(
                            value: (ev.batteryPercent! / 100).clamp(0, 1)),
                        const SizedBox(height: 6),
                        Text(
                            '${ev.batteryPercent!.round()}% battery · est. ${ev.estimatedRangeKm?.round() ?? ev.electricRangeKm?.round() ?? "—"} km'),
                      ] else
                        const Text('No live battery reading'),
                      if (ev.rangeAlert)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Row(
                            children: [
                              Icon(Icons.battery_alert,
                                  color: Colors.orange.shade800, size: 20),
                              const SizedBox(width: 6),
                              Text('Range alert — plan charging',
                                  style:
                                      TextStyle(color: Colors.orange.shade900)),
                            ],
                          ),
                        ),
                      if (ev.nearbyStations.isNotEmpty) ...[
                        const SizedBox(height: 12),
                        Text('Charging stations',
                            style: Theme.of(context).textTheme.labelLarge),
                        ...ev.nearbyStations.map(
                          (s) => ListTile(
                            dense: true,
                            contentPadding: EdgeInsets.zero,
                            leading: const Icon(Icons.ev_station),
                            title: Text(s.name),
                            subtitle: Text(
                                s.address ?? '${s.latitude}, ${s.longitude}'),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              );
            },
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, _) => Text('Could not load EV status: $e'),
          ),
          const SizedBox(height: 16),
          Text('Vehicle documents',
              style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 8),
          docsAsync.when(
            data: (docs) {
              if (docs.isEmpty) {
                return const Text(
                    'No expiring documents on your assigned vehicle.');
              }
              return Column(
                children: docs
                    .map(
                      (d) => ListTile(
                        leading: Icon(
                          d.status == 'Expired'
                              ? Icons.error_outline
                              : Icons.event,
                          color: d.daysRemaining <= 30 ? Colors.orange : null,
                        ),
                        title: Text(d.documentType),
                        subtitle: Text(
                          d.expiryDate != null
                              ? 'Expires ${d.expiryDate!.toLocal().toString().split(' ').first} (${d.daysRemaining}d)'
                              : d.status,
                        ),
                      ),
                    )
                    .toList(),
              );
            },
            loading: () => const LinearProgressIndicator(),
            error: (e, _) => Text('Documents: $e'),
          ),
        ],
      ),
    );
  }
}
