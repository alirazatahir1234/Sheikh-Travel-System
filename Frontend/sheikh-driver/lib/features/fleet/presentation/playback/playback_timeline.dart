import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';

class PlaybackTimeline extends StatelessWidget {
  const PlaybackTimeline({
    super.key,
    required this.playback,
    required this.stops,
    required this.events,
    required this.index,
    required this.onIndexChanged,
    this.virtualTime,
    this.onVirtualTimeChanged,
    this.onScrubStart,
    this.onScrubEnd,
    this.filteredEvents = const [],
    this.showEventJumpChips = false,
  });

  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final List<TripEvent> filteredEvents;
  final int index;

  /// Wall-clock playback cursor. When set, elapsed / slider use this instead of
  /// the discrete [index] timestamp (so progress moves during long segments).
  final DateTime? virtualTime;
  final ValueChanged<int> onIndexChanged;
  final ValueChanged<DateTime>? onVirtualTimeChanged;
  final VoidCallback? onScrubStart;
  final VoidCallback? onScrubEnd;
  final bool showEventJumpChips;

  @override
  Widget build(BuildContext context) {
    if (playback.isEmpty) return const SizedBox.shrink();
    final p = context.sg;
    final max = playback.length - 1;
    final displayEvents =
        filteredEvents.isNotEmpty ? filteredEvents : events;
    final markerIndices = showEventJumpChips
        ? timelineMarkerIndices(
            playback: playback,
            stops: stops,
            events: displayEvents,
          )
        : const <int>[];
    final clampedIndex = index.clamp(0, max);
    final firstTs = playback.first.timestamp;
    final lastTs = playback.last.timestamp;
    final clock = virtualTime ?? playback[clampedIndex].timestamp;
    final elapsed = clock.difference(firstTs);
    final total = lastTs.difference(firstTs);
    final totalMs = total.inMilliseconds;
    final elapsedMs =
        elapsed.inMilliseconds.clamp(0, totalMs < 0 ? 0 : totalMs);
    final progress =
        totalMs <= 0 ? 0 : ((elapsedMs / totalMs) * 100).round();
    final sliderValue = max <= 0 || totalMs <= 0
        ? 0.0
        : (elapsedMs / totalMs) * max;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Text(
              '${_formatElapsed(elapsed)} / ${_formatElapsed(total)}',
              style: TextStyle(
                fontSize: 11,
                color: p.textSecondary,
                fontWeight: FontWeight.w700,
              ),
            ),
            const Spacer(),
            Text(
              formatPlaybackClock(clock),
              style: TextStyle(
                fontSize: 11,
                color: p.textPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(width: 8),
            Text(
              '$progress%',
              style: TextStyle(
                fontSize: 10,
                color: p.textMuted,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
        const SizedBox(height: 2),
        Row(
          children: [
            Text(
              formatPlaybackClock(firstTs),
              style: TextStyle(fontSize: 10, color: p.textMuted),
            ),
            const Spacer(),
            Text(
              formatPlaybackClock(lastTs),
              style: TextStyle(fontSize: 10, color: p.textMuted),
            ),
          ],
        ),
        SliderTheme(
          data: SliderTheme.of(context).copyWith(
            trackHeight: 6,
            activeTrackColor: AppColors.primary,
            inactiveTrackColor: p.border,
            thumbColor: AppColors.navy,
            overlayColor: AppColors.primary.withValues(alpha: 0.15),
            thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 11),
            overlayShape: const RoundSliderOverlayShape(overlayRadius: 18),
          ),
          child: Slider(
            value: sliderValue.clamp(0, max.toDouble()),
            min: 0,
            max: max.toDouble(),
            onChangeStart: (_) => onScrubStart?.call(),
            onChanged: (v) {
              if (onVirtualTimeChanged != null && totalMs > 0) {
                final fraction = max <= 0 ? 0.0 : (v / max).clamp(0.0, 1.0);
                final seek = firstTs.add(
                  Duration(milliseconds: (totalMs * fraction).round()),
                );
                onVirtualTimeChanged!(seek);
              } else {
                onIndexChanged(v.round());
              }
            },
            onChangeEnd: (_) => onScrubEnd?.call(),
          ),
        ),
        if (markerIndices.isNotEmpty)
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final mi in markerIndices)
                OutlinedButton.icon(
                  onPressed: () => onIndexChanged(mi),
                  icon: Icon(
                    _iconForIndex(mi, displayEvents, stops, playback),
                    size: 14,
                    color: AppColors.warning,
                  ),
                  label: Text(formatPlaybackClock(playback[mi].timestamp)),
                  style: OutlinedButton.styleFrom(
                    visualDensity: VisualDensity.compact,
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                  ),
                ),
            ],
          ),
      ],
    );
  }

  IconData _iconForIndex(
    int idx,
    List<TripEvent> events,
    List<TripStop> stops,
    List<HistoryReplayPoint> playback,
  ) {
    for (final e in events) {
      if (indexForTimestamp(playback, e.time) == idx) {
        final t = e.type.toLowerCase();
        if (t.contains('sos') || t.contains('panic')) return Icons.emergency;
        if (t.contains('overspeed') || t.contains('speed')) return Icons.speed;
        if (t.contains('geofence')) return Icons.fence;
        if (t.contains('fuel')) return Icons.local_gas_station_outlined;
        return Icons.warning_amber_rounded;
      }
    }
    for (final s in stops) {
      if (indexForTimestamp(playback, s.startTime) == idx) {
        return Icons.stop_circle_outlined;
      }
    }
    return Icons.circle;
  }

  String _formatElapsed(Duration d) {
    if (d.isNegative) return '00:00';
    final totalSec = d.inSeconds;
    final h = totalSec ~/ 3600;
    final m = (totalSec % 3600) ~/ 60;
    final s = totalSec % 60;
    if (h > 0) {
      return '${h.toString().padLeft(2, '0')}:'
          '${m.toString().padLeft(2, '0')}:'
          '${s.toString().padLeft(2, '0')}';
    }
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }
}
