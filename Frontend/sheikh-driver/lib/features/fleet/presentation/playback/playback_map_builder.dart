import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';

import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';

/// User tapped a semantic playback marker (start, finish, stop, or event).
class PlaybackMarkerTap {
  const PlaybackMarkerTap({
    required this.playbackIndex,
    this.eventType,
    this.eventLabel,
    this.stopDurationMinutes,
  });

  final int playbackIndex;
  final String? eventType;
  final String? eventLabel;
  final int? stopDurationMinutes;
}

typedef PlaybackMarkerTapCallback = void Function(PlaybackMarkerTap tap);

String formatPlaybackCoords(double latitude, double longitude) =>
    '${latitude.toStringAsFixed(5)}, ${longitude.toStringAsFixed(5)}';

String? sanitizePlaybackAddress(String? address) {
  final raw = address?.trim();
  if (raw == null || raw.isEmpty) return null;
  var cleaned = raw;
  if (cleaned.toLowerCase().startsWith('near ')) {
    final comma = cleaned.indexOf(',');
    cleaned = comma > 0 ? cleaned.substring(comma + 1).trim() : '';
  }
  if (cleaned.isEmpty) return null;
  final parts = cleaned
      .split(',')
      .map((e) => e.trim())
      .where((e) => e.isNotEmpty)
      .where(
        (e) => !RegExp(
          r'\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b',
          caseSensitive: false,
        ).hasMatch(e),
      )
      .toList();
  if (parts.isEmpty) return null;
  return parts.join(', ');
}

/// True when address is city/province only (e.g. "Pasrur, Punjab, Pakistan").
bool isCoarsePlaybackAddress(String? address) {
  final a = sanitizePlaybackAddress(address) ?? address?.trim();
  if (a == null || a.isEmpty) return true;
  final lower = a.toLowerCase();
  if (lower.contains('tehsil') ||
      lower.contains('district') ||
      lower.contains('division')) {
    return true;
  }
  final parts =
      a.split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();
  if (parts.any((p) => RegExp(r'\d').hasMatch(p))) return false;
  return parts.length <= 3;
}

/// Primary street/POI line + optional locality for address-first UI.
({String? primary, String? secondary}) splitPlaybackAddress(String? address) {
  final raw = sanitizePlaybackAddress(address) ?? address?.trim();
  if (raw == null || raw.isEmpty) {
    return (primary: null, secondary: null);
  }
  final parts =
      raw.split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();
  if (parts.length <= 2) {
    return (primary: raw, secondary: null);
  }
  final take = parts.length >= 4 ? 2 : 1;
  return (
    primary: parts.take(take).join(', '),
    secondary: parts.skip(take).join(', '),
  );
}

String playbackAddressLine(HistoryReplayPoint point) {
  final addr = point.address?.trim();
  if (addr != null && addr.isNotEmpty && !isCoarsePlaybackAddress(addr)) {
    return addr;
  }
  if (addr != null && addr.isNotEmpty) return addr;
  return formatPlaybackCoords(point.latitude, point.longitude);
}

String playbackAddressPrimary(String? address, {double? lat, double? lng}) {
  final split = splitPlaybackAddress(address);
  if (split.primary != null && split.primary!.isNotEmpty) return split.primary!;
  if (lat != null && lng != null) return formatPlaybackCoords(lat, lng);
  return 'Address unavailable';
}

String? playbackAddressSecondary(String? address) =>
    splitPlaybackAddress(address).secondary;

String? playbackNearbyLine(String? address) {
  final raw = address?.trim();
  if (raw == null || raw.isEmpty) return null;
  if (!raw.toLowerCase().startsWith('near ')) return null;
  final comma = raw.indexOf(',');
  final near = (comma > 0 ? raw.substring(5, comma) : raw.substring(5)).trim();
  if (near.isEmpty) return null;
  return 'Near: $near';
}

