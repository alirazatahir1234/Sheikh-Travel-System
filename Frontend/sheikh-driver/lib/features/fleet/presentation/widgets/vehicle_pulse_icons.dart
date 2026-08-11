import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../domain/fleet_models.dart';
import 'fleet_kpi_strip.dart';

/// Canvas-drawn SheikhGo Vehicle Pulse icons for Google Maps markers.
/// Car points "up"; use [Marker.rotation] + [Marker.flat] for heading.
class VehiclePulseIcons {
  VehiclePulseIcons._();

  static const _cacheEpoch = 'pulse-v1';
  static final Map<String, BitmapDescriptor> _cache = {};
  static BitmapDescriptor? _transparent;

  static Color colorFor(FleetTrackStatus status) => fleetStatusColor(status);

  static void clearCache() {
    _cache.clear();
    _transparent = null;
  }

  static Future<void> preload(double devicePixelRatio) async {
    for (final status in FleetTrackStatus.values) {
      await iconFor(status, devicePixelRatio: devicePixelRatio);
      await iconFor(status, devicePixelRatio: devicePixelRatio, mini: true);
    }
    await transparentIcon();
  }

  /// 1×1 transparent marker used when the Flutter pulse overlay owns the icon.
  static Future<BitmapDescriptor> transparentIcon() async {
    if (_transparent != null) return _transparent!;
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    canvas.drawRect(
      const Rect.fromLTWH(0, 0, 1, 1),
      Paint()..color = const Color(0x00000000),
    );
    final image = await recorder.endRecording().toImage(1, 1);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    _transparent = BitmapDescriptor.bytes(
      data!.buffer.asUint8List(),
      imagePixelRatio: 1,
    );
    return _transparent!;
  }

  static Future<BitmapDescriptor> iconFor(
    FleetTrackStatus status, {
    double devicePixelRatio = 2,
    bool selected = false,
    bool mini = false,
  }) async {
    final key =
        '$_cacheEpoch:$status:${selected ? 's' : 'n'}:${mini ? 'm' : 'f'}:${devicePixelRatio.toStringAsFixed(1)}';
    final cached = _cache[key];
    if (cached != null) return cached;

    try {
      final bytes = await _discBytes(
        color: colorFor(status),
        status: status,
        selected: selected,
        mini: mini,
        devicePixelRatio: devicePixelRatio,
      );
      final icon = BitmapDescriptor.bytes(
        bytes,
        imagePixelRatio: devicePixelRatio,
      );
      _cache[key] = icon;
      return icon;
    } catch (_) {
      final fallback = BitmapDescriptor.defaultMarkerWithHue(
        BitmapDescriptor.hueAzure,
      );
      _cache[key] = fallback;
      return fallback;
    }
  }

  static Future<Uint8List> _discBytes({
    required Color color,
    required FleetTrackStatus status,
    required bool selected,
    required bool mini,
    required double devicePixelRatio,
  }) async {
    final logical = mini ? 28.0 : (selected ? 56.0 : 48.0);
    final size = (logical * devicePixelRatio).round();
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    canvas.scale(devicePixelRatio);

    final center = Offset(logical / 2, logical / 2);
    final radius = mini ? 8.0 : (selected ? 18.0 : 16.0);

    // Soft shadow
    canvas.drawCircle(
      center.translate(0.6, 1.2),
      radius,
      Paint()
        ..color = Colors.black.withValues(alpha: 0.28)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 2.2),
    );

    // Status fill
    canvas.drawCircle(center, radius, Paint()..color = color);

    // White ring
    canvas.drawCircle(
      center,
      radius,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = selected ? 3.0 : 2.4,
    );

    if (!mini) {
      if (status == FleetTrackStatus.parked) {
        _paintP(canvas, center, radius * 0.9);
      } else {
        _paintCar(canvas, center, radius * 0.85);
        // Small heading notch at top (north) — Marker.rotation aligns this.
        if (status == FleetTrackStatus.moving ||
            status == FleetTrackStatus.idle ||
            status == FleetTrackStatus.sos) {
          final tip = Offset(center.dx, center.dy - radius - 1);
          final arrow = Path()
            ..moveTo(tip.dx, tip.dy - 5)
            ..lineTo(tip.dx + 5, tip.dy + 2)
            ..lineTo(tip.dx - 5, tip.dy + 2)
            ..close();
          canvas.drawPath(arrow, Paint()..color = color);
          canvas.drawPath(
            arrow,
            Paint()
              ..color = Colors.white
              ..style = PaintingStyle.stroke
              ..strokeWidth = 1.2,
          );
        }
      }
    }

