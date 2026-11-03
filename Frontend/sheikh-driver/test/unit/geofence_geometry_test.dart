import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/widgets/geofence_distance.dart';
import 'package:sheikh_go_driver/features/gps_operator/domain/geofence_models.dart';

GpsGeofenceItem _zone({
  String areaType = 'polygon',
  String? geoJson,
  double radius = 0,
}) =>
    GpsGeofenceItem(
      id: 1,
      name: 'Z',
      areaType: areaType,
      centerLat: 31.55,
      centerLng: 74.35,
      radiusMeters: radius,
      geoJson: geoJson,
    );

void main() {
  const square =
      '{"type":"Polygon","coordinates":[[[74.30,31.50],[74.40,31.50],[74.40,31.60],[74.30,31.60],[74.30,31.50]]]}';

  test('polygon and rectangle zones get inside/outside status', () {
    expect(isInsideGeofence(31.55, 74.35, _zone(geoJson: square)), isTrue);
    expect(isInsideGeofence(31.70, 74.35, _zone(geoJson: square)), isFalse);
    expect(
      isInsideGeofence(31.55, 74.35, _zone(areaType: 'rectangle', geoJson: square)),
      isTrue,
    );
  });

  test('Feature-wrapped GeoJSON parses', () {
    final ring = parseGeofenceRing(
      '{"type":"Feature","geometry":$square}',
    );
    expect(ring, hasLength(4));
    expect(ring.first.lat, 31.50);
    expect(ring.first.lng, 74.30);
  });

  test('invalid geometry yields null status instead of "outside"', () {
    expect(isInsideGeofence(31.55, 74.35, _zone(geoJson: '{bad')), isNull);
    expect(
      isInsideGeofence(31.55, 74.35, _zone(geoJson: '{"type":"Polygon","coordinates":[[[1,1],[2,2]]]}')),
      isNull,
    );
    expect(isInsideGeofence(31.55, 74.35, _zone(areaType: 'circle')), isNull);
  });

  test('circle status uses radius', () {
    final c = _zone(areaType: 'circle', radius: 200);
    expect(isInsideGeofence(31.55, 74.35, c), isTrue);
    expect(isInsideGeofence(31.56, 74.35, c), isFalse);
  });

  test('ring → GeoJSON round-trips and closes the ring', () {
    final ring = [
      (lat: 31.5, lng: 74.3),
      (lat: 31.5, lng: 74.4),
      (lat: 31.6, lng: 74.4),
    ];
    final json = geofenceRingToGeoJson(ring);
    expect(json, contains('[74.3,31.5]]]'));
    expect(parseGeofenceRing(json), ring);
  });

  test('client geometry validation mirrors server rules', () {
    expect(validateGeofenceGeometry(circle: true, radiusMeters: 300), isNotNull);
    expect(
      validateGeofenceGeometry(circle: true, hasCenter: true, radiusMeters: 10),
      isNotNull,
    );
    expect(
      validateGeofenceGeometry(circle: true, hasCenter: true, radiusMeters: 300),
      isNull,
    );
    const a = (lat: 31.5, lng: 74.3);
    expect(validateGeofenceGeometry(circle: false, vertices: [a, a, a]), isNotNull);
    expect(
      validateGeofenceGeometry(
        circle: false,
        vertices: [a, (lat: 31.5, lng: 74.4), (lat: 31.6, lng: 74.4)],
      ),
      isNull,
    );
  });

  test('draft carries alert rule flags and vehicle assignments', () {
    final json = const GeofenceDraft(
      name: 'Depot',
      category: 'Depot',
      areaType: 'circle',
      radiusMeters: 300,
      alertOnEnter: true,
      alertOnExit: false,
      vehicleIds: [4, 7],
    ).toJson();
    expect(json['alertOnEnter'], isTrue);
    expect(json['alertOnExit'], isFalse);
    expect(json['vehicleIds'], [4, 7]);
  });

  test('vehicle state parses PascalCase or camelCase and freshness formats', () {
    final s = GeofenceVehicleState.fromJson({
      'GeofenceId': 3,
      'VehicleId': 9,
      'IsInside': true,
      'RegistrationNumber': 'LEA-123',
      'LastPositionUtc': '2026-11-03T06:00:00',
    });
    expect(s.geofenceId, 3);
    expect(s.isInside, isTrue);
    expect(s.vehicleLabel, 'LEA-123');
    expect(
      formatFreshness(s.lastPositionUtc, now: DateTime.utc(2026, 11, 3, 6, 7)),
      '7 min ago',
    );
    expect(formatFreshness(null), 'no position');
  });
}
