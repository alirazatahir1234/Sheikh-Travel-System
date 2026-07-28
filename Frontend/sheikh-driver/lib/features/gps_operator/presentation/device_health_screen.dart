import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/presentation/vehicle_commands_sheet.dart';
import '../../fleet/presentation/vehicle_detail_screen.dart';

class DeviceHealthScreen extends ConsumerWidget {
  const DeviceHealthScreen({super.key, required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final gpsAsync = ref.watch(vehicleGpsInfoProvider(vehicleId));
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session != null &&
        session.hasAnyPermission(const [
          FleetPermissions.gpsCommandSend,
          FleetPermissions.gpsCommandView,
        ]);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Device health'),
        actions: [
          if (canCommands)
            IconButton(
              tooltip: 'Commands',
              icon: const Icon(Icons.power_settings_new_outlined),
              onPressed: () => showVehicleCommandsSheet(context, vehicleId),
            ),
        ],
      ),
      body: gpsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('$e', textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: () =>
                      ref.invalidate(vehicleGpsInfoProvider(vehicleId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (info) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
          children: [
            const SgSectionTitle('Tracker'),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                children: [
                  _row('Device', info.deviceName ?? info.uniqueId ?? '—'),
                  _row('IMEI / Unique ID', info.uniqueId ?? '—'),
                  _row('Model', info.modelName ?? info.brandName ?? '—'),
                  _row('Status', info.gpsOnline ? 'Online' : 'Offline'),
                  _row(
                    'Last seen',
                    info.lastSeenAt?.toLocal().toString() ??
                        info.lastUpdate?.toLocal().toString() ??
                        '—',
                  ),
                  _row(
                    'Battery',
                    info.batteryLevel != null
                        ? '${info.batteryLevel!.toStringAsFixed(0)}%'
                        : '—',
                  ),
                  _row('GSM signal', '${info.gsmSignal ?? '—'}'),
                  _row(
                    'Speed',
                    '${info.speed?.toStringAsFixed(0) ?? '—'} km/h',
                  ),
                  _row(
                    'Ignition',
                    info.lastIgnition == null
                        ? '—'
                        : (info.lastIgnition! ? 'On' : 'Off'),
                  ),
                  _row(
                    'Odometer',
                    info.totalDistanceKm != null
                        ? '${info.totalDistanceKm!.toStringAsFixed(1)} km'
                        : '—',
                  ),
                  _row('Address', info.address ?? '—'),
                ],
              ),
            ),
            const SizedBox(height: 16),
            if (canCommands)
              FilledButton.icon(
                onPressed: () => showVehicleCommandsSheet(context, vehicleId),
                icon: const Icon(Icons.terminal_rounded),
                label: const Text('Device commands'),
              ),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: Text(label,
                style: const TextStyle(color: AppColors.textSecondary)),
          ),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
