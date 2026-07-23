import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/alerts/domain/gps_alert_models.dart';
import 'package:sheikh_go_driver/features/drivers/domain/driver_models.dart';
import 'package:sheikh_go_driver/features/ops_trips/domain/ops_trip_models.dart';

void main() {
  test('DriverListItem parses camelCase json', () {
    final d = DriverListItem.fromJson({
      'id': 3,
      'fullName': 'Ali Khan',
      'phone': '03001234567',
      'licenseNumber': 'LIC-1',
      'status': 'Available',
      'isActive': true,
      'gpsOnline': true,
      'assignedVehicleRegistration': 'ABC-123',
    });
    expect(d.id, 3);
    expect(d.fullName, 'Ali Khan');
    expect(d.gpsOnline, isTrue);
    expect(d.assignedVehicleRegistration, 'ABC-123');
  });

  test('OpsTripListItem parses status enums as strings', () {
    final t = OpsTripListItem.fromJson({
      'id': 10,
      'tripNumber': 'TR-10',
      'tripDate': '2026-07-21T00:00:00Z',
      'plannedStart': '2026-07-21T08:00:00Z',
      'status': 'Scheduled',
      'priority': 'High',
      'tripType': 'Transfer',
      'customerName': 'Acme',
    });
    expect(t.tripNumber, 'TR-10');
    expect(t.status, 'Scheduled');
    expect(t.priority, 'High');
  });

  test('GpsAlertEvent parses severity and ack flag', () {
    final a = GpsAlertEvent.fromJson({
      'id': 5,
      'vehicleId': 2,
      'vehicleName': 'Bus 2',
      'eventType': 'overspeed',
      'latitude': 24.8,
      'longitude': 67.0,
      'speed': 95,
      'message': 'Over speed',
      'timestamp': '2026-07-21T10:00:00Z',
      'isAcknowledged': false,
      'severity': 'Critical',
      'status': 'Open',
    });
    expect(a.eventType, 'overspeed');
    expect(a.isOpen, isTrue);
    expect(a.severity, 'Critical');
  });
}
