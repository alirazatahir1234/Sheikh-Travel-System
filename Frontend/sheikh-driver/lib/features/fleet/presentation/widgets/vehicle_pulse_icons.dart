import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../domain/fleet_models.dart';

/// Canvas-drawn fleet markers matching ERP live map (`fleet-vehicle-marker`).
///
/// Status glyphs:
/// - moving → heading chevron (use [Marker.rotation] + [Marker.flat])
/// - idle → pause bars
/// - parked → "P" disc
/// - sos → "!" disc
/// - offline / never_seen → muted disc
class VehiclePulseIcons {
  VehiclePulseIcons._();

  static const _cacheEpoch = 'erp-fv-v2';
  static final Map<String, BitmapDescriptor> _cache = {};
  static BitmapDescriptor? _transparent;

  /// ERP STATUS_COLORS — keep in sync with sheikhgo-erp fleet-vehicle-marker.
  static Color colorFor(FleetTrackStatus status) => switch (status) {
        FleetTrackStatus.moving => const Color(0xFF2563EB),
        FleetTrackStatus.idle => const Color(0xFFF59E0B),
        FleetTrackStatus.parked => const Color(0xFF0D9488),
        FleetTrackStatus.offline => const Color(0xFFEF4444),
        FleetTrackStatus.neverSeen => const Color(0xFF94A3B8),
        FleetTrackStatus.sos => const Color(0xFFDC2626),
      };

  /// Only moving chevrons rotate with GPS heading (ERP `fv-marker--directional`).
  static bool rotatesWithHeading(FleetTrackStatus status) =>
      status == FleetTrackStatus.moving;

  static void clearCache() {
    _cache.clear();
    _transparent = null;
  }

  static Future<void> preload(double devicePixelRatio) async {
    for (final status in FleetTrackStatus.values) {
      await iconFor(status, devicePixelRatio: devicePixelRatio);
      await iconFor(status, devicePixelRatio: devicePixelRatio, selected: true);
      await iconFor(status, devicePixelRatio: devicePixelRatio, mini: true);
    }
    await transparentIcon();
  }

  /// 1×1 transparent marker used when a Flutter pulse overlay owns the icon.
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
      final bytes = await _glyphBytes(
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

  static Future<Uint8List> _glyphBytes({
    required FleetTrackStatus status,
    required bool selected,
    required bool mini,
    required double devicePixelRatio,
  }) async {
    // ERP default size 32; selected gets a bit more host padding visually.
    final logical = mini ? 28.0 : (selected ? 44.0 : 36.0);
    final size = (logical * devicePixelRatio).round();
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    canvas.scale(devicePixelRatio);

    final color = colorFor(status);
    final glyphSize = mini ? 22.0 : (selected ? 34.0 : 30.0);
    final origin = Offset((logical - glyphSize) / 2, (logical - glyphSize) / 2);

    // Soft shadow under glyph
    canvas.drawCircle(
      Offset(logical / 2, logical / 2).translate(0.5, 1.0),
      glyphSize * 0.42,
      Paint()
        ..color = Colors.black.withValues(alpha: 0.22)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 1.8),
    );

    canvas.save();
    canvas.translate(origin.dx, origin.dy);
    canvas.scale(glyphSize / 32);
    _paintErpGlyph(canvas, status, color);
    canvas.restore();

    if (selected) {
      canvas.drawCircle(
        Offset(logical / 2, logical / 2),
        glyphSize * 0.52,
        Paint()
          ..color = Colors.white
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2.5,
      );
    }

    final image = await recorder.endRecording().toImage(size, size);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }

  /// Paint ERP SVG glyphs in a 32×32 viewBox.
  static void _paintErpGlyph(
      Canvas canvas, FleetTrackStatus status, Color fill) {
    final fillPaint = Paint()
      ..color = fill
      ..style = PaintingStyle.fill;
    final strokePaint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.75
      ..strokeJoin = StrokeJoin.round;

    switch (status) {
      case FleetTrackStatus.parked:
        canvas.drawCircle(const Offset(16, 16), 13, fillPaint);
        canvas.drawCircle(const Offset(16, 16), 13, strokePaint);
        _paintCenteredText(canvas, 'P', 14, FontWeight.w800);
        break;
      case FleetTrackStatus.idle:
        canvas.drawCircle(const Offset(16, 16), 13, fillPaint);
        canvas.drawCircle(const Offset(16, 16), 13, strokePaint);
        final bar = Paint()..color = Colors.white;
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            const Rect.fromLTWH(11, 10, 3.2, 12),
            const Radius.circular(1),
          ),
          bar,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            const Rect.fromLTWH(17.8, 10, 3.2, 12),
            const Radius.circular(1),
          ),
          bar,
        );
        break;
      case FleetTrackStatus.sos:
        canvas.drawCircle(const Offset(16, 16), 13, fillPaint);
        canvas.drawCircle(const Offset(16, 16), 13, strokePaint);
        _paintCenteredText(canvas, '!', 16, FontWeight.w900);
        break;
      case FleetTrackStatus.offline:
      case FleetTrackStatus.neverSeen:
        canvas.drawCircle(const Offset(16, 16), 12, fillPaint);
        canvas.drawCircle(const Offset(16, 16), 12, strokePaint);
        canvas.drawCircle(
          const Offset(16, 16),
          4.5,
          Paint()..color = Colors.white.withValues(alpha: 0.45),
        );
        break;
      case FleetTrackStatus.moving:
        // Chevron pointing north — Marker.rotation applies heading.
        final chevron = Path()
          ..moveTo(16, 1.5)
          ..lineTo(27.5, 27.5)
          ..lineTo(16, 21.5)
          ..lineTo(4.5, 27.5)
          ..close();
        canvas.drawPath(chevron, strokePaint);
        canvas.drawPath(chevron, fillPaint);
        canvas.drawCircle(
          const Offset(16, 18.5),
          3.2,
          Paint()..color = Colors.white.withValues(alpha: 0.4),
        );
        break;
    }
  }

  static void _paintCenteredText(
    Canvas canvas,
    String text,
    double fontSize,
    FontWeight weight,
  ) {
    final tp = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(
          color: Colors.white,
          fontSize: fontSize,
          fontWeight: weight,
          height: 1,
        ),
      ),
      textDirection: ui.TextDirection.ltr,
    )..layout();
    tp.paint(canvas, Offset(16 - tp.width / 2, 16 - tp.height / 2));
  }
}

/// Paint helpers shared with [PulseVehicleOverlay] for visual parity with ERP.
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
    // Scale 32 viewBox to ~2×radius diameter.
    final scale = (radius * 2) / 32;
    if (VehiclePulseIcons.rotatesWithHeading(status)) {
      canvas.rotate(headingDegrees * math.pi / 180);
    }
    canvas.scale(scale);
    canvas.translate(-16, -16);
    VehiclePulseIcons._paintErpGlyph(canvas, status, color);
    canvas.restore();
  }
}

/// Online green for KPI strip consumers (connection, not track status).
const Color fleetOnlineColor = Color(0xFF16A34A);
