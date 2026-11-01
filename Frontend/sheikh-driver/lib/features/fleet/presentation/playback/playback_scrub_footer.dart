import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import 'playback_helpers.dart';

/// Live playback readout — current clock / speed / distance so far.
/// Distinct from trip max speed and total trip distance.
class PlaybackScrubFooter extends StatelessWidget {
  const PlaybackScrubFooter({
    super.key,
    required this.time,
    required this.speedKmh,
    required this.distanceKm,
    this.playbackSpeed,
  });

  final DateTime time;
  final double speedKmh;
  final double distanceKm;
  final double? playbackSpeed;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: p.chipBg,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: p.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: _cell(
              context,
              'Time',
              formatPlaybackClock(time),
            ),
          ),
          Expanded(
            child: _cell(
              context,
              'Current speed',
              formatOptionalSpeedKmh(speedKmh),
            ),
          ),
          Expanded(
            child: _cell(
              context,
              'Distance so far',
              '${distanceKm.toStringAsFixed(1)} km',
            ),
          ),
          if (playbackSpeed != null)
            Expanded(
              child: _cell(
                context,
                'Play rate',
                '${playbackSpeed}x',
              ),
            ),
        ],
      ),
    );
  }

  Widget _cell(BuildContext context, String label, String value) {
    final p = context.sg;
    return Column(
      children: [
        Text(
          label,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.w600,
            color: p.textMuted,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w800,
            color: p.textPrimary,
          ),
        ),
      ],
    );
  }
}
