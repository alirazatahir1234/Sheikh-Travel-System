import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import 'playback_map_builder.dart';

class PlaybackLegend extends StatelessWidget {
  const PlaybackLegend({super.key});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        child: Wrap(
          spacing: 8,
          runSpacing: 6,
          children: [
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.vehicle), label: 'Vehicle'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.start), label: 'Start'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.finish), label: 'Finish'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.stop), label: 'Stop'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.overspeed), label: 'Overspeed'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.geofence), label: 'Geofence'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.fuel), label: 'Fuel'),
            _LegendItem(color: PlaybackMapAssets.colorForKind(PlaybackMarkerKind.sos), label: 'SOS'),
          ],
        ),
      ),
    );
  }
}

class _LegendItem extends StatelessWidget {
  const _LegendItem({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 10,
          height: 10,
          decoration: BoxDecoration(
            color: color,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: 4),
        Text(
          label,
          style: const TextStyle(fontSize: 11, color: AppColors.textSecondary),
        ),
      ],
    );
  }
}
