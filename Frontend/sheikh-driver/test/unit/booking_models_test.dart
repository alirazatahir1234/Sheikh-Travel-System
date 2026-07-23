import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/bookings/domain/booking_models.dart';

void main() {
  group('BookingListItem', () {
    test('parses booking dto and dispatch flags', () {
      final b = BookingListItem.fromJson({
        'id': 10,
        'bookingNumber': 'BK-2026-1010',
        'customerId': 7,
        'customerName': 'Hamza',
        'routeId': 6,
        'routeName': 'Islamabad – Murree',
        'pickupTime': '2026-07-20T13:43:00',
        'passengerCount': 3,
        'totalAmount': 15000,
        'status': 'Pending',
        'createdAt': '2026-07-19T10:00:00Z',
      });
      expect(b.bookingNumber, 'BK-2026-1010');
      expect(b.needsDispatch, isTrue);
      expect(b.isUnassigned, isTrue);
    });

    test('assigned confirmed booking is ready', () {
      final b = BookingListItem.fromJson({
        'id': 11,
        'bookingNumber': 'BK-2',
        'customerId': 1,
        'routeId': 1,
        'driverId': 2,
        'vehicleId': 3,
        'pickupTime': '2026-07-20T13:43:00',
        'passengerCount': 2,
        'totalAmount': 1,
        'status': 'Confirmed',
        'createdAt': '2026-07-19T10:00:00Z',
      });
      expect(b.needsDispatch, isTrue);
      expect(b.isUnassigned, isFalse);
    });
  });
}
