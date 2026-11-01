import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';

class PlaybackControls extends StatelessWidget {
  const PlaybackControls({
    super.key,
    required this.playing,
    required this.speed,
    required this.atEnd,
    required this.onPlayPause,
    required this.onPrevPoint,
    required this.onNextPoint,
    required this.onPrevEvent,
    required this.onNextEvent,
    required this.onSpeed,
    this.onStop,
    this.onFirst,
    this.onEnd,
    this.onPrevTrip,
    this.onNextTrip,
    this.canPrevTrip = false,
    this.canNextTrip = false,
    this.compact = false,
  });

  final bool playing;
  final double speed;
  final bool atEnd;
  final VoidCallback onPlayPause;
  final VoidCallback? onStop;
  final VoidCallback? onFirst;
  final VoidCallback onPrevPoint;
  final VoidCallback onNextPoint;
  final VoidCallback onPrevEvent;
  final VoidCallback onNextEvent;
  final VoidCallback? onEnd;
  final ValueChanged<double> onSpeed;
  final VoidCallback? onPrevTrip;
  final VoidCallback? onNextTrip;
  final bool canPrevTrip;
  final bool canNextTrip;
  final bool compact;

  static const speeds = <double>[0.5, 1, 2, 4, 8, 16];

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _CtrlBtn(
              tooltip: 'Previous trip',
              icon: Icons.skip_previous_rounded,
              onPressed: canPrevTrip ? onPrevTrip : null,
            ),
            _CtrlBtn(
              tooltip: 'Step back',
              icon: Icons.chevron_left_rounded,
              onPressed: onPrevPoint,
            ),
            _CtrlBtn(
              tooltip: 'Previous event',
              icon: Icons.fast_rewind_rounded,
              onPressed: onPrevEvent,
              compact: true,
            ),
            Semantics(
              button: true,
              label: playing ? 'Pause' : (atEnd ? 'Replay from start' : 'Play'),
              child: Material(
                color: AppColors.primary,
                shape: const CircleBorder(),
                elevation: 1,
                child: InkWell(
                  customBorder: const CircleBorder(),
                  onTap: onPlayPause,
                  onLongPress: onStop ?? onFirst,
                  child: SizedBox(
                    width: 52,
                    height: 52,
                    child: Icon(
                      playing
                          ? Icons.pause_rounded
                          : (atEnd
                              ? Icons.replay_rounded
                              : Icons.play_arrow_rounded),
                      color: Colors.white,
                      size: 28,
                    ),
                  ),
                ),
              ),
            ),
            _CtrlBtn(
              tooltip: 'Next event',
              icon: Icons.fast_forward_rounded,
              onPressed: onNextEvent,
              compact: true,
            ),
            _CtrlBtn(
              tooltip: 'Step forward',
              icon: Icons.chevron_right_rounded,
              onPressed: onNextPoint,
            ),
            _CtrlBtn(
              tooltip: 'Next trip',
              icon: Icons.skip_next_rounded,
              onPressed: canNextTrip ? onNextTrip : null,
            ),
          ],
        ),
        if (!compact) ...[
          const SizedBox(height: 10),
          SizedBox(
            height: 34,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: speeds.length,
              separatorBuilder: (_, __) => const SizedBox(width: 6),
              itemBuilder: (_, i) {
                final s = speeds[i];
                final selected = speed == s;
                return Semantics(
                  button: true,
                  selected: selected,
                  label: 'Playback speed ${s}x',
                  child: Material(
                    color: selected ? AppColors.navy : p.chipBg,
                    borderRadius: BorderRadius.circular(AppRadii.sm),
                    child: InkWell(
                      onTap: () => onSpeed(s),
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                      child: Container(
                        alignment: Alignment.center,
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        child: Text(
                          '${s}x',
                          style: TextStyle(
                            fontWeight: FontWeight.w800,
                            fontSize: 12,
                            color: selected ? Colors.white : p.textSecondary,
                          ),
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ] else ...[
          const SizedBox(height: 6),
          Align(
            alignment: Alignment.centerRight,
            child: PopupMenuButton<double>(
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
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '${speed}x',
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 12,
                        color: p.textSecondary,
                      ),
                    ),
                    Icon(Icons.speed, size: 18, color: p.textSecondary),
                  ],
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _CtrlBtn extends StatelessWidget {
  const _CtrlBtn({
    required this.tooltip,
    required this.icon,
    this.onPressed,
    this.compact = false,
  });

  final String tooltip;
  final IconData icon;
  final VoidCallback? onPressed;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final size = compact ? 40.0 : 44.0;
    return Semantics(
      button: true,
      enabled: onPressed != null,
      label: tooltip,
      child: Tooltip(
        message: tooltip,
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            customBorder: const CircleBorder(),
            onTap: onPressed,
            child: SizedBox(
              width: size,
              height: size,
              child: Icon(
                icon,
                size: compact ? 22 : 26,
                color: onPressed == null
                    ? p.textMuted.withValues(alpha: 0.45)
                    : p.textPrimary,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
