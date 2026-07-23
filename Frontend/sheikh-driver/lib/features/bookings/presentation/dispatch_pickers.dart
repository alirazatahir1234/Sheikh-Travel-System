import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../features/drivers/data/drivers_api.dart';
import '../../../features/fleet/data/fleet_api.dart';

Future<int?> pickDriverId(BuildContext context, WidgetRef ref) async {
  final drivers = await ref.read(driversApiProvider).list();
  if (!context.mounted) return null;
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (ctx) {
      return SafeArea(
        child: SizedBox(
          height: MediaQuery.sizeOf(ctx).height * 0.65,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 0, 20, 8),
                child: Text(
                  'Assign driver',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: drivers.length,
                  itemBuilder: (_, i) {
                    final d = drivers[i];
                    return ListTile(
                      title: Text(d.fullName),
                      subtitle: Text('${d.phone} · ${d.status}'),
                      onTap: () => Navigator.pop(ctx, d.id),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}

Future<int?> pickVehicleId(BuildContext context, WidgetRef ref) async {
  final vehicles = await ref.read(fleetApiProvider).getVehicles();
  if (!context.mounted) return null;
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (ctx) {
      return SafeArea(
        child: SizedBox(
          height: MediaQuery.sizeOf(ctx).height * 0.65,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 0, 20, 8),
                child: Text(
                  'Assign vehicle',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: vehicles.length,
                  itemBuilder: (_, i) {
                    final v = vehicles[i];
                    return ListTile(
                      title: Text(v.name),
                      subtitle: Text('${v.registrationNumber} · ${v.status}'),
                      onTap: () => Navigator.pop(ctx, v.id),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}
