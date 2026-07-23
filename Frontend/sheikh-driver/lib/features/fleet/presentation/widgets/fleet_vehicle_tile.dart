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
}
