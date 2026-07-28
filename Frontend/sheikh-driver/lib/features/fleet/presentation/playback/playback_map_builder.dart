import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';
import 'dart:typed_data';
import 'dart:ui' as ui;

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

class PlaybackMapAssets {
  static final Map<PlaybackMarkerKind, BitmapDescriptor> _iconCache = {};

  static Future<BitmapDescriptor> vehicleIcon() async {
    return markerForKind(PlaybackMarkerKind.vehicle);
  }

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
        return const Color(0xFF2563EB);
      case PlaybackMarkerKind.eventFallback:
        return const Color(0xFF64748B);
    }
  }

  static String shortLabelForKind(PlaybackMarkerKind kind) {
    switch (kind) {
      case PlaybackMarkerKind.start:
        return 'S';
      case PlaybackMarkerKind.finish:
        return 'F';
      case PlaybackMarkerKind.stop:
        return 'P';
      case PlaybackMarkerKind.overspeed:
        return 'SPD';
      case PlaybackMarkerKind.ignitionOn:
        return 'ON';
      case PlaybackMarkerKind.ignitionOff:
        return 'OFF';
      case PlaybackMarkerKind.geofence:
        return 'GEO';
      case PlaybackMarkerKind.fuel:
        return 'FUEL';
      case PlaybackMarkerKind.sos:
        return 'SOS';
      case PlaybackMarkerKind.vehicle:
      case PlaybackMarkerKind.eventFallback:
        return '';
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

  static Future<BitmapDescriptor> markerForEvent(TripEvent event) {
    return markerForKind(markerKindForEvent(event));
  }

  static Future<BitmapDescriptor> markerForKind(PlaybackMarkerKind kind) async {
    final cached = _iconCache[kind];
    if (cached != null) return cached;

    try {
      final bytes = kind == PlaybackMarkerKind.vehicle
          ? await _vehicleArrowBytes(colorForKind(kind))
          : await _pinBytes(
              background: colorForKind(kind),
              label: shortLabelForKind(kind),
            );
      final icon = BitmapDescriptor.bytes(bytes);
      _iconCache[kind] = icon;
      return icon;
    } catch (_) {
      final fallback = _fallbackMarker(kind);
      _iconCache[kind] = fallback;
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
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueOrange);
      case PlaybackMarkerKind.ignitionOn:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen);
      case PlaybackMarkerKind.ignitionOff:
      case PlaybackMarkerKind.eventFallback:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueViolet);
      case PlaybackMarkerKind.geofence:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueMagenta);
      case PlaybackMarkerKind.vehicle:
        return BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure);
    }
  }

  static Future<Uint8List> _vehicleArrowBytes(Color color) async {
    const size = 96.0;
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    final center = const Offset(size / 2, size / 2);

    final ringPaint = Paint()..color = Colors.white;
    canvas.drawCircle(center, 28, ringPaint);
    final ringBorder = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5;
    canvas.drawCircle(center, 28, ringBorder);

    final path = Path()
      ..moveTo(size / 2, 14)
      ..lineTo(size / 2 + 18, size / 2 + 18)
      ..lineTo(size / 2, size / 2 + 10)
      ..lineTo(size / 2 - 18, size / 2 + 18)
      ..close();

    canvas.drawPath(path, Paint()..color = color);

    final image = await recorder.endRecording().toImage(size.toInt(), size.toInt());
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }

  static Future<Uint8List> _pinBytes({
    required Color background,
    required String label,
  }) async {
    const width = 96.0;
    const height = 112.0;
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);

    final path = Path()
      ..moveTo(width / 2, height)
      ..quadraticBezierTo(width / 2 - 8, height - 20, width / 2 - 24, height - 38)
      ..arcToPoint(
        Offset(width / 2 + 24, height - 38),
        radius: const Radius.circular(26),
        clockwise: false,
      )
      ..quadraticBezierTo(width / 2 + 8, height - 20, width / 2, height)
      ..close();

    canvas.drawPath(path, Paint()..color = background);
    canvas.drawPath(
      path,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4,
    );

    if (label.isNotEmpty) {
      final textPainter = TextPainter(
        text: TextSpan(
          text: label,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 15,
            fontWeight: FontWeight.w900,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout(maxWidth: 52);
      textPainter.paint(
        canvas,
        Offset((width - textPainter.width) / 2, (height - 38 - textPainter.height) / 2),
      );
    }

    final image = await recorder.endRecording().toImage(width.toInt(), height.toInt());
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    return data!.buffer.asUint8List();
  }
}

Color segmentColor(PlaybackSegmentKind kind) {
  switch (kind) {
    case PlaybackSegmentKind.overspeed:
      return const Color(0xFFDC2626);
    case PlaybackSegmentKind.ignitionOff:
      return const Color(0xFF0EA5E9);
    case PlaybackSegmentKind.stop:
      return const Color(0xFFF59E0B);
    case PlaybackSegmentKind.normal:
      return const Color(0xFF2563EB);
  }
}

