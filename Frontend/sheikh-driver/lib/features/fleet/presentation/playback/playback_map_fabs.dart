import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../../../core/constants/app_theme.dart';

/// Always-visible map controls: Zoom+, Zoom−, Center, Layers.
class PlaybackMapFabs extends StatelessWidget {
  const PlaybackMapFabs({
    super.key,
    required this.bottom,
    required this.onZoomIn,
    required this.onZoomOut,
    required this.onCenter,
    required this.onMapType,
    required this.onFitRoute,
    required this.mapType,
    this.visible = true,
  });

  final double bottom;
  final VoidCallback onZoomIn;
  final VoidCallback onZoomOut;
  final VoidCallback onCenter;
  final ValueChanged<MapType> onMapType;
  final VoidCallback onFitRoute;
  final MapType mapType;
  final bool visible;

  @override
  Widget build(BuildContext context) {
    return Positioned(
      right: 12,
      bottom: bottom,
      child: AnimatedOpacity(
        opacity: visible ? 1 : 0,
        duration: const Duration(milliseconds: 220),
        child: IgnorePointer(
          ignoring: !visible,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _MapFab(icon: Icons.add, tooltip: 'Zoom in', onPressed: onZoomIn),
              const SizedBox(height: 12),
              _MapFab(icon: Icons.remove, tooltip: 'Zoom out', onPressed: onZoomOut),
              const SizedBox(height: 12),
              _MapFab(
                icon: Icons.my_location,
                tooltip: 'Center vehicle',
                onPressed: onCenter,
              ),
              const SizedBox(height: 12),
              PopupMenuButton<String>(
                tooltip: 'Layers',
                offset: const Offset(-8, 0),
                position: PopupMenuPosition.under,
                onSelected: (v) {
                  switch (v) {
                    case 'standard':
                      onMapType(MapType.normal);
                    case 'satellite':
                      onMapType(MapType.hybrid);
                    case 'fit':
                      onFitRoute();
                  }
                },
                itemBuilder: (_) => [
                  CheckedPopupMenuItem(
                    value: 'standard',
                    checked: mapType == MapType.normal,
                    child: const Text('Standard'),
                  ),
                  CheckedPopupMenuItem(
                    value: 'satellite',
                    checked: mapType == MapType.hybrid || mapType == MapType.satellite,
                    child: const Text('Satellite'),
                  ),
                  const PopupMenuDivider(),
                  const PopupMenuItem(
                    value: 'fit',
                    child: ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      leading: Icon(Icons.fit_screen, size: 20),
                      title: Text('Fit route'),
                    ),
                  ),
                ],
                child: const _MapFabChrome(icon: Icons.layers_outlined),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MapFab extends StatelessWidget {
  const _MapFab({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: Material(
        color: Colors.white,
        elevation: 3,
        shadowColor: Colors.black26,
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: onPressed,
          child: SizedBox(
            width: 44,
            height: 44,
            child: Icon(icon, size: 22, color: AppColors.textPrimary),
          ),
        ),
      ),
    );
  }
}

class _MapFabChrome extends StatelessWidget {
  const _MapFabChrome({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      elevation: 3,
      shadowColor: Colors.black26,
      shape: const CircleBorder(),
      child: SizedBox(
        width: 44,
        height: 44,
        child: Icon(icon, size: 22, color: AppColors.textPrimary),
      ),
    );
  }
}