    final image = await recorder.endRecording().toImage(size, size);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }

  static void _paintCar(Canvas canvas, Offset center, double size) {
    final paint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.fill;
    final w = size * 0.55;
    final h = size * 0.85;
    final body = RRect.fromRectAndRadius(
      Rect.fromCenter(center: center, width: w, height: h),
      Radius.circular(w * 0.28),
    );
    canvas.drawRRect(body, paint);

    // Cabin
    final cabin = RRect.fromRectAndRadius(
      Rect.fromCenter(
        center: center.translate(0, -h * 0.08),
        width: w * 0.62,
        height: h * 0.38,
      ),
      Radius.circular(w * 0.15),
    );
    canvas.drawRRect(
      cabin,
      Paint()..color = Colors.white.withValues(alpha: 0.35),
    );

    // Wheels
    final wheelPaint = Paint()..color = Colors.white.withValues(alpha: 0.55);
    final wx = w * 0.42;
    final wy = h * 0.28;
    canvas.drawCircle(center.translate(-wx, -wy), w * 0.14, wheelPaint);
    canvas.drawCircle(center.translate(wx, -wy), w * 0.14, wheelPaint);
    canvas.drawCircle(center.translate(-wx, wy), w * 0.14, wheelPaint);
    canvas.drawCircle(center.translate(wx, wy), w * 0.14, wheelPaint);
  }

  static void _paintP(Canvas canvas, Offset center, double size) {
    final tp = TextPainter(
      text: TextSpan(
        text: 'P',
        style: TextStyle(
          color: Colors.white,
          fontSize: size * 0.95,
          fontWeight: FontWeight.w900,
          height: 1,
        ),
      ),
      textDirection: ui.TextDirection.ltr,
    )..layout();
    tp.paint(canvas, Offset(center.dx - tp.width / 2, center.dy - tp.height / 2));
  }
}

/// Paint helpers shared with [PulseVehicleOverlay] for visual parity.
class VehiclePulsePainter {
  VehiclePulsePainter._();

  static void paintDisc({
    required Canvas canvas,
    required Offset center,
    required double radius,
    required Color color,
    required FleetTrackStatus status,
    required double headingDegrees,
  }) {
    canvas.save();
    canvas.translate(center.dx, center.dy);
    canvas.rotate(headingDegrees * math.pi / 180);
    canvas.translate(-center.dx, -center.dy);

    canvas.drawCircle(
      center.translate(0.5, 1.0),
      radius,
      Paint()
        ..color = Colors.black.withValues(alpha: 0.22)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 2),
    );
    canvas.drawCircle(center, radius, Paint()..color = color);
    canvas.drawCircle(
      center,
      radius,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.5,
    );

    if (status == FleetTrackStatus.parked) {
      final tp = TextPainter(
        text: TextSpan(
          text: 'P',
          style: TextStyle(
            color: Colors.white,
            fontSize: radius * 1.1,
            fontWeight: FontWeight.w900,
            height: 1,
          ),
        ),
        textDirection: ui.TextDirection.ltr,
      )..layout();
      tp.paint(
        canvas,
        Offset(center.dx - tp.width / 2, center.dy - tp.height / 2),
      );
    } else {
      // Simple top-down car (points up / north in local space)
      final w = radius * 0.7;
      final h = radius * 1.05;
      final body = RRect.fromRectAndRadius(
        Rect.fromCenter(center: center, width: w, height: h),
        Radius.circular(w * 0.28),
      );
      canvas.drawRRect(body, Paint()..color = Colors.white);
    }

    canvas.restore();
  }
}

// Re-export color online green for KPI strip consumers.
const Color fleetOnlineColor = Color(0xFF16A34A);
