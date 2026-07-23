import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/drivers/domain/driver_models.dart';
import 'package:sheikh_go_driver/features/drivers/presentation/drivers_notifier.dart';

void main() {
  group('DriverPerformanceSummary', () {
    test('parses summary dto', () {
      final p = DriverPerformanceSummary.fromJson({
        'driverId': 3,
        'driverName': 'Ali',
        'rating': 4.5,
        'totalTrips': 20,
        'completedTrips': 18,
        'totalRevenue': 120000,
        'completionRate': 90,
        'violationCount': 1,
        'attendancePresentCount': 12,
      });
      expect(p.driverId, 3);
      expect(p.completionRate, 90);
      expect(p.violationCount, 1);
    });
  });

  group('DriverRankItem', () {
    test('parses ranking dto', () {
      final r = DriverRankItem.fromJson({
        'driverId': 9,
        'driverName': 'Sara',
        'score': 88,
        'rating': 'Good',
        'isPartial': true,
      });
      expect(r.score, 88);
      expect(r.isPartial, isTrue);
    });
  });

  group('DriversHubState license filter', () {
    test('filters expired and expiring licenses', () {
      final drivers = [
        DriverListItem(
          id: 1,
          fullName: 'A',
          phone: '1',
          licenseNumber: 'L1',
          status: 'Available',
          isActive: true,
          licenseExpired: true,
        ),
        DriverListItem(
          id: 2,
          fullName: 'B',
          phone: '2',
          licenseNumber: 'L2',
          status: 'Available',
          isActive: true,
          licenseExpiringSoon: true,
        ),
        DriverListItem(
          id: 3,
          fullName: 'C',
          phone: '3',
          licenseNumber: 'L3',
          status: 'Available',
          isActive: true,
        ),
      ];
      final expired = DriversHubState(
        drivers: drivers,
        licenseAlert: DriverLicenseAlertFilter.expired,
      );
      expect(expired.visible.map((d) => d.id), [1]);

      final expiring = DriversHubState(
        drivers: drivers,
        licenseAlert: DriverLicenseAlertFilter.expiring,
      );
      expect(expiring.visible.map((d) => d.id), [2]);
    });
  });

  group('DriverDocumentItem', () {
    test('flags expiry windows', () {
      final expired = DriverDocumentItem.fromJson({
        'id': 1,
        'documentType': 'License',
        'status': 'Approved',
        'expiryDate': DateTime.now()
            .subtract(const Duration(days: 2))
            .toIso8601String(),
      });
      expect(expired.isExpired, isTrue);

      final soon = DriverDocumentItem.fromJson({
        'id': 2,
        'documentType': 'CNIC',
        'status': 'Approved',
        'expiryDate':
            DateTime.now().add(const Duration(days: 10)).toIso8601String(),
      });
      expect(soon.isExpiringSoon, isTrue);
    });
  });
}
