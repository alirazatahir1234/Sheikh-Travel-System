import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';

class PlaybackEventList extends StatelessWidget {
  const PlaybackEventList({
    super.key,
    required this.playback,
    required this.stops,
    required this.events,
    required this.onJump,
  });

  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final ValueChanged<int> onJump;

  @override
  Widget build(BuildContext context) {
    if (playback.isEmpty) return const SizedBox.shrink();
    final items = <_TimelineItem>[
      for (final s in stops)
        _TimelineItem(
          time: s.startTime,
          label: 'Stop (${s.durationMinutes}m)',
          icon: Icons.stop_circle_outlined,
        ),
      for (final e in events)
        _TimelineItem(
          time: e.time,
          label: _prettyType(e.type),
          icon: _iconForType(e.type),
        ),
    ]..sort((a, b) => a.time.compareTo(b.time));
    if (items.isEmpty) return const SizedBox.shrink();
    final tf = DateFormat('HH:mm');
    return SizedBox(
      height: 36,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: 6),
        itemBuilder: (_, i) {
          final item = items[i];
          return ActionChip(
            avatar: Icon(item.icon, size: 14, color: AppColors.warning),
            label: Text('${tf.format(item.time.toLocal())} ${item.label}'),
            onPressed: () => onJump(indexForTimestamp(playback, item.time)),
            labelStyle: const TextStyle(fontSize: 11),
          );
        },
      ),
    );
  }

  static IconData _iconForType(String type) {
    final t = type.toLowerCase();
    if (t.contains('sos') || t.contains('panic')) return Icons.emergency;
    if (t.contains('overspeed') || t.contains('speed')) return Icons.speed;
    if (t.contains('geofence')) return Icons.fence;
    if (t.contains('fuel')) return Icons.local_gas_station_outlined;
    if (t.contains('ignition')) return Icons.power_settings_new;
    return Icons.warning_amber_rounded;
  }

  static String _prettyType(String type) {
    return type
        .replaceAll('_', ' ')
        .split(' ')
        .where((e) => e.isNotEmpty)
        .map((e) => '${e[0].toUpperCase()}${e.substring(1).toLowerCase()}')
        .join(' ');
  }
}

class _TimelineItem {
  const _TimelineItem({
    required this.time,
    required this.label,
    required this.icon,
  });

  final DateTime time;
  final String label;
  final IconData icon;
}
