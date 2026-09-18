import '../domain/gps_alert_models.dart';

/// Local-only demo incidents (never POSTed to the API).
class AlertsSimulator {
  AlertsSimulator._();

  static int _nextId = -1000;

  static List<GpsAlertEvent> createBatch({int seed = 0}) {
    final now = DateTime.now().toUtc();
    final templates = <GpsAlertEvent Function()>[
      () => GpsAlertEvent(
            id: _nextId--,
            vehicleId: 104,
            vehicleName: 'FL-849-XR · TRUCK-104',
            eventType: 'speed_exceeded',
            latitude: 52.0116,
            longitude: 4.3571,
            speed: 114,
            message:
                'Vehicle exceeded posted maximum speed limit (80 km/h) by +34 km/h on Highway A4 for over 45 seconds.',
            timestamp: now,
            isAcknowledged: false,
            severity: 'critical',
            status: 'active',
            driverId: 1,
            driverName: 'Marcus Vance',
            canAcknowledge: true,
            canResolve: true,
          ),
      () => GpsAlertEvent(
            id: _nextId--,
            vehicleId: 22,
            vehicleName: 'TR-302-AA · REEFER-22',
            eventType: 'geofence_exit',
            latitude: 52.0705,
            longitude: 4.3007,
            speed: 28,
            message:
                'Vehicle crossed exterior boundary of Distribution Center Hub Alpha without authorization.',
            timestamp: now,
            isAcknowledged: false,
            severity: 'critical',
            status: 'active',
            geofenceId: 1,
            geofenceName: 'Hub Alpha',
            driverId: 2,
            driverName: 'Elena Rostova',
            canAcknowledge: true,
            canResolve: true,
          ),
      () => GpsAlertEvent(
            id: _nextId--,
            vehicleId: 55,
            vehicleName: 'VN-118-BK · VAN-55',
            eventType: 'engine_fault',
            latitude: 51.9225,
            longitude: 4.4792,
            speed: 0,
            message:
                'DTC P0217 engine overheating detected. Coolant temp above threshold — reduce load.',
            timestamp: now,
            isAcknowledged: false,
            severity: 'high',
            status: 'active',
            driverId: 3,
            driverName: 'Jonas Berg',
            canAcknowledge: true,
            canResolve: true,
          ),
    ];
    // One new demo incident per press (rotates type).
    return [templates[seed % templates.length]()];
  }
}
