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

    test('canReject only before accept (Accept + Reject)', () {
      final assigned = _trip(lifecycle: 3, actions: ['Accept', 'Reject']);
      expect(assigned.canReject, isTrue);

      // Backend historically still sent Reject after Started — must hide.
      final driving = _trip(
        status: 3,
        lifecycle: 5,
        actions: ['Arrived', 'Onboard', 'Reject'],
      );
      expect(driving.canAccept, isFalse);
      expect(driving.canArrive, isTrue);
      expect(driving.canReject, isFalse);

      final enroute = _trip(
        status: 3,
        lifecycle: 7,
        actions: ['Complete', 'Reject'],
      );
      expect(enroute.canComplete, isTrue);
      expect(enroute.canReject, isFalse);
    });

    test('shouldNavigateToDropoff after enroute / complete', () {
      expect(_trip(lifecycle: 5, actions: ['Arrived']).shouldNavigateToDropoff,
          isFalse);
      expect(_trip(lifecycle: 6, actions: ['Onboard']).shouldNavigateToDropoff,
          isFalse);
      expect(
        _trip(lifecycle: 7, actions: ['Complete']).shouldNavigateToDropoff,
        isTrue,
      );
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

    test('hasMapPreviewCoords requires real pickup or drop lat/lng', () {
      expect(_trip().hasMapPreviewCoords, isFalse);
      expect(_trip().copyWithCoords(0, 0).hasMapPreviewCoords, isFalse);
      expect(_trip().copyWithCoords(33.68, 73.05).hasMapPreviewCoords, isTrue);
      final dropOnly = Trip(
        id: 2,
        bookingNumber: 'B-2',
        customerName: 'Ali',
        routeName: 'A → B',
        pickupTime: DateTime.utc(2026, 1, 1),
        status: 2,
        statusName: 'Confirmed',
        totalAmount: 100,
        dropLatitude: 34.0151,
        dropLongitude: 71.5249,
      );
      expect(dropOnly.hasMapPreviewCoords, isTrue);
      expect(dropOnly.hasDropCoords, isTrue);
      expect(dropOnly.hasPickupCoords, isFalse);
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

    test('parses UTC pickup/dropoff without inventing device time', () {
      final t = Trip.fromJson({
        'id': 1,
        'bookingNumber': 'BK-2026-1001',
        'customerName': 'Ali',
        'routeName': 'Route A',
        'pickupTime': '2026-10-15T07:36:00Z',
        'dropoffTime': '2026-10-15T09:00:00Z',
        'status': 2,
        'statusName': 'Assigned',
        'totalAmount': 1000,
      });
      expect(t.hasScheduledPickup, isTrue);
      expect(t.pickupTime.isUtc, isTrue);
      expect(t.dropoffTime, isNotNull);
      expect(t.dropoffTime!.isUtc, isTrue);
      expect(t.pickupTimeLocal, t.pickupTime.toLocal());
      expect(t.dropoffDateTimeLabel, isNot(equals('ETA pending')));
    });

    test('missing pickupTime does not fall back to DateTime.now', () {
      final before = DateTime.now();
      final t = Trip.fromJson({
        'id': 2,
        'bookingNumber': 'BK-2',
        'customerName': 'Ali',
        'routeName': 'Route A',
        'status': 2,
        'statusName': 'Assigned',
        'totalAmount': 1000,
      });
      expect(t.hasScheduledPickup, isFalse);
      expect(t.pickupTime, Trip.missingPickupTime);
      expect(t.scheduledDateLabel, 'Date unavailable');
      expect(t.pickupDateTimeLabel, 'Time unavailable');
      expect(t.dropoffDateTimeLabel, 'ETA pending');
      // Guard against accidental DateTime.now() fallback.
      expect(t.pickupTime.isBefore(before.subtract(const Duration(days: 365))),
          isTrue);
    });

    test('null dropoffTime shows ETA pending', () {
      final t = Trip.fromJson({
        'id': 3,
        'bookingNumber': 'BK-3',
        'customerName': 'Ali',
        'routeName': 'Route A',
        'pickupTime': '2026-10-15T12:36:00',
        'dropoffTime': null,
        'status': 2,
        'statusName': 'Assigned',
        'totalAmount': 1000,
      });
      expect(t.dropoffTime, isNull);
      expect(t.dropoffDateTimeLabel, 'ETA pending');
    });
  });

  group('Trip date labels', () {
    test('formats scheduled date and time-only stop labels in local timezone',
        () {
      // Far-future local wall clock so overdue logic stays off in CI.
      final t = Trip(
        id: 1,
        bookingNumber: 'BK-2026-1001',
        customerName: 'Ali',
        routeName: 'Lahore → Islamabad',
        pickupTime: DateTime(2099, 10, 15, 12, 36),
        dropoffTime: DateTime(2099, 10, 15, 15, 10),
        status: 2,
        statusName: 'Assigned',
        totalAmount: 1000,
      );
      expect(t.scheduledDateLabel, 'Thursday, 15 October 2099');
      // Full date is under booking ID; stops show time only.
      expect(t.pickupDateTimeLabel, '12:36 PM');
      expect(t.dropoffDateTimeLabel, '3:10 PM');
      expect(t.isPickupOverdue, isFalse);
    });

    test('marks overdue pickup without inventing dropoff ETA', () {
      final t = Trip(
        id: 1,
        bookingNumber: 'BK-1',
        customerName: 'Ali',
        routeName: 'Route A',
        pickupTime: DateTime.now().subtract(const Duration(hours: 2)),
        status: 2,
        statusName: 'Assigned',
        totalAmount: 1000,
        lifecycleStatus: 5,
        lifecycleStatusName: 'Driving to pickup',
      );
      expect(t.isPickupOverdue, isTrue);
      expect(t.pickupDateTimeLabel, contains('Overdue'));
      expect(t.dropoffDateTimeLabel, 'ETA pending');
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
