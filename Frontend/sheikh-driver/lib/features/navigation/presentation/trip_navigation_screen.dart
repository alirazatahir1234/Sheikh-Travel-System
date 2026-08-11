import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/trips_cache.dart';
import '../../fleet/domain/fleet_models.dart' as fleet;
import '../../fleet/presentation/widgets/vehicle_pulse_icons.dart';
import '../../trips/domain/trip_model.dart';
import '../../trips/presentation/trips_notifier.dart';
import '../data/navigation_api.dart';
import '../domain/gps_eta.dart';
import '../utils/external_maps.dart';
import '../utils/gps_geo.dart';

class TripNavigationScreen extends ConsumerStatefulWidget {
  const TripNavigationScreen({super.key, required this.tripId});
  final int tripId;

  @override
  ConsumerState<TripNavigationScreen> createState() =>
      _TripNavigationScreenState();
}

class _TripNavigationScreenState extends ConsumerState<TripNavigationScreen> {
  GoogleMapController? _map;
  Position? _position;
  StreamSubscription<Position>? _posSub;
  final List<LatLng> _trail = [];
  bool _follow = true;
  bool _traffic = true;
  bool _navigating = false;
  GpsEta? _eta;
  String? _error;
  final Map<fleet.FleetTrackStatus, BitmapDescriptor> _pulseIcons = {};
  bool _iconsReady = false;

  static const _trailCap = 14; // ERP live-map trail length

  Future<void> _loadPulseIcons(double dpr) async {
    final next = <fleet.FleetTrackStatus, BitmapDescriptor>{};
    for (final s in fleet.FleetTrackStatus.values) {
      next[s] = await VehiclePulseIcons.iconFor(s, devicePixelRatio: dpr);
    }
    if (!mounted) return;
    setState(() {
      _pulseIcons
        ..clear()
        ..addAll(next);
      _iconsReady = true;
    });
  }

