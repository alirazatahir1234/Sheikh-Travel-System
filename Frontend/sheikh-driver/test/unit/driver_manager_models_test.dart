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

  group('DriverStats', () {
    test('parses expanded hub KPI fields', () {
      final s = DriverStats.fromJson({
        'totalDrivers': 10,
        'active': 8,
        'onTrip': 2,
        'available': 4,
        'offDuty': 1,
        'gpsOnline': 3,
        'licensesExpiringSoon': 2,
        'licensesExpired': 1,
        'suspended': 1,
        'pendingVerification': 3,
        'rejectedDrivers': 1,
        'licensesExpiringIn7Days': 1,
        'licensesExpiringIn60Days': 4,
        'assignedDrivers': 5,
        'openViolations': 2,
      });
      expect(s.pendingVerification, 3);
      expect(s.openViolations, 2);
      expect(s.assignedDrivers, 5);
      expect(s.licensesExpiringIn7Days, 1);
      expect(s.suspended, 1);
    });
  });

  group('resolveDriverDisplayName', () {
    test('uses fullName when first/last are null', () {
      expect(
        resolveDriverDisplayName({
          'fullName': 'Demo Driver',
          'firstName': null,
          'lastName': null,
        }),
        'Demo Driver',
      );
    });

    test('falls back to first + last when fullName empty', () {
      expect(
        resolveDriverDisplayName({
          'fullName': '',
          'firstName': 'Ali',
          'lastName': 'Khan',
        }),
        'Ali Khan',
      );
    });
  });

  group('computeDriverScore', () {
    test('uses rating when present', () {
      const d = DriverListItem(
        id: 1,
        fullName: 'A',
        phone: '1',
        licenseNumber: 'L1',
        status: 'Available',
        isActive: true,
        rating: 4.0,
      );
      expect(computeDriverScore(d), 80);
    });

    test('builds top drivers from list', () {
      final top = buildTopDriversFromList([
        const DriverListItem(
          id: 1,
          fullName: 'Low',
          phone: '1',
          licenseNumber: 'L1',
          status: 'Suspended',
          isActive: true,
          licenseExpired: true,
        ),
        const DriverListItem(
          id: 2,
          fullName: 'High',
          phone: '2',
          licenseNumber: 'L2',
          status: 'Available',
          isActive: true,
          verificationStatus: 'Verified',
          assignedVehicleId: 9,
          rating: 5.0,
        ),
      ]);
      expect(top.first.driverId, 2);
      expect(top.first.score, 100);
    });

    test('blends GPS safety 60% with compliance 40%', () {
      expect(blendDriverTopScore(100, 50), 70);
      final top = buildTopDriversFromList(
        [
          const DriverListItem(
            id: 1,
            fullName: 'Mixed',
            phone: '1',
            licenseNumber: 'L1',
            status: 'Available',
            isActive: true,
            rating: 5.0,
          ),
        ],
        gpsByDriverId: {
          1: const DriverRankItem(
            driverId: 1,
            driverName: 'Mixed',
            score: 50,
            rating: 'Fair',
          ),
        },
      );
      expect(top.first.score, 70);
      expect(top.first.safetyScore, 50);
      expect(top.first.complianceScore, 100);
    });
  });

  group('licenseExpiryQuery', () {
    test('maps alert filters to API query values', () {
      expect(licenseExpiryQuery(DriverLicenseAlertFilter.expired), 'EXPIRED');
      expect(licenseExpiryQuery(DriverLicenseAlertFilter.expiring), 'EXPIRING');
      expect(licenseExpiryQuery(DriverLicenseAlertFilter.none), isNull);
    });
  });

  group('DriversHubState search', () {
    test('filters by search text', () {
      final drivers = [
        const DriverListItem(
          id: 1,
          fullName: 'Ali',
          phone: '1',
          licenseNumber: 'L1',
          status: 'Available',
          isActive: true,
        ),
        const DriverListItem(
          id: 2,
          fullName: 'Sara',
          phone: '2',
          licenseNumber: 'L2',
          status: 'Available',
          isActive: true,
        ),
      ];
      final hub = DriversHubState(drivers: drivers, search: 'sar');
      expect(hub.visible.map((d) => d.id), [2]);
    });
  });

  group('DriverDocumentItem', () {
    test('flags expiry windows', () {
      final expired = DriverDocumentItem.fromJson({
        'id': 1,
        'documentType': 'License',
        'status': 'Approved',
        'expiryDate':
            DateTime.now().subtract(const Duration(days: 2)).toIso8601String(),
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
