import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../domain/fleet_models.dart';
import 'fleet_kpi_strip.dart';
import 'vehicle_pulse_icons.dart';

/// Radar-style pulse overlay for a single focused vehicle on Google Maps.
/// Position via [screenPosition] from [GoogleMapController.getScreenCoordinate].
class PulseVehicleOverlay extends StatefulWidget {
  const PulseVehicleOverlay({
    super.key,
    required this.screenPosition,
    required this.status,
    this.headingDegrees = 0,
    this.visible = true,
    this.discRadius = 18,
    this.showDisc = true,
  });

  final Offset? screenPosition;
  final FleetTrackStatus status;
  final double headingDegrees;
  final bool visible;
  final double discRadius;

  /// When false, only radar rings are drawn (map marker supplies the vehicle icon).
  final bool showDisc;

  @override
  State<PulseVehicleOverlay> createState() => _PulseVehicleOverlayState();
}

class _PulseVehicleOverlayState extends State<PulseVehicleOverlay>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  static const _duration = Duration(milliseconds: 1200);

  bool get _shouldPulse {
    if (!widget.visible || widget.screenPosition == null) return false;
    return widget.status != FleetTrackStatus.offline &&
        widget.status != FleetTrackStatus.neverSeen;
  }

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: _duration);
    _syncController();
  }

  @override
  void didUpdateWidget(covariant PulseVehicleOverlay oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncController();
  }

  void _syncController() {
    if (_shouldPulse) {
      if (!_controller.isAnimating) {
        _controller.repeat();
      }
    } else {
      _controller.stop();
      _controller.value = 0;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final pos = widget.screenPosition;
    if (!widget.visible || pos == null) {
      return const SizedBox.shrink();
    }

    final color = fleetStatusColor(widget.status);
    const size = 120.0;

    return IgnorePointer(
      child: Stack(
        children: [
          Positioned(
            left: pos.dx - size / 2,
            top: pos.dy - size / 2,
            width: size,
            height: size,
            child: AnimatedBuilder(
              animation: _controller,
              builder: (context, _) {
                return CustomPaint(
                  size: const Size(size, size),
                  painter: _PulsePainter(
                    progress: _controller.value,
                    color: color,
                    status: widget.status,
                    headingDegrees: widget.headingDegrees,
                    discRadius: widget.discRadius,
                    showRings: _shouldPulse,
                    showDisc: widget.showDisc,
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _PulsePainter extends CustomPainter {
  _PulsePainter({
    required this.progress,
    required this.color,
    required this.status,
    required this.headingDegrees,
    required this.discRadius,
    required this.showRings,
    required this.showDisc,
  });

  final double progress;
  final Color color;
  final FleetTrackStatus status;
  final double headingDegrees;
  final double discRadius;
  final bool showRings;
  final bool showDisc;

  static const _phases = [0.0, 0.33, 0.66];

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);

    if (showRings) {
      for (final phase in _phases) {
        var t = (progress + phase) % 1.0;
        final scale = 1.0 + 0.6 * t; // 1.0 → 1.6
        final opacity = 0.35 * (1.0 - t);
        final r = discRadius * 1.35 * scale;
        canvas.drawCircle(
          center,
          r,
          Paint()
            ..color = color.withValues(alpha: opacity)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 2.2,
        );
      }
    }

    if (showDisc) {
      VehiclePulsePainter.paintDisc(
        canvas: canvas,
        center: center,
        radius: discRadius,
        color: color,
        status: status,
        headingDegrees: headingDegrees,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _PulsePainter oldDelegate) {
    return oldDelegate.progress != progress ||
        oldDelegate.color != color ||
        oldDelegate.status != status ||
        oldDelegate.headingDegrees != headingDegrees ||
        oldDelegate.showRings != showRings ||
        oldDelegate.showDisc != showDisc;
  }
}

/// Resolves map LatLng → overlay Offset inside a [Stack] that wraps [GoogleMap].
///
/// [GoogleMapController.getScreenCoordinate] returns **physical** pixels on
/// Android/iOS. Flutter layout uses **logical** pixels, so we divide by
/// [devicePixelRatio] (fallback: view DPR from [stackKey] context).
Future<Offset?> mapLatLngToOverlayOffset({
  required GoogleMapController? map,
  required LatLng? target,
  required GlobalKey stackKey,
  double? devicePixelRatio,
}) async {
  if (map == null || target == null) return null;
  try {
    final ctx = stackKey.currentContext;
    final dpr = devicePixelRatio ??
        (ctx != null ? MediaQuery.maybeDevicePixelRatioOf(ctx) : null) ??
        1.0;
    final safeDpr = dpr <= 0 ? 1.0 : dpr;
    final coord = await map.getScreenCoordinate(target);
    // Physical → logical for Stack positioning.
    return Offset(coord.x.toDouble() / safeDpr, coord.y.toDouble() / safeDpr);
  } catch (_) {
    return null;
  }
}

/// Degrees → radians helper for callers that need Transform.rotate.
double headingToRadians(double degrees) => degrees * math.pi / 180;
