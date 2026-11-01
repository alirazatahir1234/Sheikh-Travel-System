import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import 'playback_map_builder.dart';

/// Compact legend — collapsed by default so it does not dominate the map.
class PlaybackLegend extends StatefulWidget {
  const PlaybackLegend({super.key, this.initiallyExpanded = false});

  final bool initiallyExpanded;

  @override
  State<PlaybackLegend> createState() => _PlaybackLegendState();
}

class _PlaybackLegendState extends State<PlaybackLegend> {
  late bool _expanded = widget.initiallyExpanded;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card.withValues(alpha: 0.96),
      elevation: 2,
      shadowColor: Colors.black26,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.md),
        onTap: () => setState(() => _expanded = !_expanded),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
          child: _expanded
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Legend',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w800,
                            color: p.textPrimary,
                          ),
                        ),
                        const SizedBox(width: 4),
                        Icon(Icons.expand_less, size: 16, color: p.textMuted),
                      ],
                    ),
                    const SizedBox(height: 6),
                    const Wrap(
                      spacing: 8,
                      runSpacing: 6,
                      children: [
                        _LegendItem(
                          color: kPlaybackRemainingColor,
                          label: 'Remaining',
                        ),
                        _LegendItem(
                          color: kPlaybackTravelledColor,
                          label: 'Travelled',
                        ),
                        _LegendItem(
                          color: kPlaybackStopVisitedColor,
                          label: 'Stop visited',
                        ),
                        _LegendItem(
                          color: kPlaybackStopUpcomingColor,
                          label: 'Stop upcoming',
                        ),
                      ],
                    ),
                  ],
                )
              : Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const _LegendItem(
                      color: kPlaybackTravelledColor,
                      label: 'Route',
                    ),
                    const SizedBox(width: 8),
                    Icon(Icons.info_outline, size: 14, color: p.textMuted),
                  ],
                ),
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
          style: TextStyle(
            fontSize: 11,
            color: context.sg.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}
