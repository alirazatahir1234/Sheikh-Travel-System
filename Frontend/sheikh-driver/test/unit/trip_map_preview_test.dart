import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:sheikh_go_driver/core/constants/app_theme.dart';
import 'package:sheikh_go_driver/features/trips/domain/trip_model.dart';
import 'package:sheikh_go_driver/features/trips/presentation/widgets/trip_map_preview.dart';

Trip _trip({
  double? pickupLat,
  double? pickupLng,
  double? dropLat,
  double? dropLng,
  String? directionsUrl,
}) =>
    Trip(
      id: 1,
      bookingNumber: 'BK-1',
      customerName: 'Hina',
      routeName: 'Islamabad → Peshawar',
      pickupTime: DateTime.utc(2026, 10, 15, 7, 36),
      status: 3,
      statusName: 'Started',
      totalAmount: 8278.57,
      pickupAddress: 'Islamabad',
      pickupLatitude: pickupLat,
      pickupLongitude: pickupLng,
      dropoffAddress: 'Peshawar',
      dropLatitude: dropLat,
      dropLongitude: dropLng,
      googleDirectionsUrl: directionsUrl,
      lifecycleStatus: 6,
      lifecycleStatusName: 'Arrived at pickup',
      nextActions: const ['Onboard'],
    );

void main() {
  group('tripMapPreviewPoints / camera', () {
    test('returns empty when only city names exist (no lat/lng)', () {
      final t = _trip();
      expect(tripMapPreviewPoints(t), isEmpty);
      expect(t.hasMapPreviewCoords, isFalse);
    });

    test('uses pickup and drop coords without inventing midpoints as markers',
        () {
      final t = _trip(
        pickupLat: 33.6844,
        pickupLng: 73.0479,
        dropLat: 34.0151,
        dropLng: 71.5249,
      );
      final pts = tripMapPreviewPoints(t);
      expect(pts, hasLength(2));
      expect(pts.first, const LatLng(33.6844, 73.0479));
      expect(pts.last, const LatLng(34.0151, 71.5249));
      final cam = tripMapPreviewCamera(t);
      expect(cam.target.latitude, closeTo(33.84975, 0.0001));
      expect(cam.target.longitude, closeTo(72.2864, 0.0001));
    });

    test('single pickup zooms in on that point', () {
      final t = _trip(pickupLat: 31.52, pickupLng: 74.35);
      final pts = tripMapPreviewPoints(t);
      expect(pts, hasLength(1));
      final cam = tripMapPreviewCamera(t);
      expect(cam.target, const LatLng(31.52, 74.35));
      expect(cam.zoom, 13.5);
    });

    test('shows a map from coords without a dart-define maps key', () {
      expect(tripMapPreviewShowsMap(_trip()), isFalse);
      expect(
        tripMapPreviewShowsMap(_trip(pickupLat: 33.6844, pickupLng: 73.0479)),
        isTrue,
      );
    });

    test('ETA label uses the scheduled drop-off and stays empty otherwise', () {
      expect(tripMapPreviewEtaLabel(_trip()), isNull);
      final timed = Trip(
        id: 1,
        bookingNumber: 'BK-1',
        customerName: 'Hina',
        routeName: 'Islamabad → Peshawar',
        pickupTime: DateTime.utc(2026, 10, 15, 7, 36),
        dropoffTime: DateTime.utc(2026, 10, 15, 10, 0),
        status: 3,
        statusName: 'Started',
        totalAmount: 1,
      );
      expect(tripMapPreviewEtaLabel(timed), isNotNull);
      expect(tripMapPreviewEtaLabel(timed), isNot(contains('pending')));
    });
  });

  group('TripMapPreviewCard placeholder', () {
    testWidgets('shows Map unavailable when coords missing', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: Scaffold(
            body: TripMapPreviewCard(
              trip: _trip(directionsUrl: 'https://maps.google.com/?q=test'),
            ),
          ),
        ),
      );
      await tester.pump();

      expect(find.text('Map needs a location'), findsOneWidget);
      expect(
          find.text('Navigate still works from the address.'), findsOneWidget);
      expect(find.byType(GoogleMap), findsNothing);
      expect(tester.getSize(find.byKey(const Key('trip-map-preview'))).height,
          200);
    });

    testWidgets('placeholder without directions omits navigate hint',
        (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: Scaffold(
            body: TripMapPreviewCard(trip: _trip()),
          ),
        ),
      );
      await tester.pump();

      expect(find.text('Map needs a location'), findsOneWidget);
      expect(
        find.text('This trip has no pickup or drop-off coordinates.'),
        findsOneWidget,
      );
    });
  });
}
