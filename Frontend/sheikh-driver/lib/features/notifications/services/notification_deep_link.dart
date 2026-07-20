import '../domain/notification_models.dart';

/// Maps notification / FCM payload fields to an in-app go_router path.
class NotificationDeepLink {
  static String? fromNotification(AppNotification n) {
    return resolve(
      type: n.type,
      module: n.module,
      referenceId: n.referenceId,
    );
  }

  static String? fromData(Map<String, dynamic> data) {
    final route = data['route']?.toString();
    if (route != null && route.startsWith('/')) return route;

    final tripId = int.tryParse(
        data['tripId']?.toString() ?? data['TripId']?.toString() ?? '');
    if (tripId != null && tripId > 0) return '/trips/$tripId';

    final bookingId = int.tryParse(
        data['bookingId']?.toString() ?? data['BookingId']?.toString() ?? '');
    if (bookingId != null && bookingId > 0) return '/trips/$bookingId';

    final referenceId = int.tryParse(
        data['referenceId']?.toString() ?? data['ReferenceId']?.toString() ?? '');

    return resolve(
      type: data['type']?.toString() ?? data['Type']?.toString(),
      module: data['module']?.toString() ?? data['Module']?.toString(),
      referenceId: referenceId,
    );
  }

  static String? resolve({
    String? type,
    String? module,
    int? referenceId,
  }) {
    final t = (type ?? '').toLowerCase();
    final m = (module ?? '').toLowerCase();

    if (t.contains('sos') || m.contains('sos')) return '/notifications';
    if (m.contains('fuel') || t.contains('fuel')) return '/fuel';
    if (m.contains('inspection')) return '/inspection';
    if (m.contains('document') || m.contains('compliance')) return '/documents';
    if (m.contains('earning') || m.contains('payment') || t.contains('payment')) {
      return '/earnings';
    }
    if (m.contains('attendance')) return '/attendance';
    if (m.contains('gps') || t.contains('vehicleoffline') || m.contains('fleet')) {
      return '/live';
    }

    final isTrip = m.contains('trip') ||
        m.contains('booking') ||
        t.contains('trip') ||
        t.contains('booking');
    if (isTrip && referenceId != null && referenceId > 0) {
      return '/trips/$referenceId';
    }
    if (isTrip) return '/trips';

    return null;
  }
}
