import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/fleet_models.dart';
import '../../domain/gps_freshness.dart';
import 'fleet_kpi_strip.dart';

class FleetVehicleTile extends StatelessWidget {
  const FleetVehicleTile({super.key, required this.vehicle});

  final FleetVehicleLocation vehicle;

  @override
  Widget build(BuildContext context) {
    final color = fleetStatusColor(vehicle.status);
    final facts = <_FactItem>[
      _FactItem(
        'Speed',
        formatDisplaySpeedLabel(
          speed: vehicle.speed,
          ignition: vehicle.ignition,
          status: vehicle.status,
        ),
      ),
      _FactItem(
        'Battery',
        vehicle.batteryLevel != null
            ? '${vehicle.batteryLevel!.toStringAsFixed(0)}%'
            : '—',
      ),
      _FactItem(
        'GPS',
        formatGpsFreshness(
          latitude: vehicle.latitude,
          longitude: vehicle.longitude,
          lastUpdated: vehicle.lastUpdated,
        ),
      ),
      _FactItem(
        'Ignition',
        vehicle.ignition == true
            ? 'ON'
            : vehicle.ignition == false
                ? 'OFF'
                : '—',
      ),
      _FactItem('Signal', _signalLabel(vehicle.gsmSignal)),
    ];

    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      onTap: () => context.push('/fleet/vehicles/${vehicle.vehicleId}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                ),
                child: Icon(Icons.navigation_rounded, color: color, size: 20),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      vehicle.vehicleName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      vehicle.registrationNumber,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    const Text(
                      'Driver',
                      style: TextStyle(
                        fontSize: 11,
                        color: AppColors.textMuted,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Text(
                      (vehicle.driverName ?? '').trim().isEmpty
                          ? 'Unassigned'
                          : vehicle.driverName!,
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              StatusBadge(vehicle.status.label, color: color),
            ],
          ),
          const SizedBox(height: 10),
          const Divider(height: 1),
          const SizedBox(height: 10),
          LayoutBuilder(
            builder: (context, constraints) {
              final tileWidth = (constraints.maxWidth - 12) / 2;
              return Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  for (final fact in facts)
                    SizedBox(
                      width: tileWidth,
                      child: _factRow(fact.label, fact.value),
                    ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _factRow(String label, String value) {
    return Row(
      children: [
        Expanded(
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 11,
              color: AppColors.textMuted,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        Text(
          value,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }

  String _signalLabel(int? signal) {
    if (signal == null) return 'Unknown';
    if (signal >= 4) return 'Strong';
    if (signal >= 2) return 'Medium';
    if (signal >= 1) return 'Weak';
    return 'Offline';
  }
}

class _FactItem {
  const _FactItem(this.label, this.value);

  final String label;
  final String value;
}
