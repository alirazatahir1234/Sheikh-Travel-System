import 'dart:io';
import 'package:flutter/foundation.dart';
import 'package:url_launcher/url_launcher.dart';

/// External turn-by-turn — same pattern as ERP trip detail `googleDirectionsUrl`.
class ExternalMapsLauncher {
  ExternalMapsLauncher._();

  static Future<bool> openGoogleMaps({
    required double? originLat,
    required double? originLng,
    required double destLat,
    required double destLng,
    String? destLabel,
    String? fallbackUrl,
  }) async {
    if (fallbackUrl != null && fallbackUrl.isNotEmpty) {
      final ok = await _launch(Uri.parse(fallbackUrl));
      if (ok) return true;
    }

    final dest = '$destLat,$destLng';
    final origin = (originLat != null && originLng != null)
        ? '$originLat,$originLng'
        : null;

    final web = Uri.parse(
      origin == null
          ? 'https://www.google.com/maps/dir/?api=1&destination=$dest&travelmode=driving'
          : 'https://www.google.com/maps/dir/?api=1&origin=$origin&destination=$dest&travelmode=driving',
    );

    if (!kIsWeb && Platform.isAndroid) {
      final nav = Uri.parse(
        origin == null
            ? 'google.navigation:q=$dest&mode=d'
            : 'google.navigation:q=$dest&mode=d',
      );
      if (await _launch(nav)) return true;
    }

    if (!kIsWeb && Platform.isIOS) {
      final app = Uri.parse(
        origin == null
            ? 'comgooglemaps://?daddr=$dest&directionsmode=driving'
            : 'comgooglemaps://?saddr=$origin&daddr=$dest&directionsmode=driving',
      );
      if (await _launch(app)) return true;
    }

    return _launch(web);
  }

  static Future<bool> openAppleMaps({
    required double destLat,
    required double destLng,
    double? originLat,
    double? originLng,
  }) async {
    final dest = '$destLat,$destLng';
    final uri = (originLat != null && originLng != null)
        ? Uri.parse(
            'https://maps.apple.com/?saddr=$originLat,$originLng&daddr=$dest&dirflg=d',
          )
        : Uri.parse('https://maps.apple.com/?daddr=$dest&dirflg=d');
    return _launch(uri);
  }

  static Future<bool> openWaze({
    required double destLat,
    required double destLng,
  }) async {
    final app = Uri.parse('waze://?ll=$destLat,$destLng&navigate=yes');
    if (await _launch(app)) return true;
    return _launch(Uri.parse(
      'https://waze.com/ul?ll=$destLat%2C$destLng&navigate=yes',
    ));
  }

  static Future<bool> _launch(Uri uri) async {
    try {
      if (await canLaunchUrl(uri)) {
        return launchUrl(uri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {}
    return false;
  }
}
