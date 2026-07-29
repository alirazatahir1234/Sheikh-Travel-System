import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/fleet_models.dart';
import 'fleet_kpi_strip.dart';

class FleetVehicleTile extends StatelessWidget {
  const FleetVehicleTile({super.key, required this.vehicle});

  final FleetVehicleLocation vehicle;

  @override
  Widget build(BuildContext context) {
    final color = fleetStatusColor(vehicle.status);
    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      onTap: () => context.push('/fleet/vehicles/${vehicle.vehicleId}'),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(AppRadii.sm),
            ),
            child: Icon(Icons.directions_car_filled_rounded, color: color),
          ),
          const SizedBox(width: 12),
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
                if (vehicle.driverName != null &&
                    vehicle.driverName!.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    vehicle.driverName!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textMuted,
                    ),
                  ),
                ],
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    _meta(
                      '${vehicle.speed.toStringAsFixed(0)} km/h',
                      Icons.speed,
                    ),
                    _meta(
                      vehicle.ignition == true ? 'IGN on' : 'IGN off',
                      Icons.power_settings_new,
                    ),
                    _meta(
                      vehicle.hasGps && vehicle.status.label.toLowerCase() != 'offline'
                          ? 'GPS'
                          : 'No GPS',
                      Icons.satellite_alt_outlined,
                      online: vehicle.hasGps &&
                          vehicle.status.label.toLowerCase() != 'offline',
                    ),
                    if (vehicle.batteryLevel != null)
                      _meta(
                        '${vehicle.batteryLevel!.toStringAsFixed(0)}%',
                        Icons.battery_charging_full,
                      ),
                    if (vehicle.gsmSignal != null)
                      _meta('GSM ${vehicle.gsmSignal}', Icons.signal_cellular_alt),
                    if (vehicle.lastUpdated != null)
                      _meta(
                        _relTime(vehicle.lastUpdated!),
                        Icons.schedule,
                      ),
                  ],
                ),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              StatusBadge(vehicle.status.label, color: color),
              if (vehicle.speed > 0) ...[
                const SizedBox(height: 6),
                Text(
                  '${vehicle.speed.toStringAsFixed(0)} km/h',
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _meta(String text, IconData icon, {bool online = true}) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          icon,
          size: 12,
          color: online ? AppColors.textMuted : AppColors.error,
        ),
        const SizedBox(width: 2),
        Text(
          text,
          style: TextStyle(
            fontSize: 11,
            color: online ? AppColors.textMuted : AppColors.error,
          ),
        ),
      ],
    );
  }

  String _relTime(DateTime t) {
    final d = DateTime.now().difference(t);
    if (d.inMinutes < 1) return 'now';
    if (d.inMinutes < 60) return '${d.inMinutes}m';
    if (d.inHours < 24) return '${d.inHours}h';
    return '${d.inDays}d';
  }
}
