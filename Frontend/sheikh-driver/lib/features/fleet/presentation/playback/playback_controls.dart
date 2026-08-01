import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';

class PlaybackControls extends StatelessWidget {
  const PlaybackControls({
    super.key,
    required this.playing,
    required this.speed,
    required this.atEnd,
    required this.onPlayPause,
    required this.onStop,
    required this.onFirst,
    required this.onPrevPoint,
    required this.onNextPoint,
    required this.onPrevEvent,
    required this.onNextEvent,
    required this.onEnd,
    required this.onSpeed,
  });

  final bool playing;
  final double speed;
  final bool atEnd;
  final VoidCallback onPlayPause;
  final VoidCallback onStop;
  final VoidCallback onFirst;
  final VoidCallback onPrevPoint;
  final VoidCallback onNextPoint;
  final VoidCallback onPrevEvent;
  final VoidCallback onNextEvent;
  final VoidCallback onEnd;
  final ValueChanged<double> onSpeed;

  static const speeds = <double>[0.5, 1, 2, 4, 8, 16];

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'First',
                onPressed: onFirst,
                icon: const Icon(Icons.skip_previous_rounded),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'Previous point',
                onPressed: onPrevPoint,
                icon: const Icon(Icons.chevron_left_rounded),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'Previous event',
                onPressed: onPrevEvent,
                icon: const Icon(Icons.fast_rewind_rounded),
              ),
              IconButton.filledTonal(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 44, minHeight: 44),
                tooltip: playing
                    ? 'Pause'
                    : (atEnd ? 'Replay from start' : 'Play'),
                onPressed: onPlayPause,
                icon: Icon(
                  playing
                      ? Icons.pause
                      : (atEnd ? Icons.replay_rounded : Icons.play_arrow),
                ),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'Stop',
                onPressed: onStop,
                icon: const Icon(Icons.stop_rounded),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'Next event',
                onPressed: onNextEvent,
                icon: const Icon(Icons.fast_forward_rounded),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'Next point',
                onPressed: onNextPoint,
                icon: const Icon(Icons.chevron_right_rounded),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 36, minHeight: 40),
                tooltip: 'End',
                onPressed: onEnd,
                icon: const Icon(Icons.skip_next_rounded),
              ),
            ],
          ),
        ),
        PopupMenuButton<double>(
          tooltip: 'Playback speed',
          initialValue: speed,
          onSelected: onSpeed,
          itemBuilder: (_) => speeds
              .map(
                (s) => PopupMenuItem<double>(
                  value: s,
                  child: Text('${s}x'),
                ),
              )
              .toList(),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '${speed}x',
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                const Icon(Icons.speed, size: 18, color: AppColors.textSecondary),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