String stopPlaybackHeadline(TripStop stop) {
  final kind = stop.durationMinutes >= 120 ? 'Parking' : 'Stop';
  final primary = playbackAddressPrimary(
    stop.address,
    lat: stop.latitude,
    lng: stop.longitude,
  );
  return '$kind — $primary';
}

String stopPlaybackHeadlineFor({
  required int durationMinutes,
  String? address,
  required double latitude,
  required double longitude,
}) {
  final kind = durationMinutes >= 120 ? 'Parking' : 'Stop';
  final primary = playbackAddressPrimary(
    address,
    lat: latitude,
    lng: longitude,
  );
  return '$kind — $primary';
}

enum PlaybackMarkerKind {
  vehicle,
  start,
  finish,
  stop,
  overspeed,
  ignitionOn,
  ignitionOff,
  geofence,
  fuel,
  sos,
  eventFallback,
}

enum PlaybackPinGlyph {
  play,
  flag,
  parking,
  speed,
  power,
  powerOff,
  fence,
  fuel,
  sos,
  pin,
}

class PlaybackMapAssets {
  /// Bump when icon art changes so hot restart refreshes descriptors.
  static const _cacheEpoch = 'v4';
  static final Map<String, BitmapDescriptor> _iconCache = {};

  static Future<BitmapDescriptor> vehicleIcon({double devicePixelRatio = 2}) {
    return markerForKind(
      PlaybackMarkerKind.vehicle,
      devicePixelRatio: devicePixelRatio,
    );
  }

  static void clearCache() => _iconCache.clear();

  static Color colorForKind(PlaybackMarkerKind kind) {
    switch (kind) {
      case PlaybackMarkerKind.start:
        return const Color(0xFF10B981);
      case PlaybackMarkerKind.finish:
        return const Color(0xFFEF4444);
      case PlaybackMarkerKind.stop:
        return const Color(0xFFF59E0B);
      case PlaybackMarkerKind.overspeed:
      case PlaybackMarkerKind.sos:
        return const Color(0xFFDC2626);
      case PlaybackMarkerKind.ignitionOn:
        return const Color(0xFF22C55E);
      case PlaybackMarkerKind.ignitionOff:
        return const Color(0xFF334155);
      case PlaybackMarkerKind.geofence:
        return const Color(0xFF8B5CF6);
      case PlaybackMarkerKind.fuel:
        return const Color(0xFF0EA5E9);
      case PlaybackMarkerKind.vehicle:
        return const Color(0xFF1D4ED8);
      case PlaybackMarkerKind.eventFallback:
        return const Color(0xFF64748B);
    }
  }

  static PlaybackPinGlyph glyphForKind(PlaybackMarkerKind kind) {
    switch (kind) {
      case PlaybackMarkerKind.start:
        return PlaybackPinGlyph.play;
      case PlaybackMarkerKind.finish:
        return PlaybackPinGlyph.flag;
      case PlaybackMarkerKind.stop:
        return PlaybackPinGlyph.parking;
      case PlaybackMarkerKind.overspeed:
        return PlaybackPinGlyph.speed;
      case PlaybackMarkerKind.ignitionOn:
        return PlaybackPinGlyph.power;
      case PlaybackMarkerKind.ignitionOff:
        return PlaybackPinGlyph.powerOff;
      case PlaybackMarkerKind.geofence:
        return PlaybackPinGlyph.fence;
      case PlaybackMarkerKind.fuel:
        return PlaybackPinGlyph.fuel;
      case PlaybackMarkerKind.sos:
        return PlaybackPinGlyph.sos;
      case PlaybackMarkerKind.vehicle:
      case PlaybackMarkerKind.eventFallback:
        return PlaybackPinGlyph.pin;
    }
  }

  static PlaybackMarkerKind markerKindForEvent(TripEvent e) {
    final t = e.type.toLowerCase();
    if (t.contains('overspeed') || t.contains('speed')) {
      return PlaybackMarkerKind.overspeed;
    }
    if (t.contains('ignition') && (t.contains('off') || t.contains('false'))) {
      return PlaybackMarkerKind.ignitionOff;
    }
    if (t.contains('ignition')) return PlaybackMarkerKind.ignitionOn;
    if (t.contains('geofence')) return PlaybackMarkerKind.geofence;
    if (t.contains('fuel')) return PlaybackMarkerKind.fuel;
    if (t.contains('sos') || t.contains('panic') || t.contains('distress')) {
      return PlaybackMarkerKind.sos;
    }
    return PlaybackMarkerKind.eventFallback;
  }

