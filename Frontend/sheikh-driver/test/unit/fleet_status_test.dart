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

    test('parked when ignition off even at higher drift speed', () {
      expect(
        resolveFleetStatus(
          speed: 14,
          ignition: false,
          lastUpdated: now,
          now: now,
        ),
        FleetTrackStatus.parked,
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

    test('idle otherwise', () {
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
  });

  group('mergeVehiclesWithLive', () {
    test('prefers live position over snapshot', () {
      final now = DateTime.now().toUtc();
      final vehicles = [
        VehicleListItem(
          id: 1,
          name: 'Bus A',
          registrationNumber: 'ABC-1',
          status: 'Available',
          locationLatitude: 24.0,
          locationLongitude: 67.0,
          locationLastUpdate: now.subtract(const Duration(hours: 2)),
          hasGpsDevice: true,
        ),
      ];
      final live = [
        GpsPosition(
          vehicleId: 1,
          latitude: 24.86,
          longitude: 67.00,
          speed: 20,
          timestamp: now.subtract(const Duration(seconds: 20)),
          ignition: true,
        ),
      ];
      final merged = mergeVehiclesWithLive(vehicles: vehicles, live: live);
      expect(merged, hasLength(1));
      expect(merged.first.latitude, 24.86);
      expect(merged.first.status, FleetTrackStatus.moving);
      // ignition true + speed 20 → moving under unified rule
    });

    test('applyLiveUpdate ignores stale payloads', () {
      final now = DateTime.now().toUtc();
      final current = [
        FleetVehicleLocation(
          vehicleId: 1,
          vehicleName: 'Bus A',
          registrationNumber: 'ABC-1',
          status: FleetTrackStatus.moving,
          latitude: 24.86,
          longitude: 67.0,
          lastUpdated: now,
          speed: 20,
          hasGps: true,
        ),
      ];
      final stale = GpsPosition(
        vehicleId: 1,
        latitude: 24.0,
        longitude: 67.0,
        speed: 0,
        timestamp: now.subtract(const Duration(minutes: 5)),
      );
      final next = applyLiveUpdate(current, stale);
      expect(next.first.latitude, 24.86);
      expect(next.first.speed, 20);
    });
  });
}
