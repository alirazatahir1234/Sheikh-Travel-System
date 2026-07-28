import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_helpers.dart';

void main() {
  test('HistoryReplayBundle parses stops events summary', () {
    final bundle = HistoryReplayBundle.fromJson({
      'route': [
        {
          'timestamp': '2026-07-22T10:00:00Z',
          'latitude': 24.86,
          'longitude': 67.00,
          'speedKmh': 10,
        },
        {
          'timestamp': '2026-07-22T10:05:00Z',
          'latitude': 24.87,
          'longitude': 67.01,
          'speedKmh': 95,
        },
      ],
      'playback': [
        {
          'timestamp': '2026-07-22T10:01:00Z',
          'latitude': 24.87,
          'longitude': 67.01,
          'speedKmh': 20,
        },
      ],
      'stops': [
        {
          'startTime': '2026-07-22T10:02:00Z',
          'endTime': '2026-07-22T10:03:00Z',
          'latitude': 24.87,
          'longitude': 67.01,
          'durationMinutes': 1,
        },
      ],
      'events': [
        {
          'time': '2026-07-22T10:04:00Z',
          'type': 'overspeed',
          'latitude': 24.87,
          'longitude': 67.01,
        },
      ],
      'summary': {
        'distanceKm': 18.4,
        'drivingMinutes': 138,
        'avgSpeedKmh': 44,
        'maxSpeedKmh': 92,
      },
      'statistics': {
        'distanceKm': 18.4,
        'drivingMinutes': 138,
        'idleMinutes': 23,
        'avgSpeedKmh': 44,
        'maxSpeedKmh': 92,
        'stopCount': 5,
        'overspeedCount': 2,
      },
      'mileageKm': 12.5,
      'vehicle': {
        'vehicleId': 1,
        'vehicleName': 'Test',
        'plateNumber': 'ABC',
        'gpsDeviceId': 9,
      },
    });
    expect(bundle.points, hasLength(1));
    expect(bundle.stops, hasLength(1));
    expect(bundle.events, hasLength(1));
    expect(bundle.summary?.maxSpeedKmh, 92);
    expect(bundle.statistics?.idleMinutes, 23);
    expect(bundle.vehicle?.plateNumber, 'ABC');
  });

  test('indexForTimestamp picks nearest playback point', () {
    final playback = [
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:00:00Z'),
        latitude: 0,
        longitude: 0,
        speedKmh: 0,
      ),
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:10:00Z'),
        latitude: 1,
        longitude: 1,
        speedKmh: 0,
      ),
    ];
    final idx = indexForTimestamp(
      playback,
      DateTime.parse('2026-07-22T10:09:00Z'),
    );
    expect(idx, 1);
  });

  test('buildSpeedSegments classifies overspeed', () {
    final points = [
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:00:00Z'),
        latitude: 0,
        longitude: 0,
        speedKmh: 40,
      ),
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:01:00Z'),
        latitude: 0.01,
        longitude: 0,
        speedKmh: 100,
      ),
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:02:00Z'),
        latitude: 0.02,
        longitude: 0,
        speedKmh: 100,
      ),
    ];
    final segments = buildSpeedSegments(points);
    expect(segments.any((s) => s.kind == PlaybackSegmentKind.overspeed), isTrue);
  });

  test('buildCsv includes header and rows', () {
    final csv = buildCsv([
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:00:00Z'),
        latitude: 24.86,
        longitude: 67.00,
        speedKmh: 12,
        heading: 42,
        ignition: true,
        address: 'Karachi',
      ),
    ]);
    expect(csv.contains('timestamp,latitude,longitude'), isTrue);
    expect(csv.contains('Karachi'), isTrue);
  });
}
