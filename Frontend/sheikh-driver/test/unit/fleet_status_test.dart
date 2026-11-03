import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_status.dart';

void main() {
  group('resolveFleetStatus', () {
    final now = DateTime.utc(2026, 7, 21, 12);

    test('SOS wins', () {
      expect(
        resolveFleetStatus(
          speed: 40,
          ignition: true,
          lastUpdated: now,
          alarmType: 'sos',
          now: now,
        ),
        FleetTrackStatus.sos,
      );
    });

    test('never seen without timestamp', () {
      expect(
        resolveFleetStatus(hasGps: true, now: now),
        FleetTrackStatus.neverSeen,
      );
    });

    test('offline when no tracker even with fresh coords', () {
      expect(
        resolveFleetStatus(
          speed: 55,
          ignition: true,
          lastUpdated: now,
          hasGps: false,
          now: now,
        ),
        FleetTrackStatus.offline,
      );
    });

    test('offline when stale', () {
      expect(
        resolveFleetStatus(
          speed: 0,
          lastUpdated: now.subtract(const Duration(minutes: 45)),
          now: now,
        ),
        FleetTrackStatus.offline,
      );
    });

    test('parked when ignition off even if speed reports drift', () {
      expect(
        resolveFleetStatus(
          speed: 5,
          ignition: false,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.parked,
      );
    });

    test('unknown when ignition off at high speed (contradictory)', () {
      expect(
        resolveFleetStatus(
          speed: 14,
          ignition: false,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.unknown,
      );
    });

    test('moving when speed >= 10 and ignition on', () {
      expect(
        resolveFleetStatus(
          speed: 14,
          ignition: true,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.moving,
      );
    });

    test('parked when ignition off and not moving', () {
      expect(
        resolveFleetStatus(
          speed: 0,
          ignition: false,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.parked,
      );
    });

    test('idle when ignition on at rest', () {
      expect(
        resolveFleetStatus(
          speed: 2,
          ignition: true,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.idle,
      );
    });

    test('unknown when ignition null at rest (ACC not reported)', () {
      expect(
        resolveFleetStatus(
          speed: 0,
          ignition: null,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.unknown,
      );
    });

    test('unknown when GPS invalid even with ignition off', () {
      expect(
        resolveFleetStatus(
          speed: 0,
          ignition: false,
          lastUpdated: now,
          now: now,
          isGpsValid: false,
        ),
        FleetTrackStatus.unknown,
      );
    });
  });

  group('mapLiveFleetRow', () {
    test('vehicle without tracker stays offline despite coords', () {
      final row = mapLiveFleetRow({
        'vehicleId': 2004,
        'vehicleName': 'MG ZS EV',
        'registrationNumber': 'EV-DEMO-002',
        'hasGpsDevice': false,
        'latitude': 25.5,
        'longitude': 68.0,
        'lastUpdated': '2026-07-21T12:00:00Z',
        'speed': 55,
        'ignition': true,
      });
      expect(row.status, FleetTrackStatus.offline);
      expect(row.hasGps, isFalse);
      // Last-known coords still pin on the live map (styled as offline).
      expect(row.hasMapCoords, isTrue);
    });

    test('stale tracker with coords stays mappable when offline', () {
      final row = mapLiveFleetRow({
        'vehicleId': 9,
        'vehicleName': 'Bus',
        'registrationNumber': 'ABC-9',
        'hasGpsDevice': true,
        'latitude': 31.5,
        'longitude': 74.3,
        'lastUpdated': '2026-07-21T10:00:00Z',
        'speed': 0,
        'ignition': false,
      });
      expect(row.status, FleetTrackStatus.offline);
      expect(row.hasMapCoords, isTrue);
    });

    test('invalid GPS maps to unknown', () {
      final fresh = DateTime.now().toUtc().toIso8601String();
      final row = mapLiveFleetRow({
        'vehicleId': 1,
        'vehicleName': 'Corolla',
        'registrationNumber': 'LEB-8825',
        'hasGpsDevice': true,
        'latitude': 31.5,
        'longitude': 74.3,
        'lastUpdated': fresh,
        'speed': 0,
        'ignition': false,
        'isGpsValid': false,
        'gpsQualityCode': 'frozen_coordinates',
      });
      expect(row.status, FleetTrackStatus.unknown);
      expect(row.isGpsValid, isFalse);
    });

    test('null ignition at rest is unknown not parked', () {
      final fresh = DateTime.now().toUtc().toIso8601String();
      final row = mapLiveFleetRow({
        'vehicleId': 1,
        'vehicleName': 'Corolla',
        'registrationNumber': 'LEB-8825',
        'hasGpsDevice': true,
        'latitude': 31.5,
        'longitude': 74.3,
        'lastUpdated': fresh,
        'speed': 0,
        'ignition': null,
      });
      expect(row.status, FleetTrackStatus.unknown);
      expect(row.ignition, isNull);
    });
  });
}
