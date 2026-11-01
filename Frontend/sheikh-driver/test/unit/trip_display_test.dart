import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/domain/trip_display.dart';

GpsTrip _trip({
  double distanceKm = 5,
  String? status = 'Completed',
  String? tripKey,
}) {
  final now = DateTime.now();
  return GpsTrip(
    vehicleId: 1,
    startTime: now.subtract(const Duration(hours: 1)),
    endTime: now,
    distanceKm: distanceKm,
    avgSpeedKmh: 40,
    maxSpeedKmh: 60,
    durationMinutes: 60,
    status: status,
    tripKey: tripKey,
  );
}

void main() {
  test('tripStatusLabel uses Driven instead of Completed for motion trips', () {
    expect(tripStatusLabel(_trip()), 'Driven');
    expect(tripStatusLabel(_trip(distanceKm: 0.05)), 'Stop');
  });

  test('tripIdLabel hides opaque base64-like tokens', () {
    expect(tripIdLabel(_trip(tripKey: 'v1:MDAwMDBa')), '');
    expect(tripIdLabel(_trip(tripKey: 'trip-12345')), 'Trip 12345');
  });

  test('tripFilterHelperText is plain language', () {
    expect(
      tripFilterHelperText(TripListFilter.moving).toLowerCase(),
      contains('moved'),
    );
    expect(
      tripFilterHelperText(TripListFilter.stops).toLowerCase(),
      contains('parked'),
    );
  });

  test('tripRangeSummaryLabel summarizes motion/stops/km', () {
    final trips = [
      _trip(distanceKm: 10),
      _trip(distanceKm: 0.01, status: 'Stop'),
    ];
    final label = tripRangeSummaryLabel(trips);
    expect(label, contains('1 motion'));
    expect(label, contains('1 stop'));
    expect(label, contains('10.0 km'));
  });
}
