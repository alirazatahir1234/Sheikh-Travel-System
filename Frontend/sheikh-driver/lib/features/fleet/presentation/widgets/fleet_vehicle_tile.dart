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

  bool get _isOffline =>
      vehicle.status == FleetTrackStatus.offline ||
      vehicle.status == FleetTrackStatus.neverSeen ||
      !hasValidFleetCoords(vehicle.latitude, vehicle.longitude);

  @override
  Widget build(BuildContext context) {
    final badgeLabel = _badgeLabel(vehicle);
    final badgeColor = _isOffline
        ? (vehicle.status == FleetTrackStatus.neverSeen
            ? fleetStatusColor(FleetTrackStatus.neverSeen)
            : fleetStatusColor(FleetTrackStatus.offline))
        : fleetStatusColor(vehicle.status);

    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      onTap: () => context.push('/fleet/vehicles/${vehicle.vehicleId}'),
      child: _isOffline
          ? _CompactOfflineCard(
              vehicle: vehicle,
              badgeLabel: badgeLabel,
              badgeColor: badgeColor,
            )
          : _OnlineCard(
              vehicle: vehicle,
              badgeLabel: badgeLabel,
              badgeColor: badgeColor,
            ),
    );
  }

  static String _badgeLabel(FleetVehicleLocation v) {
    if (v.status == FleetTrackStatus.neverSeen) return 'Unknown';
    if (v.status == FleetTrackStatus.offline ||
        !hasValidFleetCoords(v.latitude, v.longitude)) {
      return 'Offline';
    }
    return v.status.label;
  }
}

class _CompactOfflineCard extends StatelessWidget {
  const _CompactOfflineCard({
    required this.vehicle,
    required this.badgeLabel,
    required this.badgeColor,
  });

  final FleetVehicleLocation vehicle;
  final String badgeLabel;
  final Color badgeColor;

  @override
  Widget build(BuildContext context) {
    final lastSeen = formatRelativeAge(vehicle.lastUpdated);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.navigation_rounded, color: badgeColor, size: 20),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    vehicle.vehicleName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  Text(
                    vehicle.registrationNumber,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            StatusBadge(badgeLabel, color: badgeColor),
          ],
        ),
        const SizedBox(height: 8),
        Text(
          'Driver: ${(vehicle.driverName ?? '').trim().isEmpty ? 'Not assigned' : vehicle.driverName!}',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          'Last seen: $lastSeen',
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: Text(
                '⚠ Not reporting recently',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: badgeColor.withValues(alpha: 0.95),
                ),
              ),
            ),
            const Text(
              'View →',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.primary,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _OnlineCard extends StatelessWidget {
  const _OnlineCard({
    required this.vehicle,
    required this.badgeLabel,
    required this.badgeColor,
  });

  final FleetVehicleLocation vehicle;
  final String badgeLabel;
  final Color badgeColor;

  @override
  Widget build(BuildContext context) {
    final metrics = <_MetricItem>[
      _MetricItem(
        'Speed',
        formatDisplaySpeedLabel(
          speed: vehicle.speed,
          ignition: vehicle.ignition,
          status: vehicle.status,
          latitude: vehicle.latitude,
          longitude: vehicle.longitude,
        ),
      ),
      _MetricItem('GPS', _gpsStatusLabel(vehicle)),
      _MetricItem('Last GPS update', formatRelativeAge(vehicle.lastUpdated)),
      _MetricItem(
        'Ignition',
        vehicle.ignition == true
            ? 'ON'
            : vehicle.ignition == false
                ? 'OFF'
                : 'Not available',
      ),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 38,
              height: 38,
              decoration: BoxDecoration(
                color: badgeColor.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(AppRadii.sm),
              ),
              child: Icon(Icons.navigation_rounded, color: badgeColor, size: 20),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    vehicle.vehicleName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    vehicle.registrationNumber,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'Driver: ${(vehicle.driverName ?? '').trim().isEmpty ? 'Not assigned' : vehicle.driverName!}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 13,
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
            StatusBadge(badgeLabel, color: badgeColor),
          ],
        ),
        const SizedBox(height: 10),
        const Divider(height: 1),
        const SizedBox(height: 10),
        _MetricGrid(items: metrics),
        if ((vehicle.address ?? '').trim().isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(
            vehicle.address!,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 12,
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
        const SizedBox(height: 8),
        const Align(
          alignment: Alignment.centerRight,
          child: Text(
            'View →',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: AppColors.primary,
            ),
          ),
        ),
      ],
    );
  }

  static String _gpsStatusLabel(FleetVehicleLocation v) {
    if (!hasValidFleetCoords(v.latitude, v.longitude)) return 'No GPS data';
    if (v.lastUpdated == null) return 'Position available';
    final sec = DateTime.now().difference(v.lastUpdated!).inSeconds;
    if (sec <= 120) return 'Live';
    return 'Position available';
  }
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.items});

  final List<_MetricItem> items;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < items.length; i += 2) {
      final left = items[i];
      final right = i + 1 < items.length ? items[i + 1] : null;
      rows.add(
        Padding(
          padding: EdgeInsets.only(bottom: i + 2 < items.length ? 10 : 0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: _VehicleMetric(label: left.label, value: left.value)),
              const SizedBox(width: 16),
              Expanded(
                child: right == null
                    ? const SizedBox.shrink()
                    : _VehicleMetric(label: right.label, value: right.value),
              ),
            ],
          ),
        ),
      );
    }
    return Column(children: rows);
  }
}

class _VehicleMetric extends StatelessWidget {
  const _VehicleMetric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontSize: 11,
            color: AppColors.textMuted,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 3),
        Text(
          value,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontSize: 13,
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w700,
            height: 1.2,
          ),
        ),
      ],
    );
  }
}

class _MetricItem {
  const _MetricItem(this.label, this.value);

  final String label;
  final String value;
}