  fleet.FleetTrackStatus _toFleetStatus(FleetTrackStatus status) => switch (status) {
        FleetTrackStatus.moving => fleet.FleetTrackStatus.moving,
        FleetTrackStatus.idle => fleet.FleetTrackStatus.idle,
        FleetTrackStatus.parked => fleet.FleetTrackStatus.parked,
        FleetTrackStatus.offline => fleet.FleetTrackStatus.offline,
        FleetTrackStatus.sos => fleet.FleetTrackStatus.sos,
      };

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      unawaited(_loadPulseIcons(MediaQuery.devicePixelRatioOf(context)));
    });
    _bootstrap();
  }

  @override
  void dispose() {
    _posSub?.cancel();
    _map?.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    final perm = await Geolocator.requestPermission();
    if (perm == LocationPermission.denied ||
        perm == LocationPermission.deniedForever) {
      setState(() => _error = 'Location permission required for navigation');
      return;
    }

    try {
      final pos = await Geolocator.getCurrentPosition();
      if (!mounted) return;
      setState(() {
        _position = pos;
        _trail.add(LatLng(pos.latitude, pos.longitude));
      });
    } catch (_) {}

    await _refreshEta();
    _posSub = Geolocator.getPositionStream(
      locationSettings: const LocationSettings(
        accuracy: LocationAccuracy.high,
        distanceFilter: 8,
      ),
    ).listen(_onPosition);
  }

  Future<void> _refreshEta() async {
    final eta =
        await ref.read(navigationApiProvider).getEta(widget.tripId);
    if (mounted) setState(() => _eta = eta);
  }

  void _onPosition(Position pos) {
    if (!mounted) return;
    final latLng = LatLng(pos.latitude, pos.longitude);

    // ERP pattern: snap if jump is huge, else append trail
    if (_trail.isNotEmpty) {
      final last = _trail.last;
      final jump = haversineKm(
          last.latitude, last.longitude, latLng.latitude, latLng.longitude);
      if (jump > 2) {
        _trail
          ..clear()
          ..add(latLng);
      } else {
        _trail.add(latLng);
        while (_trail.length > _trailCap) {
          _trail.removeAt(0);
        }
      }
    } else {
      _trail.add(latLng);
    }

    setState(() => _position = pos);

    if (_follow && _map != null) {
      _map!.animateCamera(CameraUpdate.newLatLng(latLng));
    }
  }

  bool get _mapsAvailable => AppConfig.googleMapsApiKey.isNotEmpty;

  Trip? _findTrip(List<Trip> trips) {
    return trips
        .where((t) => t.id == widget.tripId || t.bookingId == widget.tripId)
        .firstOrNull;
  }

  Trip? _tripFromCache() {
    try {
      return TripsCache.load()
          .map(Trip.fromJson)
          .where((t) => t.id == widget.tripId || t.bookingId == widget.tripId)
          .firstOrNull;
    } catch (_) {
      return null;
    }
  }

  Trip? get _trip {
    final fromProvider = _findTrip(ref.read(tripsProvider).valueOrNull ?? []);
    return fromProvider ?? _tripFromCache();
  }

  LatLng get _initialTarget {
    final t = _trip;
    if (_position != null) {
      return LatLng(_position!.latitude, _position!.longitude);
    }
    if (t?.hasPickupCoords == true) {
      return LatLng(t!.pickupLatitude!, t.pickupLongitude!);
    }
    if (t?.hasDropCoords == true) {
      return LatLng(t!.dropLatitude!, t.dropLongitude!);
    }
    return const LatLng(defaultMapLat, defaultMapLng);
  }

  Set<Marker> _markers(Trip trip) {
    final markers = <Marker>{};
    if (trip.hasPickupCoords) {
      markers.add(Marker(
        markerId: const MarkerId('pickup'),
        position: LatLng(trip.pickupLatitude!, trip.pickupLongitude!),
        icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen),
        infoWindow: InfoWindow(
          title: 'Pickup',
          snippet: trip.pickupAddress ?? trip.customerName,
        ),
      ));
    }
    if (trip.hasDropCoords) {
      markers.add(Marker(
        markerId: const MarkerId('dropoff'),
        position: LatLng(trip.dropLatitude!, trip.dropLongitude!),
        icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueRed),
        infoWindow: InfoWindow(
          title: 'Drop-off',
          snippet: trip.dropoffAddress ?? '',
        ),
      ));
    }
    if (_position != null) {
      final status = resolveFleetStatus(
        speedKmh: (_position!.speed * 3.6).clamp(0, 200).toDouble(),
      );
      final fleetStatus = _toFleetStatus(status);
      markers.add(Marker(
        markerId: const MarkerId('driver'),
        position: LatLng(_position!.latitude, _position!.longitude),
        rotation: _position!.heading.isFinite ? _position!.heading : 0,
        flat: true,
        anchor: const Offset(0.5, 0.5),
        icon: (_iconsReady ? _pulseIcons[fleetStatus] : null) ??
            BitmapDescriptor.defaultMarkerWithHue(
              status == FleetTrackStatus.moving
                  ? BitmapDescriptor.hueAzure
                  : BitmapDescriptor.hueOrange,
            ),
        infoWindow: const InfoWindow(title: 'You'),
      ));
    }
    return markers;
  }

  Set<Polyline> _polylines(Trip trip) {
    final lines = <Polyline>{};
    final status = resolveFleetStatus(
      speedKmh: _position == null
          ? 0.0
          : (_position!.speed * 3.6).clamp(0, 200).toDouble(),
    );
    if (_trail.length >= 2) {
      lines.add(Polyline(
        polylineId: const PolylineId('trail'),
        points: List.of(_trail),
        color: Color(colorForStatus(status)),
        width: 5,
      ));
    }

    // Straight route preview pickup → drop (ERP trip detail uses external directions;
    // in-app we show a guide line until Directions API is configured).
    if (trip.hasPickupCoords && trip.hasDropCoords) {
      lines.add(Polyline(
        polylineId: const PolylineId('route'),
        points: [
          LatLng(trip.pickupLatitude!, trip.pickupLongitude!),
          LatLng(trip.dropLatitude!, trip.dropLongitude!),
        ],
        color: const Color(GpsStatusColors.route),
        width: 4,
        patterns: [PatternItem.dash(18), PatternItem.gap(10)],
      ));
    } else if (_position != null && trip.hasPickupCoords) {
      lines.add(Polyline(
        polylineId: const PolylineId('toPickup'),
        points: [
          LatLng(_position!.latitude, _position!.longitude),
          LatLng(trip.pickupLatitude!, trip.pickupLongitude!),
        ],
        color: const Color(GpsStatusColors.pickup),
        width: 4,
        patterns: [PatternItem.dash(14), PatternItem.gap(8)],
      ));
    }
    return lines;
  }

  Future<void> _fitRoute(Trip trip) async {
    if (_map == null) return;
    final pts = <LatLng>[];
    if (_position != null) {
      pts.add(LatLng(_position!.latitude, _position!.longitude));
    }
    if (trip.hasPickupCoords) {
      pts.add(LatLng(trip.pickupLatitude!, trip.pickupLongitude!));
    }
    if (trip.hasDropCoords) {
      pts.add(LatLng(trip.dropLatitude!, trip.dropLongitude!));
    }
    if (pts.isEmpty) return;
    if (pts.length == 1) {
      await _map!.animateCamera(CameraUpdate.newLatLngZoom(pts.first, 15));
      return;
    }
    double minLat = pts.first.latitude, maxLat = pts.first.latitude;
    double minLng = pts.first.longitude, maxLng = pts.first.longitude;
    for (final p in pts.skip(1)) {
      minLat = minLat < p.latitude ? minLat : p.latitude;
      maxLat = maxLat > p.latitude ? maxLat : p.latitude;
      minLng = minLng < p.longitude ? minLng : p.longitude;
      maxLng = maxLng > p.longitude ? maxLng : p.longitude;
    }
    await _map!.animateCamera(CameraUpdate.newLatLngBounds(
      LatLngBounds(
        southwest: LatLng(minLat, minLng),
        northeast: LatLng(maxLat, maxLng),
      ),
      64,
    ));
  }

  Future<void> _recenter() async {
    setState(() => _follow = true);
    if (_position == null || _map == null) return;
    await _map!.animateCamera(CameraUpdate.newLatLngZoom(
      LatLng(_position!.latitude, _position!.longitude),
      16,
    ));
  }

  LatLng? _navTarget(Trip trip) {
    // Enroute / at drop-off phase → navigate to destination
    final toDrop = trip.lifecycleStatus == 7 || // Enroute
        trip.lifecycleStatus == 6 || // AtPickup — after onboard, head to drop
        trip.canComplete;
    if (toDrop && trip.hasDropCoords) {
      return LatLng(trip.dropLatitude!, trip.dropLongitude!);
    }
    if (trip.hasPickupCoords && !toDrop) {
      return LatLng(trip.pickupLatitude!, trip.pickupLongitude!);
    }
    if (trip.hasDropCoords) {
      return LatLng(trip.dropLatitude!, trip.dropLongitude!);
    }
    if (trip.hasPickupCoords) {
      return LatLng(trip.pickupLatitude!, trip.pickupLongitude!);
    }
    return null;
  }

  Future<void> _startExternal(Trip trip, String app) async {
    final target = _navTarget(trip);
    if (target == null) {
      final fallback = trip.googleDirectionsUrl;
      if (fallback != null && fallback.isNotEmpty) {
        setState(() => _navigating = true);
        final ok = await ExternalMapsLauncher.openUrl(fallback);
        if (mounted) {
          setState(() => _navigating = false);
          if (!ok) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Could not open maps app')),
            );
          }
        }
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No destination coordinates available')),
      );
      return;
    }
    setState(() => _navigating = true);
    final originLat = _position?.latitude;
    final originLng = _position?.longitude;
    bool ok = false;
    switch (app) {
      case 'google':
        ok = await ExternalMapsLauncher.openGoogleMaps(
          originLat: originLat,
          originLng: originLng,
          destLat: target.latitude,
          destLng: target.longitude,
          fallbackUrl: trip.googleDirectionsUrl,
        );
      case 'apple':
        ok = await ExternalMapsLauncher.openAppleMaps(
          destLat: target.latitude,
          destLng: target.longitude,
          originLat: originLat,
          originLng: originLng,
        );
      case 'waze':
        ok = await ExternalMapsLauncher.openWaze(
          destLat: target.latitude,
          destLng: target.longitude,
        );
    }
    if (mounted) {
      setState(() => _navigating = false);
      if (!ok) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not open maps app')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final tripsAsync = ref.watch(tripsProvider);
    final cached = _tripFromCache();

    if (cached != null && tripsAsync.isLoading) {
      return _buildScaffold(cached);
    }

    return tripsAsync.when(
      loading: () {
        if (cached != null) return _buildScaffold(cached);
        return const Scaffold(
          body: Center(child: CircularProgressIndicator()),
        );
      },
      error: (e, _) {
        if (cached != null) return _buildScaffold(cached);
        return Scaffold(body: Center(child: Text('$e')));
      },
      data: (trips) {
        final trip = _findTrip(trips) ?? cached;
        if (trip == null) {
          return const Scaffold(body: Center(child: Text('Trip not found')));
        }
        return _buildScaffold(trip);
      },
    );
  }

  Widget _buildScaffold(Trip trip) {
    if (!_mapsAvailable) {
      return _NavigationFallback(
        trip: trip,
        onGoogle: () => _startExternal(trip, 'google'),
        onApple: () => _startExternal(trip, 'apple'),
        onWaze: () => _startExternal(trip, 'waze'),
      );
    }

    return _buildMapScaffold(trip);
  }

  Widget _buildMapScaffold(Trip trip) {
    final speedKmh = _position == null
        ? 0.0
        : (_position!.speed * 3.6).clamp(0, 200).toDouble();
    final status = resolveFleetStatus(speedKmh: speedKmh);
    final localEta = _computeLocalEta(trip);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Navigate'),
        actions: [
          IconButton(
            tooltip: _traffic ? 'Hide traffic' : 'Show traffic',
            onPressed: () => setState(() => _traffic = !_traffic),
            icon: Icon(
              _traffic ? Icons.traffic : Icons.traffic_outlined,
              color: _traffic ? AppColors.primary : null,
            ),
          ),
          IconButton(
            tooltip: 'Fit route',
            onPressed: () => _fitRoute(trip),
            icon: const Icon(Icons.list_alt_rounded),
          ),
        ],
      ),
      body: Stack(
        children: [
          GoogleMap(
            initialCameraPosition: CameraPosition(
              target: _initialTarget,
              zoom: 13,
            ),
            myLocationEnabled: true,
            myLocationButtonEnabled: false,
            trafficEnabled: _traffic,
            compassEnabled: true,
            mapToolbarEnabled: false,
            markers: _markers(trip),
            polylines: _polylines(trip),
            onMapCreated: (c) {
              _map = c;
              Future.delayed(const Duration(milliseconds: 400), () {
                if (mounted) _fitRoute(trip);
              });
            },
            onCameraMoveStarted: () {
              // User panned — pause follow (ERP followSelected pattern)
              if (_follow) setState(() => _follow = false);
            },
          ),
          if (_error != null)
            Positioned(
              top: 12,
              left: 12,
              right: 12,
              child: Material(
                color: AppColors.error,
                borderRadius: BorderRadius.circular(8),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(_error!,
                      style: const TextStyle(color: Colors.white)),
                ),
              ),
            ),
          Positioned(
            left: 12,
            right: 12,
            bottom: 12,
            child: _BottomPanel(
              trip: trip,
              status: status,
              speedKmh: speedKmh,
              eta: _eta,
              localDistanceKm: localEta.$1,
              localEtaMinutes: localEta.$2,
              navigating: _navigating,
              follow: _follow,
              onRecenter: _recenter,
              onRefreshEta: _refreshEta,
              onGoogle: () => _startExternal(trip, 'google'),
              onApple: () => _startExternal(trip, 'apple'),
              onWaze: () => _startExternal(trip, 'waze'),
            ),
          ),
        ],
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.endFloat,
      floatingActionButton: Padding(
        padding: const EdgeInsets.only(bottom: 200),
        child: FloatingActionButton.small(
          heroTag: 'recenter',
          onPressed: _recenter,
          backgroundColor: _follow ? AppColors.primary : Colors.white,
          foregroundColor: _follow ? Colors.white : AppColors.primary,
          child: const Icon(Icons.my_location),
        ),
      ),
    );
  }

  (double?, int?) _computeLocalEta(Trip trip) {
    if (_position == null) return (null, null);
    final target = _navTarget(trip);
    if (target == null) return (null, null);
    final km = haversineKm(
      _position!.latitude,
      _position!.longitude,
      target.latitude,
      target.longitude,
    );
    return (km, etaMinutesFromKm(km)    );
  }
}

