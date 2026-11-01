import '../domain/notification_models.dart';

/// Maps notification / FCM payload fields to an in-app go_router path.
class NotificationDeepLink {
  static String? fromNotification(
    AppNotification n, {
    bool forDriver = false,
  }) {
    return resolve(
      type: n.type,
      module: n.module,
      referenceId: n.referenceId,
      forDriver: forDriver,
    );
  }

  static String? fromData(
    Map<String, dynamic> data, {
    bool forDriver = false,
  }) {
    final route = data['route']?.toString();
    if (route != null && route.startsWith('/')) {
      return forDriver ? _driverSafeRoute(route) : route;
    }

    final tripId = int.tryParse(
        data['tripId']?.toString() ?? data['TripId']?.toString() ?? '');
    if (tripId != null && tripId > 0) return '/trips/$tripId';

    final bookingId = int.tryParse(
        data['bookingId']?.toString() ?? data['BookingId']?.toString() ?? '');
    if (bookingId != null && bookingId > 0) return '/trips/$bookingId';

    final driverId = int.tryParse(
        data['driverId']?.toString() ?? data['DriverId']?.toString() ?? '');
    if (driverId != null && driverId > 0) {
      return forDriver ? '/profile' : '/more/drivers/$driverId';
    }

    final vehicleId = int.tryParse(
        data['vehicleId']?.toString() ?? data['VehicleId']?.toString() ?? '');
    if (vehicleId != null && vehicleId > 0) {
      return forDriver ? '/live' : '/fleet/vehicles/$vehicleId';
    }

    final alertId = int.tryParse(
        data['alertId']?.toString() ?? data['AlertId']?.toString() ?? '');
    if (alertId != null && alertId > 0) {
      return forDriver ? '/notifications' : '/alerts/$alertId';
    }

    final referenceId = int.tryParse(data['referenceId']?.toString() ??
        data['ReferenceId']?.toString() ??
        '');

    return resolve(
      type: data['type']?.toString() ?? data['Type']?.toString(),
      module: data['module']?.toString() ?? data['Module']?.toString(),
      referenceId: referenceId,
      forDriver: forDriver,
    );
  }

  static String? resolve({
    String? type,
    String? module,
    int? referenceId,
    bool forDriver = false,
  }) {
    final t = (type ?? '').toLowerCase();
    final m = (module ?? '').toLowerCase();

    if (t.contains('sos') || m.contains('sos') || m.contains('alert')) {
      if (forDriver) return '/notifications';
      if (referenceId != null && referenceId > 0) return '/alerts/$referenceId';
      return '/alerts';
    }
    if (m.contains('fuel') || t.contains('fuel')) return '/fuel';
    if (m.contains('inspection')) return '/inspection';
    if (m.contains('document') || m.contains('compliance')) return '/documents';
    if (m.contains('earning') ||
        m.contains('payment') ||
        t.contains('payment')) {
      return '/earnings';
    }
    if (m.contains('attendance')) return '/attendance';
    if (m.contains('driver')) {
      if (forDriver) return '/profile';
      if (referenceId != null && referenceId > 0) {
        return '/more/drivers/$referenceId';
      }
      return '/more/drivers';
    }
    if (m.contains('gps') ||
        t.contains('vehicleoffline') ||
        m.contains('fleet') ||
        m.contains('vehicle')) {
      if (forDriver) return '/live';
      if (referenceId != null && referenceId > 0) {
        return '/fleet/vehicles/$referenceId';
      }
      return '/fleet/map';
    }

    final isTrip = m.contains('trip') ||
        m.contains('booking') ||
        t.contains('trip') ||
        t.contains('booking');
    if (isTrip && referenceId != null && referenceId > 0) {
      return '/trips/$referenceId';
    }
    if (isTrip) return '/trips';

    return '/notifications';
  }

  /// Remap a resolved route for driver-only sessions (staff paths that only redirect).
  static String? adaptForSession(String? route, {required bool forDriver}) {
    if (route == null || route.isEmpty || !forDriver) return route;
    return _driverSafeRoute(route);
  }

  /// Remap staff-only paths when a driver opens an explicit payload route.
  static String _driverSafeRoute(String route) {
    if (route.startsWith('/alerts')) return '/notifications';
    if (route.startsWith('/fleet')) return '/live';
    if (route.startsWith('/more/drivers')) return '/profile';
    if (route.startsWith('/more/maintenance') ||
        route.startsWith('/more/reports') ||
        route.startsWith('/gps/')) {
      return '/notifications';
    }
    return route;
  }
}
