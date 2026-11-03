import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../auth/data/auth_repository.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/vehicle_commands_sheet.dart';

class GpsCommandsHubScreen extends ConsumerWidget {
  const GpsCommandsHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null || !session.canUseGpsCommands) {
      return Scaffold(
        backgroundColor: context.sg.surface,
        appBar: AppBar(title: const Text('GPS commands')),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'Device commands are restricted to Super Admin.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      );
    }

    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final vehicles = hub?.locations ?? const [];

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: const Text('GPS commands')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Select a vehicle to send engine, buzzer, restart, or locate commands.',
            style: TextStyle(color: context.sg.textSecondary),
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
