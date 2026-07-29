import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/fleet_models.dart';
import 'package:intl/intl.dart';

class PlaybackSummaryCard extends StatelessWidget {
  const PlaybackSummaryCard({super.key, required this.bundle});

  final HistoryReplayBundle bundle;

  @override
  Widget build(BuildContext context) {
    final stats = bundle.statistics;
    final summary = bundle.summary;
    final dist = bundle.mileageKm ??
        stats?.distanceKm ??
        summary?.distanceKm ??
        0;
    final driveMin = stats?.drivingMinutes ?? summary?.drivingMinutes ?? 0;
    final idle = stats?.idleMinutes ?? 0;
    final avg = stats?.avgSpeedKmh ?? summary?.avgSpeedKmh ?? 0;
    final max = stats?.maxSpeedKmh ?? summary?.maxSpeedKmh ?? 0;
    final stops = bundle.stops.isNotEmpty
        ? bundle.stops.length
        : stats?.stopCount ?? 0;
    final points = bundle.points.isNotEmpty ? bundle.points : bundle.trailPoints;
    final tf = DateFormat('dd MMM, HH:mm');

    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: _primaryCell('Distance', '${dist.toStringAsFixed(1)} km'),
              ),
              Expanded(
                child: _primaryCell('Driving time', _fmtDur(driveMin)),
              ),
              if (idle > 0)
                Expanded(
                  child: _primaryCell('Idle time', _fmtDur(idle)),
                ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _secondaryCell(
                  'Average speed',
                  '${avg.toStringAsFixed(0)} km/h',
                ),
              ),
              Expanded(
                child: _secondaryCell(
                  'Maximum speed',
                  '${max.toStringAsFixed(0)} km/h',
                ),
              ),
              Expanded(
                child: _secondaryCell('Stops', '$stops'),
              ),
            ],
          ),
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
          ],
        ],
      ),
    );
  }

  String _fmtDur(int minutes) {
    if (minutes < 60) return '${minutes}m';
    return '${minutes ~/ 60}h ${minutes % 60}m';
  }

  Widget _primaryCell(String label, String value) {
    return Column(
      children: [
        Text(
          value,
          style: const TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 16,
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
    return Row(
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
    );
  }
}
