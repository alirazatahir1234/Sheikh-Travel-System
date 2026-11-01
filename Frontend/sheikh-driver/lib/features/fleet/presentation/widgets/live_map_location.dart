import '../../data/fleet_api.dart';
import '../../domain/fleet_models.dart';

/// Rounds a coordinate to ~10 m resolution so the geocode cache isn't
/// invalidated by tiny GPS drift between consecutive position updates.
String liveMapCoordKey(double? lat, double? lng) {
  if (lat == null || lng == null) return '';
  // 4 decimal places ≈ ~11 m precision — stable across normal drift
  return '${lat.toStringAsFixed(4)},${lng.toStringAsFixed(4)}';
}

bool _looksLikeRoadSegment(String part) {
  final p = part.toLowerCase();
  if (RegExp(r'\d').hasMatch(part)) return true;
  return p.contains('road') ||
      p.contains('street') ||
      p.contains('avenue') ||
      p.contains('lane') ||
      p.contains('highway') ||
      p.contains('bypass') ||
      p.contains('chowk') ||
      p.contains('boulevard');
}

/// True when [address] is city/province-only (e.g. "Pasrur, Punjab, Pakistan").
bool isLiveMapCoarseAddress(String address) {
  final lower = address.toLowerCase();
  if (lower.contains('tehsil') ||
      lower.contains('district') ||
      lower.contains('division')) {
    return true;
  }
  final trimmed = address.trim();
  if (trimmed.toLowerCase().startsWith('near ') &&
      !RegExp(r'\d').hasMatch(trimmed)) {
    return true;
  }
  final parts = address
      .split(',')
      .map((e) => e.trim())
      .where((e) => e.isNotEmpty)
      .toList();
  if (parts.isEmpty) return true;
  if (parts.any(_looksLikeRoadSegment)) return false;
  return parts.length <= 3;
}

/// True when [address] is essentially raw coordinates (not a street line).
bool isLiveMapCoordsOnlyAddress(String address) {
  final raw = address.trim();
  if (raw.isEmpty) return false;

  // Normalize degree marks / hemisphere letters so "31.52° N, 74.35° E" matches.
  final normalized = raw
      .replaceAll(RegExp(r'[°º]'), '')
      .replaceAll(RegExp(r'\b[NnSs]\b'), '')
      .replaceAll(RegExp(r'\b[EeWw]\b'), '')
      .replaceAll(RegExp(r'\s+'), ' ')
      .trim();

  if (RegExp(r'^-?\d+(\.\d+)?\s*,\s*-?\d+(\.\d+)?$').hasMatch(normalized)) {
    return true;
  }

  // No letters at all and looks like two numbers → treat as coords.
  if (!RegExp(r'[A-Za-z\u0600-\u06FF]').hasMatch(raw) &&
      RegExp(r'-?\d+(\.\d+)?').allMatches(raw).length >= 2) {
    return true;
  }

  return false;
}

bool liveMapAddressNeedsReverseGeocode(String? address) {
  final inline = address?.trim();
  if (inline == null || inline.isEmpty) return true;
  if (isLiveMapCoordsOnlyAddress(inline)) return true;
  if (isLiveMapCoarseAddress(inline)) return true;
  return false;
}

/// Formats lat/lng for secondary display under a resolved street address.
String formatLiveMapCoords(double latitude, double longitude) =>
    '${latitude.toStringAsFixed(5)}, ${longitude.toStringAsFixed(5)}';

class LiveMapLocationDisplay {
  const LiveMapLocationDisplay({
    required this.primary,
    required this.secondary,
    required this.coords,
    required this.isUnavailable,
    required this.isResolving,
  });

  final String? primary;
  final String? secondary;
  final String coords;
  final bool isUnavailable;
  final bool isResolving;
}

/// Builds primary/secondary lines for the live-map Location section.
LiveMapLocationDisplay buildLiveMapLocationDisplay({
  required FleetVehicleLocation vehicle,
  required ReverseGeocodeInfo? info,
  required bool waiting,
}) {
  final coords = formatLiveMapCoords(vehicle.latitude!, vehicle.longitude!);
  final place = info?.nearbyPlaceName?.trim() ?? info?.placeName?.trim();
  var address = info?.primaryAddress?.trim();
  final locality = info?.localityLine?.trim();
  if ((address == null || address.isEmpty) &&
      info?.formattedAddress.trim().isNotEmpty == true) {
    address = info!.formattedAddress.trim();
  }
  if (address != null && isLiveMapCoordsOnlyAddress(address)) address = null;

  final inline = vehicle.address?.trim();
  final inlineOk = inline != null &&
      inline.isNotEmpty &&
      !isLiveMapCoarseAddress(inline) &&
      !isLiveMapCoordsOnlyAddress(inline);

  if ((address == null || address.isEmpty) && inlineOk) {
    address = inline;
  }

  if (waiting && (address == null || address.isEmpty)) {
    return LiveMapLocationDisplay(
      primary: null,
      secondary: 'Resolving exact location…',
      coords: coords,
      isUnavailable: false,
      isResolving: true,
    );
  }

  if (address == null || address.isEmpty) {
    return LiveMapLocationDisplay(
      primary: place != null && place.isNotEmpty ? place : null,
      secondary: 'Address unavailable',
      coords: coords,
      isUnavailable: true,
      isResolving: false,
    );
  }

  String? secondary;
  if (place != null &&
      place.isNotEmpty &&
      !address.toLowerCase().contains(place.toLowerCase())) {
    secondary = place;
  }

  final merged = locality != null && locality.isNotEmpty
      ? (!address.toLowerCase().contains(locality.toLowerCase())
          ? '$address, $locality'
          : address)
      : address;

  return LiveMapLocationDisplay(
    primary: merged,
    secondary: secondary,
    coords: coords,
    isUnavailable: false,
    isResolving: false,
  );
}