  static Future<BitmapDescriptor> markerForEvent(
    TripEvent event, {
    double devicePixelRatio = 2,
  }) {
    return markerForKind(
      markerKindForEvent(event),
      devicePixelRatio: devicePixelRatio,
    );
  }

  static Future<BitmapDescriptor> markerForKind(
    PlaybackMarkerKind kind, {
    double devicePixelRatio = 2,
  }) async {
    final key = '$_cacheEpoch:$kind:${devicePixelRatio.toStringAsFixed(1)}';
    final cached = _iconCache[key];
    if (cached != null) return cached;

    try {
      final bytes = kind == PlaybackMarkerKind.vehicle
          ? await _vehicleArrowBytes(
              colorForKind(kind),
              devicePixelRatio: devicePixelRatio,
            )
          : await _glyphPinBytes(
              background: colorForKind(kind),
              glyph: glyphForKind(kind),
              devicePixelRatio: devicePixelRatio,
            );
      final icon = BitmapDescriptor.bytes(
        bytes,
        imagePixelRatio: devicePixelRatio,
      );
      _iconCache[key] = icon;
      return icon;
    } catch (_) {
      final fallback = _fallbackMarker(kind);
      _iconCache[key] = fallback;
      return fallback;
    }
  }

  static BitmapDescriptor _fallbackMarker(PlaybackMarkerKind kind) {
    switch (kind) {
      case PlaybackMarkerKind.start:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen);
      case PlaybackMarkerKind.finish:
      case PlaybackMarkerKind.overspeed:
      case PlaybackMarkerKind.sos:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueRed);
      case PlaybackMarkerKind.stop:
      case PlaybackMarkerKind.fuel:
        return BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueOrange);
      case PlaybackMarkerKind.ignitionOn:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen);
      case PlaybackMarkerKind.ignitionOff:
      case PlaybackMarkerKind.eventFallback:
        return BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueViolet);
      case PlaybackMarkerKind.geofence:
        return BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueMagenta);
      case PlaybackMarkerKind.vehicle:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure);
    }
  }

  /// Compact ~30 logical-px navigation chevron with soft shadow + white stroke.
  static Future<Uint8List> _vehicleArrowBytes(
    Color color, {
    required double devicePixelRatio,
  }) async {
    const logical = 64.0;
    final size = logical * devicePixelRatio;
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    final scale = devicePixelRatio;
    canvas.scale(scale);

    final cx = logical / 2;
    final cy = logical / 2;

    // Soft drop shadow under the arrow
    final shadowPath = Path()
      ..moveTo(cx, 10)
      ..lineTo(cx + 13, cy + 16)
      ..lineTo(cx, cy + 9)
      ..lineTo(cx - 13, cy + 16)
      ..close();
    canvas.drawPath(
      shadowPath.shift(const Offset(1.2, 2.2)),
      Paint()
        ..color = Colors.black.withValues(alpha: 0.28)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 2.5),
    );

    final arrow = Path()
      ..moveTo(cx, 10)
      ..lineTo(cx + 13, cy + 16)
      ..lineTo(cx, cy + 9)
      ..lineTo(cx - 13, cy + 16)
      ..close();

    canvas.drawPath(
      arrow,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3.2
        ..strokeJoin = StrokeJoin.round,
    );
    canvas.drawPath(arrow, Paint()..color = color);

    final image =
        await recorder.endRecording().toImage(size.toInt(), size.toInt());
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }

  static Future<Uint8List> _glyphPinBytes({
    required Color background,
    required PlaybackPinGlyph glyph,
    required double devicePixelRatio,
  }) async {
    const logicalW = 40.0;
    const logicalH = 48.0;
    final width = logicalW * devicePixelRatio;
    final height = logicalH * devicePixelRatio;
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    canvas.scale(devicePixelRatio);

    final path = Path()
      ..moveTo(logicalW / 2, logicalH)
      ..quadraticBezierTo(
          logicalW / 2 - 4, logicalH - 10, logicalW / 2 - 12, logicalH - 18)
      ..arcToPoint(
        Offset(logicalW / 2 + 12, logicalH - 18),
        radius: const Radius.circular(13),
        clockwise: false,
      )
      ..quadraticBezierTo(
          logicalW / 2 + 4, logicalH - 10, logicalW / 2, logicalH)
      ..close();

    canvas.drawPath(
      path.shift(const Offset(0.8, 1.2)),
      Paint()
        ..color = Colors.black.withValues(alpha: 0.22)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 1.5),
    );
    canvas.drawPath(path, Paint()..color = background);
    canvas.drawPath(
      path,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );

    _paintGlyph(canvas, glyph, Offset(logicalW / 2, (logicalH - 18) / 2));

    final image =
        await recorder.endRecording().toImage(width.toInt(), height.toInt());
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }

  static void _paintGlyph(
      Canvas canvas, PlaybackPinGlyph glyph, Offset center) {
    final paint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.fill
      ..strokeWidth = 1.8
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    switch (glyph) {
      case PlaybackPinGlyph.play:
        final p = Path()
          ..moveTo(center.dx - 4, center.dy - 6)
          ..lineTo(center.dx + 6, center.dy)
          ..lineTo(center.dx - 4, center.dy + 6)
          ..close();
        canvas.drawPath(p, paint);
      case PlaybackPinGlyph.flag:
        canvas.drawLine(
          Offset(center.dx - 5, center.dy - 7),
          Offset(center.dx - 5, center.dy + 7),
          paint..style = PaintingStyle.stroke,
        );
        final flag = Path()
          ..moveTo(center.dx - 4, center.dy - 7)
          ..lineTo(center.dx + 6, center.dy - 4)
          ..lineTo(center.dx - 4, center.dy - 1)
          ..close();
        canvas.drawPath(flag, Paint()..color = Colors.white);
      case PlaybackPinGlyph.parking:
        final r = RRect.fromRectAndRadius(
          Rect.fromCenter(center: center, width: 14, height: 14),
          const Radius.circular(3),
        );
        canvas.drawRRect(
            r, Paint()..color = Colors.white.withValues(alpha: 0.25));
        final tp = TextPainter(
          text: const TextSpan(
            text: 'P',
            style: TextStyle(
              color: Colors.white,
              fontSize: 11,
              fontWeight: FontWeight.w900,
            ),
          ),
          textDirection: ui.TextDirection.ltr,
        )..layout();
        tp.paint(canvas,
            Offset(center.dx - tp.width / 2, center.dy - tp.height / 2));
      case PlaybackPinGlyph.speed:
        // Speedometer arc + needle
        paint
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2;
        canvas.drawArc(
          Rect.fromCenter(center: center, width: 14, height: 14),
          math.pi * 0.85,
          math.pi * 1.3,
          false,
          paint,
        );
        canvas.drawLine(
          center,
          Offset(center.dx + 4, center.dy - 4),
          paint,
        );
      case PlaybackPinGlyph.power:
        paint.style = PaintingStyle.stroke;
        canvas.drawArc(
          Rect.fromCenter(
              center: center.translate(0, 1), width: 12, height: 12),
          -math.pi * 0.25,
          math.pi * 1.5,
          false,
          paint,
        );
        canvas.drawLine(
          Offset(center.dx, center.dy - 6),
          Offset(center.dx, center.dy + 1),
          paint,
        );
      case PlaybackPinGlyph.powerOff:
        paint.style = PaintingStyle.stroke;
        canvas.drawCircle(center, 6, paint);
        canvas.drawLine(
          Offset(center.dx - 4, center.dy + 4),
          Offset(center.dx + 4, center.dy - 4),
          paint,
        );
      case PlaybackPinGlyph.fence:
        paint.style = PaintingStyle.stroke;
        final rect = Rect.fromCenter(center: center, width: 12, height: 12);
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(2)),
          paint,
        );
        canvas.drawLine(
          Offset(center.dx, center.dy - 6),
          Offset(center.dx, center.dy + 6),
          paint,
        );
        canvas.drawLine(
          Offset(center.dx - 6, center.dy),
          Offset(center.dx + 6, center.dy),
          paint,
        );
      case PlaybackPinGlyph.fuel:
        paint.style = PaintingStyle.stroke;
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromLTWH(center.dx - 5, center.dy - 6, 8, 12),
            const Radius.circular(1.5),
          ),
          paint,
        );
        canvas.drawLine(
          Offset(center.dx + 3, center.dy - 2),
          Offset(center.dx + 6, center.dy + 1),
          paint,
        );
      case PlaybackPinGlyph.sos:
        final tp = TextPainter(
          text: const TextSpan(
            text: '!',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w900,
              height: 1,
            ),
          ),
          textDirection: ui.TextDirection.ltr,
        )..layout();
        tp.paint(canvas,
            Offset(center.dx - tp.width / 2, center.dy - tp.height / 2));
      case PlaybackPinGlyph.pin:
        canvas.drawCircle(center, 3.5, paint);
    }
  }
}

