import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_helpers.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_map_builder.dart';

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

  test('stopSegmentRange is stop N → next stop (last → trip end)', () {
    final playback = [
      for (var m = 0; m <= 40; m += 10)
        HistoryReplayPoint(
          timestamp: DateTime.parse(
              '2026-07-22T10:${m.toString().padLeft(2, '0')}:00Z'),
          latitude: m.toDouble(),
          longitude: 0,
          speedKmh: 0,
        ),
    ];
    final stops = [
      TripStop(
        startTime: DateTime.parse('2026-07-22T10:00:00Z'),
        endTime: DateTime.parse('2026-07-22T10:05:00Z'),
        latitude: 0,
        longitude: 0,
        durationMinutes: 5,
      ),
      TripStop(
        startTime: DateTime.parse('2026-07-22T10:20:00Z'),
        endTime: DateTime.parse('2026-07-22T10:25:00Z'),
        latitude: 20,
        longitude: 0,
        durationMinutes: 5,
      ),
      TripStop(
        startTime: DateTime.parse('2026-07-22T10:30:00Z'),
        endTime: DateTime.parse('2026-07-22T10:35:00Z'),
        latitude: 30,
        longitude: 0,
        durationMinutes: 5,
      ),
    ];
    final first = stopSegmentRange(
      playback: playback,
      stops: stops,
      stopIndex: 0,
    )!;
    expect(first.startIndex, 0);
    expect(first.endIndex, indexForTimestamp(playback, stops[1].startTime));

    final last = stopSegmentRange(
      playback: playback,
      stops: stops,
      stopIndex: 2,
    )!;
    expect(last.startIndex, indexForTimestamp(playback, stops[2].startTime));
    expect(last.endIndex, playback.length - 1);
  });

  test('isStopVisited turns true at/after stop start', () {
    final stop = TripStop(
      startTime: DateTime.parse('2026-07-22T10:10:00Z'),
      endTime: DateTime.parse('2026-07-22T10:15:00Z'),
      latitude: 1,
      longitude: 1,
      durationMinutes: 5,
    );
    expect(
        isStopVisited(stop, DateTime.parse('2026-07-22T10:09:00Z')), isFalse);
    expect(isStopVisited(stop, DateTime.parse('2026-07-22T10:10:00Z')), isTrue);
    expect(isStopVisited(stop, DateTime.parse('2026-07-22T10:20:00Z')), isTrue);
  });

  test('lowerBoundPlaybackIndex finds first index not before time', () {
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
      HistoryReplayPoint(
        timestamp: DateTime.parse('2026-07-22T10:20:00Z'),
        latitude: 2,
        longitude: 2,
        speedKmh: 0,
      ),
    ];
    final idx = lowerBoundPlaybackIndex(
      playback,
      DateTime.parse('2026-07-22T10:11:00Z'),
    );
    expect(idx, 2);
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
    expect(
        segments.any((s) => s.kind == PlaybackSegmentKind.overspeed), isTrue);
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

  test('PlaybackTripMetrics prefers selected trip window and distance', () {
    final start = DateTime.parse('2026-07-22T09:00:00Z');
    final end = DateTime.parse('2026-07-22T10:00:00Z');
    final trip = GpsTrip(
      vehicleId: 1,
      startTime: start,
      endTime: end,
      distanceKm: 12.5,
      avgSpeedKmh: 40,
      maxSpeedKmh: 70,
      durationMinutes: 55,
      startAddress: 'Depot',
      endAddress: 'Yard',
    );
    final bundle = HistoryReplayBundle(
      route: [
        HistoryReplayPoint(
          timestamp: start,
          latitude: 24.86,
          longitude: 67.0,
          speedKmh: 10,
        ),
        HistoryReplayPoint(
          timestamp: end,
          latitude: 24.9,
          longitude: 67.05,
          speedKmh: 20,
        ),
      ],
      playback: const [],
      mileageKm: 99,
      summary: const TripReplaySummary(
        distanceKm: 99,
        drivingMinutes: 200,
        avgSpeedKmh: 10,
        maxSpeedKmh: 10,
      ),
      stops: [
        TripStop(
          startTime: start.add(const Duration(minutes: 20)),
          endTime: start.add(const Duration(minutes: 25)),
          latitude: 24.87,
          longitude: 67.01,
          durationMinutes: 5,
        ),
      ],
    );

    final metrics = PlaybackTripMetrics.resolve(
      bundle: bundle,
      selectedTrip: trip,
      stops: bundle.stops,
      rangeFrom: DateTime.parse('2026-07-01T00:00:00Z'),
      rangeTo: DateTime.parse('2026-07-31T00:00:00Z'),
    );

    expect(metrics.startTime, start);
    expect(metrics.endTime, end);
    expect(metrics.distanceKm, 12.5);
    expect(metrics.stopCount, 1);
    expect(metrics.hasDistance, isTrue);
    expect(metrics.distanceLabel, isNot(contains('99')));
  });

  test('PlaybackTripMetrics uses placeholders when speeds missing', () {
    final metrics = PlaybackTripMetrics.resolve(
      bundle: const HistoryReplayBundle(route: [], playback: []),
      selectedTrip: GpsTrip(
        vehicleId: 1,
        startTime: DateTime.parse('2026-07-22T09:00:00Z'),
        endTime: DateTime.parse('2026-07-22T09:30:00Z'),
        distanceKm: 0.05,
        avgSpeedKmh: 0,
        maxSpeedKmh: 0,
        durationMinutes: 30,
        status: 'Stop',
      ),
    );
    expect(metrics.avgSpeedLabel, '—');
    expect(metrics.maxSpeedLabel, '—');
  });

  test('PlaybackStats resolves from bundle and events', () {
    final bundle = HistoryReplayBundle.fromJson({
      'route': [],
      'playback': [
        {
          'timestamp': '2026-07-22T10:01:00Z',
          'latitude': 24.87,
          'longitude': 67.01,
          'speedKmh': 20,
        },
      ],
      'events': [
        {'time': '2026-07-22T10:04:00Z', 'type': 'overspeed'},
        {'time': '2026-07-22T10:05:00Z', 'type': 'sos'},
        {'time': '2026-07-22T10:06:00Z', 'type': 'fuel_drop'},
        {'time': '2026-07-22T10:07:00Z', 'type': 'ignition_off'},
      ],
      'summary': {
        'distanceKm': 18.4,
        'drivingMinutes': 138,
        'avgSpeedKmh': 44,
        'maxSpeedKmh': 92,
      },
    });
    final stats = PlaybackStats.fromBundle(bundle);
    expect(stats.distanceKm, 18.4);
    expect(stats.overspeedCount, 1);
    expect(stats.sosCount, 1);
    expect(stats.fuelEvents, 1);
    expect(stats.ignitionEvents, 1);
  });

  test('TripStop.fromJson prefers latitude/longitude then lat/lon', () {
    final fromApi = TripStop.fromJson({
      'startTime': '2026-07-22T10:00:00Z',
      'endTime': '2026-07-22T10:15:00Z',
      'latitude': 32.2554,
      'longitude': 74.6482,
      'durationMinutes': 15,
      'address': 'Near Main Rd',
    });
    expect(fromApi.latitude, closeTo(32.2554, 0.0001));
    expect(fromApi.longitude, closeTo(74.6482, 0.0001));

    final legacy = TripStop.fromJson({
      'startTime': '2026-07-22T10:00:00Z',
      'endTime': '2026-07-22T10:15:00Z',
      'lat': 32.1,
      'lon': 74.2,
      'durationMinutes': 15,
    });
    expect(legacy.latitude, closeTo(32.1, 0.0001));
    expect(legacy.longitude, closeTo(74.2, 0.0001));
  });

  test('isValidPlaybackCoord rejects null island', () {
    expect(isValidPlaybackCoord(0, 0), isFalse);
    expect(isValidPlaybackCoord(32.25, 74.64), isTrue);
  });

  test(
      'buildPlaybackPolylines is fully green at start, blue grows with progress',
      () {
    final trail = List.generate(
      5,
      (i) => HistoryReplayPoint(
        timestamp: DateTime.utc(2026, 7, 22, 10, i),
        latitude: 24.86 + i * 0.01,
        longitude: 67.0 + i * 0.01,
        speedKmh: 40,
      ),
    );

    final atStart = buildPlaybackPolylines(
      trail: trail,
      trailIndex: 0,
      stops: const [],
    );
    expect(atStart.length, 1);
    expect(atStart.single.polylineId.value, 'remaining_route');
    expect(atStart.single.color, kPlaybackRemainingColor);
    expect(atStart.single.points.length, 5);

    final mid = buildPlaybackPolylines(
      trail: trail,
      trailIndex: 2,
      stops: const [],
    );
    final byId = {for (final p in mid) p.polylineId.value: p};
    expect(byId.keys, containsAll(['remaining_route', 'travelled_route']));
    expect(byId['travelled_route']!.color, kPlaybackTravelledColor);
    expect(byId['remaining_route']!.color, kPlaybackRemainingColor);
    expect(byId['travelled_route']!.points.length, 3);
    expect(byId['remaining_route']!.points.length, 3);

    final atEnd = buildPlaybackPolylines(
      trail: trail,
      trailIndex: 4,
      stops: const [],
    );
    expect(atEnd.length, 1);
    expect(atEnd.single.polylineId.value, 'travelled_route');
    expect(atEnd.single.color, kPlaybackTravelledColor);
    expect(atEnd.single.points.length, 5);
  });
}
