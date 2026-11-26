import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/presentation/vehicle_commands_sheet.dart';
import '../../fleet/presentation/vehicle_detail_screen.dart';
import '../../../core/time/tenant_time.dart';

class DeviceHealthScreen extends ConsumerWidget {
  const DeviceHealthScreen({super.key, required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final gpsAsync = ref.watch(vehicleGpsInfoProvider(vehicleId));
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session?.canUseGpsCommands ?? false;
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return Scaffold(
      backgroundColor: context.sg.surface,
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
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () => ref.invalidate(vehicleGpsInfoProvider(vehicleId)),
        ),
        data: (info) {
          final seen = info.lastSeenAt ?? info.lastUpdate;
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              const SgSectionTitle('Tracker'),
              const SizedBox(height: 8),
              SgCard(
                child: Column(
                  children: [
                    _row(context, 'Device', info.deviceName ?? info.uniqueId ?? '—'),
                    _row(context, 'IMEI / Unique ID', info.uniqueId ?? '—'),
                    _row(context, 'Model', info.modelName ?? info.brandName ?? '—'),
                    _row(context, 'Status', info.gpsOnline ? 'Online' : 'Offline'),
                    _row(
                      context,
                      'Last seen',
                      seen != null ? df.format(TenantTime.inZone(seen)) : '—',
                    ),
                    _row(
                      context,
                      'Battery',
                      info.batteryLevel != null
                          ? '${info.batteryLevel!.toStringAsFixed(0)}%'
                          : '—',
                    ),
                    _row(context, 'GSM signal', '${info.gsmSignal ?? '—'}'),
                    _row(
                      context,
                      'Speed',
                      '${info.speed?.toStringAsFixed(0) ?? '—'} km/h',
                    ),
                    _row(
                      context,
                      'Ignition',
                      info.lastIgnition == null
                          ? '—'
                          : (info.lastIgnition! ? 'On' : 'Off'),
                    ),
                    _row(
                      context,
                      'Odometer',
                      info.totalDistanceKm != null
                          ? '${info.totalDistanceKm!.toStringAsFixed(1)} km'
                          : '—',
                    ),
                    _row(context, 'Address', info.address ?? '—'),
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
          );
        },
      ),
    );
  }

  Widget _row(BuildContext context, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: Text(
              label,
              style: TextStyle(color: context.sg.textSecondary),
            ),
          ),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: context.sg.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
