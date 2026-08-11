import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/fleet_models.dart';
import 'package:intl/intl.dart';
import 'playback_helpers.dart';

class PlaybackSummaryCard extends StatelessWidget {
  const PlaybackSummaryCard({super.key, required this.bundle});

  final HistoryReplayBundle bundle;

  @override
  Widget build(BuildContext context) {
    final stats = bundle.statistics;
    final summary = bundle.summary;
    final dist =
        bundle.mileageKm ?? stats?.distanceKm ?? summary?.distanceKm ?? 0;
    final driveMin = effectiveDrivingMinutes(bundle);
    final idle = stats?.idleMinutes ?? 0;
    final parkingCount =
        bundle.stops.where((s) => s.durationMinutes >= 120).length;
    final avg = stats?.avgSpeedKmh ?? summary?.avgSpeedKmh ?? 0;
    final max = stats?.maxSpeedKmh ?? summary?.maxSpeedKmh ?? 0;
    final stops =
        bundle.stops.isNotEmpty ? bundle.stops.length : stats?.stopCount ?? 0;
    final points =
        bundle.points.isNotEmpty ? bundle.points : bundle.trailPoints;
    final tf = DateFormat('dd MMM, HH:mm');
    final tripDurationMin = points.length >= 2
        ? points.last.timestamp.difference(points.first.timestamp).inMinutes
        : driveMin + idle;
    final fuel = summary?.fuelLiters ?? stats?.fuelLiters;
    final engineHours = summary?.engineHours ?? stats?.engineHours;
    final overspeed = stats?.overspeedCount ?? 0;
    final harshBrake = stats?.harshBrakeCount ?? 0;
    final harshAccel = stats?.harshAccelCount ?? 0;

    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: _primaryCell('Distance', formatDistanceKm(dist)),
              ),
              Expanded(
                child: _primaryCell('Driving', formatDurationMinutes(driveMin)),
              ),
              Expanded(
                child: _primaryCell(
                    'Trip', formatDurationMinutes(tripDurationMin)),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _secondaryCell('Non-moving', formatDurationMinutes(idle)),
              ),
              Expanded(
                child: _secondaryCell('Parking', '$parkingCount'),
              ),
              Expanded(
                child: _secondaryCell('Stops', '$stops'),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _secondaryCell(
                  'Avg speed',
                  '${avg.toStringAsFixed(0)} km/h',
                ),
              ),
              Expanded(
                child: _secondaryCell(
                  'Max speed',
                  '${max.toStringAsFixed(0)} km/h',
                ),
              ),
              if (fuel != null)
                Expanded(
                  child: _secondaryCell(
                    'Fuel',
                    '${fuel.toStringAsFixed(1)} L',
                  ),
                )
              else if (engineHours != null)
                Expanded(
                  child: _secondaryCell(
                    'Engine',
                    '${engineHours.toStringAsFixed(1)} h',
                  ),
                )
              else
                const Expanded(child: SizedBox.shrink()),
            ],
          ),
          if (overspeed > 0 || harshBrake > 0 || harshAccel > 0) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                if (overspeed > 0)
                  Expanded(child: _secondaryCell('Overspeed', '$overspeed')),
                if (harshBrake > 0)
                  Expanded(child: _secondaryCell('Harsh brake', '$harshBrake')),
                if (harshAccel > 0)
                  Expanded(child: _secondaryCell('Harsh accel', '$harshAccel')),
              ],
            ),
          ],
          if (points.isNotEmpty) ...[
            const SizedBox(height: 10),
            const Divider(height: 1),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: _windowCell(
                    'Start',
                    tf.format(points.first.timestamp.toLocal()),
                  ),
                ),
                Expanded(
                  child: _windowCell(
                    'End',
                    tf.format(points.last.timestamp.toLocal()),
                  ),
                ),
              ],
            ),
            if ((points.first.address ?? '').trim().isNotEmpty ||
                (points.last.address ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 6),
              if ((points.first.address ?? '').trim().isNotEmpty)
                _windowCell('From', points.first.address!.trim()),
              if ((points.last.address ?? '').trim().isNotEmpty)
                _windowCell('To', points.last.address!.trim()),
            ],
          ],
        ],
      ),
    );
  }

  Widget _primaryCell(String label, String value) {
    return Column(
      children: [
        Text(
          value,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 15,
            color: AppColors.primary,
          ),
        ),
        Text(
          label,
          style: const TextStyle(
            fontSize: 11,
            color: AppColors.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }

  Widget _secondaryCell(String label, String value) {
    return Column(
      children: [
        Text(
          value,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 13,
            color: AppColors.textPrimary,
          ),
        ),
        Text(
          label,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 10, color: AppColors.textMuted),
        ),
      ],
    );
  }

  Widget _windowCell(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: Row(
        children: [
          Text(
            '$label: ',
            style: const TextStyle(
              color: AppColors.textMuted,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
          Flexible(
            child: Text(
              value,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 11,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
