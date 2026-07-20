import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/notifications/domain/notification_models.dart';
import 'package:sheikh_go_driver/features/notifications/services/notification_deep_link.dart';

void main() {
  group('NotificationDeepLink.resolve', () {
    test('maps fuel module to /fuel', () {
      expect(NotificationDeepLink.resolve(module: 'Fuel'), '/fuel');
    });

    test('maps trip with reference to /trips/:id', () {
      expect(
        NotificationDeepLink.resolve(type: 'TripStarted', referenceId: 42),
        '/trips/42',
      );
    });

    test('maps sos to notifications', () {
      expect(NotificationDeepLink.resolve(type: 'Sos'), '/notifications');
    });

    test('maps payment to earnings', () {
      expect(
        NotificationDeepLink.resolve(type: 'PaymentReceived'),
        '/earnings',
      );
    });

    test('maps gps / fleet to live', () {
      expect(
        NotificationDeepLink.resolve(type: 'VehicleOffline'),
        '/live',
      );
    });
  });

  group('NotificationDeepLink.fromData', () {
    test('prefers explicit route', () {
      expect(
        NotificationDeepLink.fromData({'route': '/documents'}),
        '/documents',
      );
    });

    test('uses tripId', () {
      expect(
        NotificationDeepLink.fromData({'tripId': '15'}),
        '/trips/15',
      );
    });
  });

  group('AppNotification.fromJson', () {
    test('maps numeric type enum', () {
      final n = AppNotification.fromJson({
        'id': 1,
        'title': 'Trip',
        'message': 'Assigned',
        'type': 7,
        'isRead': false,
        'createdAt': '2026-07-01T12:00:00Z',
        'module': 'Trips',
        'referenceId': 9,
      });
      expect(n.type, 'TripDriverAssigned');
      expect(n.category, 'Trips');
      expect(
        NotificationDeepLink.fromNotification(n),
        '/trips/9',
      );
    });
  });
}