Color segmentColor(PlaybackSegmentKind kind) {
  switch (kind) {
    case PlaybackSegmentKind.overspeed:
      return const Color(0xFFDC2626);
    case PlaybackSegmentKind.ignitionOff:
      return const Color(0xFF64748B);
    case PlaybackSegmentKind.stop:
      return const Color(0xFFF59E0B);
    case PlaybackSegmentKind.normal:
      return const Color(0xFF1D4ED8);
  }
}

/// Stride-sample long trails for map rendering (keeps endpoints).
List<HistoryReplayPoint> downsampleTrailForMap(
  List<HistoryReplayPoint> trail, {
  int maxPoints = 2500,
}) {
  if (trail.length <= maxPoints) return trail;
  final step = (trail.length / maxPoints).ceil();
  final out = <HistoryReplayPoint>[trail.first];
  for (var i = step; i < trail.length - 1; i += step) {
    out.add(trail[i]);
  }
  if (out.last != trail.last) out.add(trail.last);
  return out;
}

/// Map a full-trail index onto a downsampled display trail by timestamp.
int displayIndexForTrailIndex(
  List<HistoryReplayPoint> fullTrail,
  List<HistoryReplayPoint> displayTrail,
  int trailIndex,
) {
  if (displayTrail.isEmpty || fullTrail.isEmpty) return 0;
  if (identical(fullTrail, displayTrail) ||
      fullTrail.length == displayTrail.length) {
    return trailIndex.clamp(0, displayTrail.length - 1);
  }
  final t = fullTrail[trailIndex.clamp(0, fullTrail.length - 1)].timestamp;
  return indexForTimestamp(displayTrail, t);
}