/// In-app map requires a Google Maps API key. Without it, offer external navigation.
class _NavigationFallback extends StatelessWidget {
  const _NavigationFallback({
    required this.trip,
    required this.onGoogle,
    required this.onApple,
    required this.onWaze,
  });

  final Trip trip;
  final VoidCallback onGoogle;
  final VoidCallback onApple;
  final VoidCallback onWaze;

  @override
  Widget build(BuildContext context) {
    final pickup = trip.pickupAddress ?? trip.routeName;
    final drop = trip.dropoffAddress ?? 'Drop-off';

    return Scaffold(
      appBar: AppBar(title: const Text('Navigate')),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Icon(Icons.map_outlined,
                size: 56, color: AppColors.primary.withValues(alpha: 0.85)),
            const SizedBox(height: 12),
            const Text(
              'Turn-by-turn navigation',
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w800,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'In-app map is unavailable (Google Maps key not configured). '
              'Open your preferred maps app for directions.',
              style: TextStyle(color: AppColors.textSecondary.withValues(alpha: 0.95)),
            ),
            const SizedBox(height: 20),
            _RouteLine(label: 'Pickup', value: pickup, color: AppColors.success),
            const SizedBox(height: 10),
            _RouteLine(label: 'Drop-off', value: drop, color: AppColors.error),
            const Spacer(),
            FilledButton.icon(
              onPressed: onGoogle,
              icon: const Icon(Icons.navigation_rounded),
              label: const Text('OPEN GOOGLE MAPS'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                minimumSize: const Size.fromHeight(48),
              ),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: onApple,
              icon: const Icon(Icons.map),
              label: const Text('OPEN APPLE MAPS'),
              style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: onWaze,
              icon: const Icon(Icons.directions_car),
              label: const Text('OPEN WAZE'),
              style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            ),
          ],
        ),
      ),
    );
  }
}

