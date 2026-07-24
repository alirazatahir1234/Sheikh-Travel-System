import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';

void main() {
  group('Fleet Manager models', () {
    test('HistoryReplayBundle prefers playback points', () {
      final bundle = HistoryReplayBundle.fromJson({
        'route': [
          {
            'timestamp': '2026-07-22T10:00:00Z',
            'latitude': 24.86,
            'longitude': 67.00,
            'speedKmh': 10,
          }
        ],
        'playback': [
          {
            'timestamp': '2026-07-22T10:01:00Z',
            'latitude': 24.87,
            'longitude': 67.01,
            'speedKmh': 20,
          }
        ],
        'mileageKm': 12.5,
        'vehicle': {'gpsDeviceId': 9},
      });
      expect(bundle.points, hasLength(1));
      expect(bundle.points.first.speedKmh, 20);
      expect(bundle.gpsDeviceId, 9);
      expect(bundle.mileageKm, 12.5);
    });

    test('VehicleFuelSummary parses nested items', () {
      final summary = VehicleFuelSummary.fromJson({
        'items': [
          {
            'id': 1,
            'vehicleId': 5,
            'liters': 40,
            'totalCost': 8000,
            'fuelDate': '2026-07-20T00:00:00Z',
            'station': 'PSO',
          }
        ],
        'totalLiters': 40,
        'totalCost': 8000,
        'totalCount': 1,
      });
      expect(summary.items, hasLength(1));
      expect(summary.items.first.station, 'PSO');
      expect(summary.totalCost, 8000);
    });

    test('VehicleGpsInfo maps device id', () {
      final gps = VehicleGpsInfo.fromJson({
        'gpsDeviceId': 44,
        'deviceName': 'Tracker A',
        'gpsOnline': true,
        'uniqueId': '359633110000001',
      });
      expect(gps.gpsDeviceId, 44);
      expect(gps.gpsOnline, isTrue);
    });

    test('SupportedGpsCommand availability', () {
      final cmd = SupportedGpsCommand.fromJson({
        'type': 'engineStop',
        'label': 'Engine stop',
        'available': false,
        'reason': 'Not supported',
      });
      expect(cmd.available, isFalse);
      expect(cmd.reason, 'Not supported');
    });
  });
}
