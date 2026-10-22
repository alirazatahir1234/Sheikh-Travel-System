import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/data/fleet_api.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/widgets/live_map_location.dart';

void main() {
  group('isLiveMapCoordsOnlyAddress', () {
    test('detects plain lat/lng', () {
      expect(isLiveMapCoordsOnlyAddress('31.52040, 74.35870'), isTrue);
      expect(isLiveMapCoordsOnlyAddress('31.52,74.35'), isTrue);
    });

    test('detects degree / hemisphere variants', () {
      expect(isLiveMapCoordsOnlyAddress('31.52040° N, 74.35870° E'), isTrue);
      expect(isLiveMapCoordsOnlyAddress('31.52 N, 74.35 E'), isTrue);
    });

    test('rejects street addresses', () {
      expect(
        isLiveMapCoordsOnlyAddress('Mall Road, Lahore, Punjab'),
        isFalse,
      );
      expect(
        isLiveMapCoordsOnlyAddress('House 12, Street 4, Gulberg'),
        isFalse,
      );
    });
  });

  group('liveMapAddressNeedsReverseGeocode', () {
    test('needs geocode for empty, coords, and coarse city lines', () {
      expect(liveMapAddressNeedsReverseGeocode(null), isTrue);
      expect(liveMapAddressNeedsReverseGeocode(''), isTrue);
      expect(liveMapAddressNeedsReverseGeocode('31.52, 74.35'), isTrue);
      expect(
        liveMapAddressNeedsReverseGeocode('Pasrur, Punjab, Pakistan'),
        isTrue,
      );
    });

    test('skips geocode for street-level lines', () {
      expect(
        liveMapAddressNeedsReverseGeocode('Mall Road, Lahore'),
        isFalse,
      );
      expect(
        liveMapAddressNeedsReverseGeocode('Street 4, Gulberg III, Lahore'),
        isFalse,
      );
    });
  });

  group('buildLiveMapLocationDisplay', () {
    FleetVehicleLocation vehicle({String? address}) => FleetVehicleLocation(
          vehicleId: 1,
          vehicleName: 'Bus 1',
          registrationNumber: 'ABC-123',
          latitude: 31.5204,
          longitude: 74.3587,
          speed: 0,
          status: FleetTrackStatus.idle,
          address: address,
        );

    test('shows resolving state while waiting without address', () {
      final d = buildLiveMapLocationDisplay(
        vehicle: vehicle(),
        info: null,
        waiting: true,
      );
      expect(d.isResolving, isTrue);
      expect(d.secondary, 'Resolving exact location…');
      expect(d.coords, '31.52040, 74.35870');
    });

    test('prefers reverse-geocode primary address over coords', () {
      final d = buildLiveMapLocationDisplay(
        vehicle: vehicle(address: '31.52040, 74.35870'),
        info: const ReverseGeocodeInfo(
          formattedAddress: 'Mall Road, Lahore, Punjab, Pakistan',
          primaryAddress: 'Mall Road',
          localityLine: 'Lahore, Punjab, Pakistan',
        ),
        waiting: false,
      );
      expect(d.primary, 'Mall Road, Lahore, Punjab, Pakistan');
      expect(d.isUnavailable, isFalse);
      expect(d.coords, '31.52040, 74.35870');
    });

    test('falls back to coords label when geocode fails', () {
      final d = buildLiveMapLocationDisplay(
        vehicle: vehicle(),
        info: null,
        waiting: false,
      );
      expect(d.primary, isNull);
      expect(d.secondary, 'Address unavailable');
      expect(d.coords, '31.52040, 74.35870');
      expect(d.isUnavailable, isTrue);
    });
  });
}
