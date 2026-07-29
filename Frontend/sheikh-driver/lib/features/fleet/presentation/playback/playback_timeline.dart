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
    this.filteredEvents = const [],
  });

  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final List<TripEvent> filteredEvents;
  final int index;
  final ValueChanged<int> onIndexChanged;

  @override
  Widget build(BuildContext context) {
    if (playback.isEmpty) return const SizedBox.shrink();
    final max = playback.length - 1;
    final displayEvents =
        filteredEvents.isNotEmpty ? filteredEvents : events;
    final markerIndices = timelineMarkerIndices(
      playback: playback,
      stops: stops,
      events: displayEvents,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Text(
              _shortTime(playback.first.timestamp),
              style: const TextStyle(fontSize: 10, color: AppColors.textMuted),
            ),
            const Spacer(),
            Text(
              _shortTime(playback.last.timestamp),
              style: const TextStyle(fontSize: 10, color: AppColors.textMuted),
            ),
          ],
        ),
        SliderTheme(
          data: SliderTheme.of(context).copyWith(
            trackHeight: 6,
            thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 12),
            overlayShape: const RoundSliderOverlayShape(overlayRadius: 18),
          ),
          child: Slider(
            value: index.toDouble().clamp(0, max.toDouble()),
            min: 0,
            max: max.toDouble(),
            onChanged: (v) => onIndexChanged(v.round()),
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
                  label: Text(_shortTime(playback[mi].timestamp)),
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

  String _shortTime(DateTime dt) {
    final l = dt.toLocal();
    return '${l.hour.toString().padLeft(2, '0')}:${l.minute.toString().padLeft(2, '0')}';
  }
}
