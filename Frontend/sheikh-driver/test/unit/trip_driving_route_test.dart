import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/trips/domain/trip_driving_route.dart';

void main() {
  test('decodes the Google polyline sample into road points', () {
    const encoded = r'_p~iF~ps|U_ulLnnqC_mqNvxq`@';
    final points = decodeEncodedPolyline(encoded);
    expect(points, hasLength(3));
    expect(points[0].latitude, closeTo(38.5, 0.00001));
    expect(points[0].longitude, closeTo(-120.2, 0.00001));
    expect(points[1].latitude, closeTo(40.7, 0.00001));
    expect(points[1].longitude, closeTo(-120.95, 0.00001));
    expect(points[2].latitude, closeTo(43.252, 0.00001));
    expect(points[2].longitude, closeTo(-126.453, 0.00001));
  });

  test('cache key rounds nearby coordinates to the same bucket', () {
    final a = tripRouteCacheKey(33.68441, 73.04789, 34.01514, 71.52491);
    final b = tripRouteCacheKey(33.68444, 73.04786, 34.01511, 71.52494);
    expect(a, b);
    expect(a, '33.6844,73.0479|34.0151,71.5249');
  });

  test('formats duration and distance only from supplied numbers', () {
    expect(
      formatDrivingRouteSummary(
        distanceMeters: 178000,
        durationSeconds: 2 * 3600 + 10 * 60,
      ),
      '2 h 10 min · 178 km',
    );
    expect(
      formatDrivingRouteSummary(distanceMeters: 0, durationSeconds: 0),
      isNull,
    );
    expect(
      formatDrivingRouteSummary(distanceMeters: 4500, durationSeconds: 600),
      '10 min · 4.5 km',
    );
  });

  test('parses planned-route data and ignores a missing polyline', () {
    final route = parsePlannedRouteData({
      'distanceMeters': 178000,
      'durationSeconds': 7800,
      'encodedPolyline': r'_p~iF~ps|U_ulLnnqC_mqNvxq`@',
    });
    expect(route, isNotNull);
    expect(route!.points, hasLength(3));
    expect(route.summary, '2 h 10 min · 178 km');
    expect(parsePlannedRouteData({'distanceMeters': 10}), isNull);
  });

  test('parses a Routes computeRoutes payload', () {
    final route = parseRoutesComputePayload({
      'routes': [
        {
          'distanceMeters': 1200,
          'duration': '90s',
          'polyline': {'encodedPolyline': r'_p~iF~ps|U_ulLnnqC_mqNvxq`@'},
        },
      ],
    });
    expect(route, isNotNull);
    expect(route!.distanceMeters, 1200);
    expect(route.durationSeconds, 90);
    expect(route.summary, '2 min · 1.2 km');
    expect(parseRoutesComputePayload({'routes': []}), isNull);
  });
}