Set<Polyline> buildPlaybackPolylines({
  required List<HistoryReplayPoint> trail,
  required int trailIndex,
  required List<TripStop> stops,
  bool speedColors = false,
}) {
  if (trail.length < 2) return {};

  final display = downsampleTrailForMap(trail);
  final displayIdx = displayIndexForTrailIndex(trail, display, trailIndex)
      .clamp(1, display.length);

  final polylines = <Polyline>{};

  // Full trip always solid dark blue for enterprise visibility.
  polylines.add(
    Polyline(
      polylineId: const PolylineId('full_route'),
      color: const Color(0xFF1D4ED8),
      width: 7,
      geodesic: true,
      zIndex: 1,
      points: [
        for (final p in display) LatLng(p.latitude, p.longitude),
      ],
    ),
  );

  if (speedColors) {
    final completed = display.sublist(0, displayIdx);
    final segments = buildSpeedSegments(completed, stops: stops);
    for (var i = 0; i < segments.length; i++) {
      final s = segments[i];
      if (s.points.length < 2) continue;
      if (s.kind == PlaybackSegmentKind.normal) continue;
      polylines.add(
        Polyline(
          polylineId: PolylineId('seg_$i'),
          color: segmentColor(s.kind),
          width: 7,
          geodesic: true,
          zIndex: 2,
          points: [
            for (final p in s.points) LatLng(p.latitude, p.longitude),
          ],
        ),
      );
    }
  }

  // Played focus highlight near current index.
  if (display.length >= 3) {
    final currentStart = (displayIdx - 2).clamp(0, display.length - 1);
    final currentEnd = displayIdx.clamp(0, display.length - 1);
    if (currentEnd > currentStart) {
      polylines.add(
        Polyline(
          polylineId: const PolylineId('current_focus'),
          color: const Color(0xFF22C55E),
          width: 8,
          geodesic: true,
          zIndex: 3,
          points: [
            for (var i = currentStart; i <= currentEnd; i++)
              LatLng(display[i].latitude, display[i].longitude),
          ],
        ),
      );
    }
  }

  return polylines;
}

