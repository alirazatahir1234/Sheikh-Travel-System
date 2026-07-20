import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/trips/domain/trip_model.dart';

Trip _trip({
  int status = 2,
  int lifecycle = 2,
  List<String> actions = const [],
}) =>
    Trip(
      id: 1,
      bookingNumber: 'B-1',
      customerName: 'Ali',
      routeName: 'Lahore → Islamabad',
      pickupTime: DateTime.utc(2026, 1, 1),
      status: status,
      statusName: 'Confirmed',
      totalAmount: 1000,
      lifecycleStatus: lifecycle,
      lifecycleStatusName: 'Scheduled',
      nextActions: actions,
    );

void main() {
  group('Trip lifecycle helpers', () {
    test('canAccept when nextActions contains Accept', () {
      final t = _trip(actions: ['Accept', 'Reject']);
      expect(t.canAccept, isTrue);
      expect(t.canReject, isTrue);
      expect(t.canComplete, isFalse);
    });

    test('isStarted for Enroute lifecycle', () {
      final t = _trip(status: 3, lifecycle: 7);
      expect(t.isStarted, isTrue);
      expect(t.isCompleted, isFalse);
    });

    test('isCompleted for Completed lifecycle', () {
      final t = _trip(status: 4, lifecycle: 9);
      expect(t.isCompleted, isTrue);
      expect(t.isActionable, isFalse);
    });

    test('hasPickupCoords ignores 0,0', () {
      final empty = _trip().copyWithCoords(0, 0);
      expect(empty.hasPickupCoords, isFalse);
      final ok = _trip().copyWithCoords(31.5, 74.3);
      expect(ok.hasPickupCoords, isTrue);
    });
  });

  group('Trip.fromJson', () {
    test('parses nextActions and coords', () {
      final t = Trip.fromJson({
        'id': 9,
        'bookingNumber': 'BK-9',
        'customerName': 'Sara',
        'routeName': 'Route A',
        'pickupTime': '2026-07-01T10:00:00Z',
        'status': 2,
        'statusName': 'Confirmed',
        'totalAmount': 2500.5,
        'pickupLatitude': 31.52,
        'pickupLongitude': 74.35,
        'lifecycleStatus': 2,
        'lifecycleStatusName': 'Scheduled',
        'nextActions': ['Accept', 'Reject'],
        'source': 'Trip',
      });
      expect(t.id, 9);
      expect(t.totalAmount, 2500.5);
      expect(t.canAccept, isTrue);
      expect(t.hasPickupCoords, isTrue);
      expect(t.source, 'Trip');
    });
  });
}

extension on Trip {
  Trip copyWithCoords(double lat, double lng) => Trip(
        id: id,
        bookingNumber: bookingNumber,
        customerName: customerName,
        routeName: routeName,
        pickupTime: pickupTime,
        status: status,
        statusName: statusName,
        totalAmount: totalAmount,
        pickupLatitude: lat,
        pickupLongitude: lng,
        lifecycleStatus: lifecycleStatus,
        lifecycleStatusName: lifecycleStatusName,
        nextActions: nextActions,
      );
}
