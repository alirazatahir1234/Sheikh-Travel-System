import '../domain/fleet_models.dart';

/// Minimum distance to treat a GPS segment as a moving trip (matches backend).
const minMovingTripDistanceKm = 0.2;

/// Motion vs idle segments — not the same as Completed/Active lifecycle status.
enum TripListFilter { moving, stops, all }

bool isMovingTrip(GpsTrip t) => t.distanceKm >= minMovingTripDistanceKm;

bool isStopTrip(GpsTrip t) =>
    t.distanceKm < minMovingTripDistanceKm ||
    (t.status ?? '').toLowerCase() == 'stop';

List<GpsTrip> filterTrips(List<GpsTrip> trips, TripListFilter filter) {
  switch (filter) {
    case TripListFilter.moving:
      return trips.where(isMovingTrip).toList();
    case TripListFilter.stops:
      return trips.where(isStopTrip).toList();
    case TripListFilter.all:
      return trips;
  }
}

String tripFilterCountLabel({
  required int visible,
  required int total,
  required TripListFilter filter,
}) {
  final kind = switch (filter) {
    TripListFilter.moving => 'motion trips',
    TripListFilter.stops => 'stops',
    TripListFilter.all => 'trips',
  };
  if (filter == TripListFilter.all || visible == total) {
    return '$total $kind';
  }
  return 'Showing $visible of $total ($kind)';
}

/// Short place label from a reverse-geocoded address (first meaningful segment).
String shortPlaceLabel(String? address, {String fallback = ''}) {
  final raw = (address ?? '').trim();
  if (raw.isEmpty) return fallback;
  final parts = raw
      .split(',')
      .map((e) => e.trim())
      .where((e) => e.isNotEmpty)
      .toList();
  if (parts.isEmpty) return fallback;
  if (parts.length >= 2 && parts.first.length <= 3) {
    return parts[1];
  }
  return parts.first;
}

String _coordLabel(double? lat, double? lng) {
  if (lat == null || lng == null || (lat == 0 && lng == 0)) return '';
  return '${lat.toStringAsFixed(5)}, ${lng.toStringAsFixed(5)}';
}

/// List-card route line: place names, round-trip, or GPS coords — never bare "Unknown".
({String primary, String? secondary, bool addressMissing}) formatTripRoute(GpsTrip t) {
  final startName = shortPlaceLabel(t.startAddress);
  final endName = shortPlaceLabel(t.endAddress);
  final startCoord = _coordLabel(t.startLatitude, t.startLongitude);
  final endCoord = _coordLabel(t.endLatitude, t.endLongitude);

  final start = startName.isNotEmpty
      ? startName
      : (startCoord.isNotEmpty ? startCoord : '');
  final end = endName.isNotEmpty
      ? endName
      : (endCoord.isNotEmpty ? endCoord : '');

  final addressMissing =
      startName.isEmpty || endName.isEmpty;

  if (start.isEmpty && end.isEmpty) {
    return (
      primary: 'Location unavailable',
      secondary: t.hasStartCoords || t.hasEndCoords
          ? 'GPS coordinates available'
          : 'No GPS position for this trip',
      addressMissing: true,
    );
  }

  if (isStopTrip(t)) {
    return (
      primary: start.isNotEmpty ? start : end,
      secondary: addressMissing && startCoord.isNotEmpty
          ? 'GPS: $startCoord'
          : null,
      addressMissing: addressMissing,
    );
  }

  // Same road / same label → round trip
  if (start.isNotEmpty &&
      end.isNotEmpty &&
      start.toLowerCase() == end.toLowerCase()) {
    return (
      primary: start,
      secondary: addressMissing && startCoord.isNotEmpty
          ? '↻ Round trip · GPS: $startCoord'
          : '↻ Round trip',
      addressMissing: addressMissing,
    );
  }

  if (start.isEmpty) {
    return (
      primary: '→ $end',
      secondary: addressMissing
          ? (startCoord.isNotEmpty
              ? 'Start GPS: $startCoord'
              : 'Start address unavailable')
          : null,
      addressMissing: addressMissing,
    );
  }

  if (end.isEmpty) {
    return (
      primary: '$start →',
      secondary: addressMissing
          ? (endCoord.isNotEmpty
              ? 'End GPS: $endCoord'
              : 'End address unavailable')
          : null,
      addressMissing: addressMissing,
    );
  }

  return (
    primary: '$start → $end',
    secondary: addressMissing && (startCoord.isNotEmpty || endCoord.isNotEmpty)
        ? 'Address lookup incomplete'
        : null,
    addressMissing: addressMissing,
  );
}

String formatTripAvgSpeed(GpsTrip t) {
  if (!isMovingTrip(t)) return '—';
  return '${displayAvgSpeedKmh(t).toStringAsFixed(0)} km/h';
}

String formatTripMaxSpeed(GpsTrip t) {
  if (!isMovingTrip(t)) return '—';
  return '${displayMaxSpeedKmh(t).toStringAsFixed(0)} km/h';
}

double displayAvgSpeedKmh(GpsTrip t) {
  if (!isMovingTrip(t)) return 0;
  final hours = t.durationMinutes <= 0 ? 1 / 60.0 : t.durationMinutes / 60.0;
  final computed = t.distanceKm / hours;
  if (computed > 0) return computed;
  return t.avgSpeedKmh;
}

double displayMaxSpeedKmh(GpsTrip t) {
  if (!isMovingTrip(t)) return 0;
  final avg = displayAvgSpeedKmh(t);
  final max = t.maxSpeedKmh;
  return max >= avg ? max : avg;
}

String tripStatusLabel(GpsTrip t) {
  if (isStopTrip(t)) return 'Stop';
  final s = (t.status ?? '').trim();
  if (s.isEmpty || s.toLowerCase() == 'completed') return 'Completed';
  return s;
}

/// Short trip id for cards (last segment of composite tripKey).
String tripIdLabel(GpsTrip t) {
  final key = (t.tripKey ?? '').trim();
  if (key.isEmpty) return '';
  final parts = key.split(RegExp(r'[:|_-]'));
  final tail = parts.isNotEmpty ? parts.last : key;
  if (tail.length <= 10) return 'Trip $tail';
  return 'Trip ${tail.substring(tail.length - 8)}';
}
