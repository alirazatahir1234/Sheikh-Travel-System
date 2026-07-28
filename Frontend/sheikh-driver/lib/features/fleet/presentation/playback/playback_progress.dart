import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';

class PlaybackProgress extends StatelessWidget {
  const PlaybackProgress({
    super.key,
    required this.playback,
    required this.index,
    required this.isPlaying,
  });

  final List<HistoryReplayPoint> playback;
  final int index;
  final bool isPlaying;

  @override
  Widget build(BuildContext context) {
    if (playback.isEmpty) return const SizedBox.shrink();
    final safe = index.clamp(0, playback.length - 1);
    final current = playback[safe].timestamp.toLocal();
    final end = playback.last.timestamp.toLocal();
    final df = DateFormat('HH:mm');
    final percent = playback.length <= 1
        ? 100
        : ((safe / (playback.length - 1)) * 100).round();
    final status = safe >= playback.length - 1
        ? 'Finished'
        : isPlaying
            ? 'Playing'
            : 'Paused';
    return Row(
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadii.pill),
            border: Border.all(color: AppColors.border),
          ),
          child: Text(
            status,
            style: const TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            '${df.format(current)} / ${df.format(end)}',
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
        ),
        Text(
          '$percent%',
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: AppColors.primary,
          ),
        ),
      ],
    );
  }
}
