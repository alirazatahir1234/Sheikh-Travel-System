import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';

class PlaybackMapFabs extends StatefulWidget {
  const PlaybackMapFabs({
    super.key,
    required this.following,
    required this.onFitRoute,
    required this.onToggleFollow,
    required this.onToggleMapType,
  });

  final bool following;
  final VoidCallback onFitRoute;
  final VoidCallback onToggleFollow;
  final VoidCallback onToggleMapType;

  @override
  State<PlaybackMapFabs> createState() => _PlaybackMapFabsState();
}

class _PlaybackMapFabsState extends State<PlaybackMapFabs> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    return AnimatedSize(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOutCubic,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (_expanded) ...[
            _actionFab(
              icon: Icons.my_location,
              tooltip: 'Fit route',
              onTap: () {
                widget.onFitRoute();
                setState(() => _expanded = false);
              },
            ),
            const SizedBox(height: 8),
            _actionFab(
              icon: widget.following ? Icons.navigation : Icons.open_with_rounded,
              tooltip: widget.following ? 'Follow vehicle' : 'Free pan',
              highlighted: widget.following,
              onTap: () {
                widget.onToggleFollow();
                setState(() => _expanded = false);
              },
            ),
            const SizedBox(height: 8),
            _actionFab(
              icon: Icons.layers_outlined,
              tooltip: 'Map layers',
              onTap: () {
                widget.onToggleMapType();
                setState(() => _expanded = false);
              },
            ),
            const SizedBox(height: 8),
          ],
          FloatingActionButton(
            heroTag: 'playback_fab_main',
            mini: true,
            backgroundColor: AppColors.primary,
            foregroundColor: Colors.white,
            onPressed: () => setState(() => _expanded = !_expanded),
            child: Icon(_expanded ? Icons.close : Icons.tune_rounded),
          ),
        ],
      ),
    );
  }

  Widget _actionFab({
    required IconData icon,
    required String tooltip,
    required VoidCallback onTap,
    bool highlighted = false,
  }) {
    return Material(
      color: Colors.white,
      elevation: 3,
      shape: const CircleBorder(),
      child: IconButton(
        tooltip: tooltip,
        icon: Icon(
          icon,
          size: 22,
          color: highlighted ? AppColors.primary : AppColors.textPrimary,
        ),
        onPressed: onTap,
      ),
    );
  }
}