class _RouteLine extends StatelessWidget {
  const _RouteLine({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.circle, size: 10, color: color),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondary)),
              Text(value,
                  style: const TextStyle(
                      fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
            ],
          ),
        ),
      ],
    );
  }
}

class _BottomPanel extends StatelessWidget {
  const _BottomPanel({
    required this.trip,
    required this.status,
    required this.speedKmh,
    required this.eta,
    required this.localDistanceKm,
    required this.localEtaMinutes,
    required this.navigating,
    required this.follow,
    required this.onRecenter,
    required this.onRefreshEta,
    required this.onGoogle,
    required this.onApple,
    required this.onWaze,
  });

  final Trip trip;
  final FleetTrackStatus status;
  final double speedKmh;
  final GpsEta? eta;
  final double? localDistanceKm;
  final int? localEtaMinutes;
  final bool navigating;
  final bool follow;
  final VoidCallback onRecenter;
  final VoidCallback onRefreshEta;
  final VoidCallback onGoogle;
  final VoidCallback onApple;
  final VoidCallback onWaze;

  @override
  Widget build(BuildContext context) {
    final distance = eta?.distanceKm ?? localDistanceKm;
    final minutes = eta?.etaMinutes ?? localEtaMinutes;
    final destLabel = trip.isStarted ? 'Drop-off' : 'Pickup';

    return Material(
      elevation: 8,
      borderRadius: BorderRadius.circular(16),
      color: Theme.of(context).cardColor,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Container(
                  width: 10,
                  height: 10,
                  decoration: BoxDecoration(
                    color: Color(colorForStatus(status)),
                    shape: BoxShape.circle,
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  status.name.toUpperCase(),
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 12,
                    color: Color(colorForStatus(status)),
                  ),
                ),
                const Spacer(),
                Text(
                  '${speedKmh.toStringAsFixed(0)} km/h',
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    color: AppColors.textSecondary,
                  ),
                ),
                IconButton(
                  tooltip: 'Refresh ETA',
                  onPressed: onRefreshEta,
                  icon: const Icon(Icons.refresh, size: 20),
                ),
              ],
            ),
            Text(
              trip.routeName,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 15,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              '$destLabel · ${trip.isStarted ? (trip.dropoffAddress ?? 'Destination') : (trip.pickupAddress ?? 'Pickup')}',
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                _StatChip(
                  icon: Icons.straighten,
                  label: distance != null
                      ? '${distance.toStringAsFixed(1)} km'
                      : '— km',
                ),
                const SizedBox(width: 8),
                _StatChip(
                  icon: Icons.schedule,
                  label: minutes != null ? '$minutes min' : '— min',
                ),
                const SizedBox(width: 8),
                _StatChip(
                  icon: follow ? Icons.gps_fixed : Icons.gps_not_fixed,
                  label: follow ? 'Follow' : 'Free',
                  onTap: onRecenter,
                ),
              ],
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: navigating ? null : onGoogle,
              icon: navigating
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.map_outlined),
              label: const Text('OPEN GOOGLE MAPS'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                minimumSize: const Size.fromHeight(48),
              ),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: navigating ? null : onGoogle,
              icon: const Icon(Icons.play_arrow_rounded),
              label: Text(trip.isStarted ? 'CONTINUE TRIP' : 'START TRIP'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.primary,
                side: const BorderSide(color: AppColors.primary, width: 1.5),
                minimumSize: const Size.fromHeight(48),
              ),
            ),
            const SizedBox(height: 6),
            Row(
              children: [
                Expanded(
                  child: TextButton(
                    onPressed: navigating ? null : onApple,
                    child: const Text('Apple Maps',
                        style: TextStyle(fontSize: 12)),
                  ),
                ),
                Expanded(
                  child: TextButton(
                    onPressed: navigating ? null : onWaze,
                    child: const Text('Waze', style: TextStyle(fontSize: 12)),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StatChip extends StatelessWidget {
  const _StatChip({
    required this.icon,
    required this.label,
    this.onTap,
  });
  final IconData icon;
  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final child = Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.7),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.divider),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: AppColors.primary),
          const SizedBox(width: 4),
          Text(label,
              style: const TextStyle(
                  fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
    return onTap == null
        ? child
        : InkWell(onTap: onTap, borderRadius: BorderRadius.circular(20), child: child);
  }
}
