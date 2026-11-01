import 'package:flutter/material.dart';

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
    this.vertical = false,
    this.maxItems,
  });

  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final ValueChanged<int> onJump;
  final bool vertical;
  final int? maxItems;

  @override
  Widget build(BuildContext context) {
    if (playback.isEmpty) return const SizedBox.shrink();
    final items = <_TimelineItem>[
      for (final s in stops)
        _TimelineItem(
          time: s.startTime,
          label: s.durationMinutes >= 120
              ? 'Parking (${formatDurationMinutesCompact(s.durationMinutes)})'
              : 'Stop (${formatDurationMinutesCompact(s.durationMinutes)})',
          detail: shortAddressOrCoords(s),
          icon: Icons.stop_circle_outlined,
          color: AppColors.warning,
        ),
      for (final e in events)
        _TimelineItem(
          time: e.time,
          label: _prettyType(e.type),
          detail: _eventDetail(e),
          icon: _iconForType(e.type),
          color: _colorForType(e.type),
        ),
    ]..sort((a, b) => a.time.compareTo(b.time));
    if (items.isEmpty) {
      return Text(
        'No events in this trip',
        style: TextStyle(
          fontSize: 12,
          color: context.sg.textMuted,
          fontWeight: FontWeight.w600,
        ),
      );
    }
    final visible =
        maxItems == null ? items : items.take(maxItems!).toList();

    if (!vertical) {
      return SizedBox(
        height: 36,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          itemCount: visible.length,
          separatorBuilder: (_, __) => const SizedBox(width: 6),
          itemBuilder: (_, i) {
            final item = visible[i];
            return ActionChip(
              avatar: Icon(item.icon, size: 14, color: item.color),
              label: Text(
                '${formatPlaybackClock(item.time)} ${item.label}',
              ),
              onPressed: () =>
                  onJump(indexForTimestamp(playback, item.time)),
              labelStyle: const TextStyle(fontSize: 11),
            );
          },
        ),
      );
    }

    final p = context.sg;
    return Column(
      children: [
        for (var i = 0; i < visible.length; i++) ...[
          if (i > 0) Divider(height: 1, color: p.divider),
          InkWell(
            onTap: () =>
                onJump(indexForTimestamp(playback, visible[i].time)),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 10,
                    height: 10,
                    margin: const EdgeInsets.only(top: 4),
                    decoration: BoxDecoration(
                      color: visible[i].color,
                      shape: BoxShape.circle,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          visible[i].label,
                          style: TextStyle(
                            fontWeight: FontWeight.w800,
                            fontSize: 13,
                            color: p.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          formatPlaybackDateTime(visible[i].time),
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w600,
                            color: p.textSecondary,
                          ),
                        ),
                        if ((visible[i].detail ?? '').isNotEmpty) ...[
                          const SizedBox(height: 2),
                          Text(
                            visible[i].detail!,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 11,
                              color: p.textMuted,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  Icon(Icons.play_circle_outline,
                      size: 20, color: p.textMuted),
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }

  static String? shortAddressOrCoords(TripStop s) {
    if (s.latitude != 0 || s.longitude != 0) {
      return '${s.latitude.toStringAsFixed(5)}, ${s.longitude.toStringAsFixed(5)}';
    }
    return null;
  }

  static String? _eventDetail(TripEvent e) {
    final parts = <String>[];
    if (e.speedKmh != null && e.speedKmh! > 0) {
      parts.add('${e.speedKmh!.toStringAsFixed(0)} km/h');
    }
    final addr = (e.address ?? '').trim();
    if (addr.isNotEmpty) {
      parts.add(addr);
    } else if (e.latitude != null && e.longitude != null) {
      parts.add(
        '${e.latitude!.toStringAsFixed(5)}, ${e.longitude!.toStringAsFixed(5)}',
      );
    }
    return parts.isEmpty ? null : parts.join(' · ');
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

  static Color _colorForType(String type) {
    final t = type.toLowerCase();
    if (t.contains('sos') || t.contains('panic')) return AppColors.error;
    if (t.contains('overspeed') || t.contains('speed')) return AppColors.warning;
    if (t.contains('fuel')) return const Color(0xFFD97706);
    if (t.contains('ignition')) return AppColors.info;
    if (t.contains('geofence')) return AppColors.primary;
    return AppColors.warning;
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
    required this.color,
    this.detail,
  });

  final DateTime time;
  final String label;
  final String? detail;
  final IconData icon;
  final Color color;
}