Set<Polyline> buildPlaybackPolylines({
  required List<HistoryReplayPoint> trail,
  required int trailIndex,
  required List<TripStop> stops,
}) {
  if (trail.length < 2) return {};

  final completed = trail.sublist(0, trailIndex.clamp(1, trail.length));
  final remaining = trail.sublist(trailIndex.clamp(0, trail.length - 1));

  final polylines = <Polyline>{};

  if (remaining.length >= 2) {
    polylines.add(
      Polyline(
        polylineId: const PolylineId('remaining'),
        color: AppColors.textMuted.withValues(alpha: 0.55),
        width: 4,
        patterns: [PatternItem.dot, PatternItem.gap(8)],
        points: [
          for (final p in remaining) LatLng(p.latitude, p.longitude),
        ],
      ),
    );
  }

  final segments = buildSpeedSegments(completed, stops: stops);
  for (var i = 0; i < segments.length; i++) {
    final s = segments[i];
    if (s.points.length < 2) continue;
    polylines.add(
      Polyline(
        polylineId: PolylineId('done_$i'),
        color: segmentColor(s.kind),
        width: 5,
        geodesic: true,
        points: [
          for (final p in s.points) LatLng(p.latitude, p.longitude),
        ],
      ),
    );
  }

  if (trail.length >= 3) {
    final currentStart = (trailIndex - 1).clamp(0, trail.length - 1);
    final currentEnd = (trailIndex + 1).clamp(0, trail.length - 1);
    if (currentEnd > currentStart) {
      polylines.add(
        Polyline(
          polylineId: const PolylineId('current_focus'),
          color: const Color(0xFF22C55E),
          width: 7,
          zIndex: 20,
          points: [
            for (var i = currentStart; i <= currentEnd; i++)
              LatLng(trail[i].latitude, trail[i].longitude),
          ],
        ),
      );
    }
  }

  return polylines;
}

Future<Set<Marker>> buildPlaybackMarkers({
  required List<HistoryReplayPoint> trail,
  required List<HistoryReplayPoint> playback,
  required int playbackIndex,
  required List<TripStop> stops,
  required List<TripEvent> events,
  required BitmapDescriptor vehicleIcon,
}) async {
  final markers = <Marker>{};
  if (trail.isEmpty) return markers;
  final startIcon = await PlaybackMapAssets.markerForKind(PlaybackMarkerKind.start);
  final finishIcon = await PlaybackMapAssets.markerForKind(PlaybackMarkerKind.finish);
  final stopIcon = await PlaybackMapAssets.markerForKind(PlaybackMarkerKind.stop);

  HistoryReplayPoint? vehiclePoint;
  if (playback.isNotEmpty) {
    vehiclePoint = playback[playbackIndex.clamp(0, playback.length - 1)];
  }

  bool nearVehicle(double lat, double lng) {
    final v = vehiclePoint;
    if (v == null) return false;
    // ~9 m — hide static markers that stack under the live playhead.
    const eps = 0.00008;
    return (v.latitude - lat).abs() < eps && (v.longitude - lng).abs() < eps;
  }

  final startPos = LatLng(trail.first.latitude, trail.first.longitude);
  if (!nearVehicle(startPos.latitude, startPos.longitude)) {
    markers.add(
      Marker(
        markerId: const MarkerId('start'),
        position: startPos,
        icon: startIcon,
        infoWindow: const InfoWindow(title: 'Start'),
        zIndexInt: 1,
      ),
    );
  }

  final endPos = LatLng(trail.last.latitude, trail.last.longitude);
  if (!nearVehicle(endPos.latitude, endPos.longitude)) {
    markers.add(
      Marker(
        markerId: const MarkerId('end'),
        position: endPos,
        icon: finishIcon,
        infoWindow: const InfoWindow(title: 'Finish'),
        zIndexInt: 1,
      ),
    );
  }

  for (var i = 0; i < stops.length; i++) {
    final s = stops[i];
    if (nearVehicle(s.latitude, s.longitude)) continue;
    markers.add(
      Marker(
        markerId: MarkerId('stop_$i'),
        position: LatLng(s.latitude, s.longitude),
        icon: stopIcon,
        infoWindow: InfoWindow(
          title: 'Stop ${s.durationMinutes} min',
          snippet: s.address,
        ),
        zIndexInt: 2,
      ),
    );
  }

  for (var i = 0; i < events.length; i++) {
    final e = events[i];
    if (e.latitude == null || e.longitude == null) continue;
    if (nearVehicle(e.latitude!, e.longitude!)) continue;
    markers.add(
      Marker(
        markerId: MarkerId('evt_$i'),
        position: LatLng(e.latitude!, e.longitude!),
        icon: await PlaybackMapAssets.markerForEvent(e),
        infoWindow: InfoWindow(title: e.type, snippet: e.label),
        zIndexInt: 3,
      ),
    );
  }

  if (vehiclePoint != null) {
    markers.add(
      Marker(
        markerId: const MarkerId('vehicle'),
        position: LatLng(vehiclePoint.latitude, vehiclePoint.longitude),
        icon: vehicleIcon,
        rotation: vehiclePoint.heading ?? 0,
        flat: true,
        anchor: const Offset(0.5, 0.5),
        zIndexInt: 10,
      ),
    );
  }

  return markers;
}
