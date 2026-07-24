import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/compliance/domain/compliance_models.dart';
import 'package:sheikh_go_driver/features/maintenance/domain/maintenance_models.dart';
import 'package:sheikh_go_driver/features/reports/domain/report_models.dart';
import 'package:sheikh_go_driver/features/staff_fuel/domain/staff_fuel_models.dart';

void main() {
  test('MaintenanceKpis parses nested dashboard kpis', () {
    final k = MaintenanceKpis.fromJson({
      'totalVehicles': 12,
      'dueForService': 3,
      'underMaintenance': 1,
      'overdueServices': 2,
      'monthlyMaintenanceCost': 45000.5,
      'activeWorkOrders': 4,
      'pendingRequests': 5,
    });
    expect(k.dueForService, 3);
    expect(k.monthlyMaintenanceCost, 45000.5);
  });

  test('StaffFuelLog parses totals', () {
    final l = StaffFuelLog.fromJson({
      'id': 1,
      'vehicleId': 9,
      'liters': 40.5,
      'pricePerLiter': 280,
      'totalCost': 11340,
      'odometerReading': 12000,
      'fuelType': 'Diesel',
      'fuelDate': '2026-07-20T10:00:00Z',
      'createdAt': '2026-07-20T10:05:00Z',
      'station': 'PSO',
    });
    expect(l.liters, 40.5);
    expect(l.station, 'PSO');
  });

  test('ComplianceDocument flags expired status', () {
    final d = ComplianceDocument.fromJson({
      'id': 2,
      'entityType': 'Vehicle',
      'entityName': 'Bus 1',
      'documentType': 'Insurance',
      'status': 'Expired',
      'expiryDate': '2026-01-01',
    });
    expect(d.isExpired, isTrue);
  });

  test('FleetReport parses columns and rows', () {
    final r = FleetReport.fromJson({
      'reportType': 'fuel',
      'title': 'Fuel Report',
      'totalValue': 1000,
      'columns': [
        {'key': 'vehicle', 'label': 'Vehicle', 'format': 'text'},
      ],
      'rows': [
        {
          'key': '1',
          'label': 'Bus A',
          'count': 3,
          'totalValue': 500,
          'fields': {'vehicle': 'Bus A'},
        },
      ],
      'summary': {'fills': 3},
    });
    expect(r.title, 'Fuel Report');
    expect(r.columns, hasLength(1));
    expect(r.rows.first.label, 'Bus A');
    expect(r.summary['fills'], 3);
  });
}
