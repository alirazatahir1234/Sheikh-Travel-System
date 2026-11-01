import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';

class PlaybackSummaryCard extends StatelessWidget {
  const PlaybackSummaryCard({
    super.key,
    required this.bundle,
    this.selectedTrip,
    this.stops = const [],
  });

  final HistoryReplayBundle bundle;
  final GpsTrip? selectedTrip;
  final List<TripStop> stops;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final metrics = PlaybackTripMetrics.resolve(
      bundle: bundle,
      selectedTrip: selectedTrip,
      stops: stops.isNotEmpty ? stops : bundle.stops,
    );
    final stats = PlaybackStats.fromBundle(bundle);
    final parkingCount = (stops.isNotEmpty ? stops : bundle.stops)
        .where((s) => s.durationMinutes >= 120)
        .length;
    final fuel = bundle.summary?.fuelLiters ?? bundle.statistics?.fuelLiters;
    final engineHours =
        bundle.summary?.engineHours ?? bundle.statistics?.engineHours;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        border: Border.all(color: p.border),
        boxShadow: AppShadows.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            metrics.timeRangeLabel(),
            style: TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 13,
              color: p.textPrimary,
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _primaryCell(context, 'Distance', metrics.distanceLabel),
              ),
              Expanded(
                child: _primaryCell(context, 'Moving', metrics.movingLabel),
              ),
              Expanded(
                child: _primaryCell(context, 'Total', metrics.totalLabel),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _secondaryCell(
                  context,
                  'Idle',
                  metrics.idleLabel,
                  valueColor:
                      nonMovingExceedsAlertThreshold(metrics.idleMinutes)
                          ? AppColors.error
                          : null,
                ),
              ),
              Expanded(
                child: _secondaryCell(
                  context,
                  'Stops',
                  metrics.stopCountLabel,
                ),
              ),
              Expanded(
                child: _secondaryCell(
                  context,
                  'Parking',
                  parkingCount > 0 ? '$parkingCount' : '—',
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _secondaryCell(
                  context,
                  'Avg speed',
                  metrics.avgSpeedLabel,
                ),
              ),
              Expanded(
                child: _secondaryCell(
                  context,
                  'Max speed',
                  metrics.maxSpeedLabel,
                ),
              ),
              if (fuel != null)
                Expanded(
                  child: _secondaryCell(
                    context,
                    'Fuel',
                    '${fuel.toStringAsFixed(1)} L',
                  ),
                )
              else if (engineHours != null)
                Expanded(
                  child: _secondaryCell(
                    context,
                    'Engine',
                    '${engineHours.toStringAsFixed(1)} h',
                  ),
                )
              else
                const Expanded(child: SizedBox.shrink()),
            ],
          ),
          if (stats.overspeedCount > 0 ||
              (bundle.statistics?.harshBrakeCount ?? 0) > 0 ||
              (bundle.statistics?.harshAccelCount ?? 0) > 0) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                if (stats.overspeedCount > 0)
                  Expanded(
                    child: _secondaryCell(
                      context,
                      'Overspeed',
                      '${stats.overspeedCount}',
                    ),
                  ),
                if ((bundle.statistics?.harshBrakeCount ?? 0) > 0)
                  Expanded(
                    child: _secondaryCell(
                      context,
                      'Harsh brake',
                      '${bundle.statistics!.harshBrakeCount}',
                    ),
                  ),
                if ((bundle.statistics?.harshAccelCount ?? 0) > 0)
                  Expanded(
                    child: _secondaryCell(
                      context,
                      'Harsh accel',
                      '${bundle.statistics!.harshAccelCount}',
                    ),
                  ),
              ],
            ),
          ],
          const SizedBox(height: 10),
          const Divider(height: 1),
          const SizedBox(height: 8),
          _windowCell(
            context,
            'Start',
            formatPlaybackDateTime(metrics.startTime),
          ),
          _windowCell(
            context,
            'End',
            formatPlaybackDateTime(metrics.endTime),
          ),
          if ((metrics.startAddress ?? '').trim().isNotEmpty)
            _windowCell(context, 'From', metrics.startAddress!.trim()),
          if ((metrics.endAddress ?? '').trim().isNotEmpty)
            _windowCell(context, 'To', metrics.endAddress!.trim()),
        ],
      ),
    );
  }

  Widget _primaryCell(BuildContext context, String label, String value) {
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
          style: TextStyle(
            fontSize: 11,
            color: context.sg.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }

  Widget _secondaryCell(
    BuildContext context,
    String label,
    String value, {
    Color? valueColor,
  }) {
    final p = context.sg;
    return Column(
      children: [
        Text(
          value,
          textAlign: TextAlign.center,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 12,
            height: 1.25,
            color: valueColor ?? p.textPrimary,
          ),
        ),
        Text(
          label,
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 10, color: p.textMuted),
        ),
      ],
    );
  }

  Widget _windowCell(BuildContext context, String label, String value) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '$label: ',
            style: TextStyle(
              color: p.textMuted,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
          Flexible(
            child: Text(
              value,
              style: TextStyle(
                color: p.textPrimary,
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
