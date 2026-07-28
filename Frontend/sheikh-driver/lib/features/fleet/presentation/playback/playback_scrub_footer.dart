import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';

class PlaybackScrubFooter extends StatelessWidget {
  const PlaybackScrubFooter({
    super.key,
    required this.time,
    required this.speedKmh,
    required this.distanceKm,
  });

  final DateTime time;
  final double speedKmh;
  final double distanceKm;

  @override
  Widget build(BuildContext context) {
    final tf = DateFormat('HH:mm');
    return Row(
      children: [
        Expanded(child: _cell('Time', tf.format(time.toLocal()))),
        Expanded(child: _cell('Speed', '${speedKmh.toStringAsFixed(0)} km/h')),
        Expanded(child: _cell('Distance', '${distanceKm.toStringAsFixed(1)} km')),
      ],
    );
  }

  Widget _cell(String label, String value) {
    return Column(
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.w600,
            color: AppColors.textMuted,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
      ],
    );
  }
}