Marker buildVehicleMarker({
  required HistoryReplayPoint point,
  required BitmapDescriptor vehicleIcon,
  double? headingOverride,
  LatLng? positionOverride,
}) {
  return Marker(
    markerId: const MarkerId('vehicle'),
    position: positionOverride ?? LatLng(point.latitude, point.longitude),
    icon: vehicleIcon,
    rotation: headingOverride ?? point.heading ?? 0,
    flat: true,
    anchor: const Offset(0.5, 0.5),
    zIndexInt: 10,
  );
}

Future<Set<Marker>> buildPlaybackStaticMarkers({
  required List<HistoryReplayPoint> trail,
  required List<HistoryReplayPoint> playback,
  required List<TripStop> stops,
  required List<TripEvent> events,
  double devicePixelRatio = 2,
  HistoryReplayPoint? vehiclePoint,
  PlaybackMarkerTapCallback? onMarkerTap,
}) async {
  final markers = <Marker>{};
  if (trail.isEmpty) return markers;

  final startIcon = await PlaybackMapAssets.markerForKind(
    PlaybackMarkerKind.start,
    devicePixelRatio: devicePixelRatio,
  );
  final finishIcon = await PlaybackMapAssets.markerForKind(
    PlaybackMarkerKind.finish,
    devicePixelRatio: devicePixelRatio,
  );
  final stopIcon = await PlaybackMapAssets.markerForKind(
    PlaybackMarkerKind.stop,
    devicePixelRatio: devicePixelRatio,
  );

  bool nearVehicle(double lat, double lng) {
    final v = vehiclePoint;
    if (v == null) return false;
    const eps = 0.00008;
    return (v.latitude - lat).abs() < eps && (v.longitude - lng).abs() < eps;
  }

  VoidCallback? tapHandler(PlaybackMarkerTap tap) =>
      onMarkerTap == null ? null : () => onMarkerTap(tap);

  final startPos = LatLng(trail.first.latitude, trail.first.longitude);
  final markerTimeFmt = DateFormat('dd MMM, HH:mm');
  // Always show start/end (even when vehicle sits on them). Vehicle marker
  // uses a higher zIndex so it remains visible on top when coincident.
  final startIdx =
      playback.isEmpty ? 0 : indexForTimestamp(playback, trail.first.timestamp);
  final startPoint = trail.first;
  markers.add(
    Marker(
      markerId: const MarkerId('start'),
      position: startPos,
      icon: startIcon,
      infoWindow: InfoWindow(
        title:
            'Start · ${markerTimeFmt.format(startPoint.timestamp.toLocal())}',
        snippet: playbackAddressLine(startPoint),
      ),
      zIndexInt: 1,
      onTap: tapHandler(PlaybackMarkerTap(
        playbackIndex: startIdx,
        eventType: 'Start',
      )),
    ),
  );

  final endPos = LatLng(trail.last.latitude, trail.last.longitude);
  final endIdx =
      playback.isEmpty ? 0 : indexForTimestamp(playback, trail.last.timestamp);
  final endPoint = trail.last;
  markers.add(
    Marker(
      markerId: const MarkerId('end'),
      position: endPos,
      icon: finishIcon,
      infoWindow: InfoWindow(
        title: 'End · ${markerTimeFmt.format(endPoint.timestamp.toLocal())}',
        snippet: playbackAddressLine(endPoint),
      ),
      zIndexInt: 1,
      onTap: tapHandler(PlaybackMarkerTap(
        playbackIndex: endIdx,
        eventType: 'End',
      )),
    ),
  );

  for (var i = 0; i < stops.length; i++) {
    final s = stops[i];
    if (nearVehicle(s.latitude, s.longitude)) continue;
    final stopIdx =
        playback.isEmpty ? 0 : indexForTimestamp(playback, s.startTime);
    markers.add(
      Marker(
        markerId: MarkerId('stop_$i'),
        position: LatLng(s.latitude, s.longitude),
        icon: stopIcon,
        infoWindow: InfoWindow(
          title: s.durationMinutes >= 120 ? 'Parking' : 'Stop',
          snippet: [
            playbackAddressPrimary(
              s.address,
              lat: s.latitude,
              lng: s.longitude,
            ),
            if (playbackNearbyLine(s.address) case final nearby?) nearby,
            if (playbackAddressSecondary(s.address) case final locality?)
              locality,
            formatStopWindow(startTime: s.startTime, endTime: s.endTime),
            'Duration: ${formatDurationMinutesCompact(s.durationMinutes)}',
          ].join('\n'),
        ),
        zIndexInt: 2,
        onTap: tapHandler(PlaybackMarkerTap(
          playbackIndex: stopIdx,
          eventType: 'Stop',
          stopDurationMinutes: s.durationMinutes,
        )),
      ),
    );
  }

  for (var i = 0; i < events.length; i++) {
    final e = events[i];
    if (e.latitude == null || e.longitude == null) continue;
    if (nearVehicle(e.latitude!, e.longitude!)) continue;
    final evtIdx = playback.isEmpty ? 0 : indexForTimestamp(playback, e.time);
    markers.add(
      Marker(
        markerId: MarkerId('evt_$i'),
        position: LatLng(e.latitude!, e.longitude!),
        icon: await PlaybackMapAssets.markerForEvent(
          e,
          devicePixelRatio: devicePixelRatio,
        ),
        infoWindow: InfoWindow(title: e.type, snippet: e.label),
        zIndexInt: 3,
        onTap: tapHandler(PlaybackMarkerTap(
          playbackIndex: evtIdx,
          eventType: e.type,
          eventLabel: e.label,
        )),
      ),
    );
  }

  return markers;
}

/// Full marker set (static + vehicle) — used for scrub / load.
Future<Set<Marker>> buildPlaybackMarkers({
  required List<HistoryReplayPoint> trail,
  required List<HistoryReplayPoint> playback,
  required int playbackIndex,
  required List<TripStop> stops,
  required List<TripEvent> events,
  required BitmapDescriptor vehicleIcon,
  double devicePixelRatio = 2,
  PlaybackMarkerTapCallback? onMarkerTap,
}) async {
  HistoryReplayPoint? vehiclePoint;
  if (playback.isNotEmpty) {
    vehiclePoint = playback[playbackIndex.clamp(0, playback.length - 1)];
  }
  final markers = await buildPlaybackStaticMarkers(
    trail: trail,
    playback: playback,
    stops: stops,
    events: events,
    devicePixelRatio: devicePixelRatio,
    vehiclePoint: vehiclePoint,
    onMarkerTap: onMarkerTap,
  );
  if (vehiclePoint != null) {
    markers
        .add(buildVehicleMarker(point: vehiclePoint, vehicleIcon: vehicleIcon));
  }
  return markers;
}
