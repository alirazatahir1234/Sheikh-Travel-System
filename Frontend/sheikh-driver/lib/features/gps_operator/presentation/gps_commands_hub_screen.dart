import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/vehicle_commands_sheet.dart';

class GpsCommandsHubScreen extends ConsumerWidget {
  const GpsCommandsHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final vehicles = hub?.locations ?? const [];

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('GPS commands')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'Select a vehicle to send engine, buzzer, restart, or locate commands.',
            style: TextStyle(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 16),
          if (vehicles.isEmpty)
            const Center(child: Text('No vehicles with GPS'))
          else
            ...vehicles.map(
              (v) => ListTile(
                title: Text(v.vehicleName),
                subtitle: Text(v.registrationNumber),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => showVehicleCommandsSheet(context, v.vehicleId),
              ),
            ),
        ],
      ),
    );
  }
}
