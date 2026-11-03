import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';

void main() {
  group('GpsFleetStatusKpis.fromJson', () {
    test('maps online / offline / idle from fleet-status-local payload', () {
      final kpis = GpsFleetStatusKpis.fromJson({
        'totalVehicles': 4,
        'online': 2,
        'offline': 2,
        'moving': 0,
        'idle': 1,
        'parked': 0,
        'neverSeen': 0,
        'sos': 0,
        'alertsToday': 68,
      });

      expect(kpis.totalVehicles, 4);
      expect(kpis.online, 2);
      expect(kpis.offline, 2);
      expect(kpis.idle, 1);
      expect(kpis.moving, 0);
    });

    test('accepts PascalCase keys from non-camel serializers', () {
      final kpis = GpsFleetStatusKpis.fromJson({
        'TotalVehicles': 3,
        'Online': 1,
        'Offline': 1,
        'Moving': 1,
        'Idle': 0,
        'Parked': 0,
        'NeverSeen': 1,
        'Sos': 0,
        'AlertsToday': 2,
      });

      expect(kpis.online, 1);
      expect(kpis.offline, 1);
      expect(kpis.neverSeen, 1);
    });
  });
}
