import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';

class PlaybackControls extends StatelessWidget {
  const PlaybackControls({
    super.key,
    required this.playing,
    required this.speed,
    required this.onPlayPause,
    required this.onFirst,
    required this.onPrevEvent,
    required this.onNextEvent,
    required this.onEnd,
    required this.onSpeed,
  });

  final bool playing;
  final double speed;
  final VoidCallback onPlayPause;
  final VoidCallback onFirst;
  final VoidCallback onPrevEvent;
  final VoidCallback onNextEvent;
  final VoidCallback onEnd;
  final ValueChanged<double> onSpeed;

  static const speeds = <double>[0.5, 1, 2, 4, 8];

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            IconButton(
              tooltip: 'First',
              onPressed: onFirst,
              icon: const Icon(Icons.skip_previous_rounded),
            ),
            IconButton(
              tooltip: 'Previous event',
              onPressed: onPrevEvent,
              icon: const Icon(Icons.fast_rewind_rounded),
            ),
            IconButton.filledTonal(
              tooltip: playing ? 'Pause' : 'Play',
              onPressed: onPlayPause,
              icon: Icon(playing ? Icons.pause : Icons.play_arrow),
            ),
            IconButton(
              tooltip: 'Next event',
              onPressed: onNextEvent,
              icon: const Icon(Icons.fast_forward_rounded),
            ),
            IconButton(
              tooltip: 'End',
              onPressed: onEnd,
              icon: const Icon(Icons.skip_next_rounded),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Text(
              'Speed',
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(width: 8),
            DropdownButton<double>(
              value: speed,
              onChanged: (v) {
                if (v != null) onSpeed(v);
              },
              items: speeds
                  .map(
                    (s) => DropdownMenuItem<double>(
                      value: s,
                      child: Text('${s}x'),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ],
    );
  }
}
