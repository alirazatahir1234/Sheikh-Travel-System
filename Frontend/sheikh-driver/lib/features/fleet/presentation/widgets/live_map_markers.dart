import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../domain/fleet_models.dart';
import 'vehicle_pulse_icons.dart';

export 'vehicle_pulse_icons.dart';

/// Back-compat alias for [VehiclePulseIcons].
@Deprecated('Use VehiclePulseIcons')
class LiveMapMarkers {
  LiveMapMarkers._();

  static Color accentFor(FleetTrackStatus status) =>
      VehiclePulseIcons.colorFor(status);

  static void clearCache() => VehiclePulseIcons.clearCache();

  static Future<void> preload(double devicePixelRatio) =>
      VehiclePulseIcons.preload(devicePixelRatio);

  static Future<BitmapDescriptor> iconFor(
    FleetTrackStatus status, {
    double devicePixelRatio = 2,
    bool selected = false,
  }) =>
      VehiclePulseIcons.iconFor(
        status,
        devicePixelRatio: devicePixelRatio,
        selected: selected,
      );

  static Future<BitmapDescriptor> previewIcon({
    required FleetTrackStatus status,
    double devicePixelRatio = 2,
  }) =>
      VehiclePulseIcons.iconFor(status, devicePixelRatio: devicePixelRatio);
}
