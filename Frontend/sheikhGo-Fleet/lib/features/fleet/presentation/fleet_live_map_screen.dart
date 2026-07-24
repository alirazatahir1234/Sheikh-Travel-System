import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../core/constants/app_theme.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import 'fleet_hub_notifier.dart';
import 'widgets/fleet_kpi_strip.dart';

class FleetLiveMapScreen extends ConsumerStatefulWidget {
  const FleetLiveMapScreen({super.key});

  @override
  ConsumerState<FleetLiveMapScreen> createState() => _FleetLiveMapScreenState();
}

class _FleetLiveMapScreenState extends ConsumerState<FleetLiveMapScreen> {
  GoogleMapController? _map;
  int? _selectedId;
  bool _fitted = false;
  List<GpsGeofenceItem> _geofences = const [];

  static const _defaultCamera = CameraPosition(
    target: LatLng(24.8607, 67.0011), // Karachi default
    zoom: 11,
  );

  @override
  void initState() {
    super.initState();
    _loadGeofences();
  }

  Future<void> _loadGeofences() async {
    try {
      final rows = await ref.read(fleetApiProvider).getGeofences();
      if (!mounted) return;
      setState(() => _geofences = rows.where((g) => g.isActive).toList());
    } catch (_) {}
  }

  Set<Circle> _geofenceCircles() {
    return _geofences
        .where((g) => g.isCircle && g.radiusMeters > 0)
        .map(
          (g) => Circle(
            circleId: CircleId('gf${g.id}'),
            center: LatLng(g.centerLat, g.centerLng),
            radius: g.radiusMeters,
            fillColor: AppColors.primary.withValues(alpha: 0.12),
            strokeColor: AppColors.primary.withValues(alpha: 0.55),
            strokeWidth: 2,
          ),
        )
        .toSet();
  }

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
  }

  Set<Marker> _markers(List<FleetVehicleLocation> locations) {
    return locations
        .where((v) => v.hasMapCoords)
        .map(
          (v) => Marker(
            markerId: MarkerId('v${v.vehicleId}'),
            position: LatLng(v.latitude!, v.longitude!),
            rotation: v.heading ?? 0,
            icon: BitmapDescriptor.defaultMarkerWithHue(
              _hueFor(v.status),
            ),
            infoWindow: InfoWindow(
              title: v.vehicleName,
              snippet:
                  '${v.status.label} · ${v.speed.toStringAsFixed(0)} km/h',
              onTap: () => context.push('/fleet/vehicles/${v.vehicleId}'),
            ),
            onTap: () => setState(() => _selectedId = v.vehicleId),
          ),
        )
        .toSet();
  }

  double _hueFor(FleetTrackStatus status) => switch (status) {
        FleetTrackStatus.moving => BitmapDescriptor.hueGreen,
        FleetTrackStatus.idle => BitmapDescriptor.hueYellow,
        FleetTrackStatus.parked => BitmapDescriptor.hueAzure,
        FleetTrackStatus.sos => BitmapDescriptor.hueRed,
        FleetTrackStatus.offline => BitmapDescriptor.hueViolet,
        FleetTrackStatus.neverSeen => BitmapDescriptor.hueRose,
      };

  Future<void> _fitBounds(List<FleetVehicleLocation> locations) async {
    final map = _map;
    if (map == null || _fitted) return;
    final pts = locations
        .where((v) => v.hasMapCoords)
        .map((v) => LatLng(v.latitude!, v.longitude!))
        .toList();
    if (pts.isEmpty) return;
    if (pts.length == 1) {
      await map.animateCamera(
        CameraUpdate.newLatLngZoom(pts.first, 14),
      );
    } else {
      var minLat = pts.first.latitude;
      var maxLat = pts.first.latitude;
      var minLng = pts.first.longitude;
      var maxLng = pts.first.longitude;
      for (final p in pts) {
        minLat = minLat < p.latitude ? minLat : p.latitude;
        maxLat = maxLat > p.latitude ? maxLat : p.latitude;
        minLng = minLng < p.longitude ? minLng : p.longitude;
        maxLng = maxLng > p.longitude ? maxLng : p.longitude;
      }
      await map.animateCamera(
        CameraUpdate.newLatLngBounds(
          LatLngBounds(
            southwest: LatLng(minLat, minLng),
            northeast: LatLng(maxLat, maxLng),
          ),
          56,
        ),
      );
    }
    _fitted = true;
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(fleetHubProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Fleet live map'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () {
              _fitted = false;
              ref.read(fleetHubProvider.notifier).refresh();
            },
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (hub) {
          final visible = hub.visible;
          final markers = _markers(visible);
          final selected = _selectedId == null
              ? null
              : visible.where((v) => v.vehicleId == _selectedId).firstOrNull;

          return Column(
            children: [
              const SizedBox(height: 8),
              FleetKpiStrip(
                kpis: hub.kpis,
                selected: hub.filter,
                onSelect: (s) {
                  _fitted = false;
                  ref.read(fleetHubProvider.notifier).setFilter(s);
                },
              ),
              const SizedBox(height: 8),
              Expanded(
                child: Stack(
                  children: [
                    GoogleMap(
                      initialCameraPosition: _defaultCamera,
                      markers: markers,
                      circles: _geofenceCircles(),
                      myLocationButtonEnabled: false,
                      zoomControlsEnabled: false,
                      mapToolbarEnabled: false,
                      onMapCreated: (c) {
                        _map = c;
                        unawaited(_fitBounds(visible));
                      },
                      onTap: (_) => setState(() => _selectedId = null),
                    ),
                    if (visible.where((v) => v.hasMapCoords).isEmpty)
                      const Positioned.fill(
                        child: IgnorePointer(
                          child: Center(
                            child: Card(
                              child: Padding(
                                padding: EdgeInsets.all(16),
                                child: Text('No vehicles with GPS positions'),
                              ),
                            ),
                          ),
                        ),
                      ),
                    if (selected != null)
                      Positioned(
                        left: 12,
                        right: 12,
                        bottom: 16,
                        child: _SelectedCard(
                          vehicle: selected,
                          onOpen: () => context
                              .push('/fleet/vehicles/${selected.vehicleId}'),
                          onClose: () => setState(() => _selectedId = null),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _SelectedCard extends StatelessWidget {
  const _SelectedCard({
    required this.vehicle,
    required this.onOpen,
    required this.onClose,
  });

  final FleetVehicleLocation vehicle;
  final VoidCallback onOpen;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final color = fleetStatusColor(vehicle.status);
    return Material(
      elevation: 6,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Container(
              width: 10,
              height: 40,
              decoration: BoxDecoration(
                color: color,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    vehicle.vehicleName,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                    ),
                  ),
                  Text(
                    '${vehicle.registrationNumber} · ${vehicle.status.label}',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            TextButton(onPressed: onOpen, child: const Text('Details')),
            IconButton(
              onPressed: onClose,
              icon: const Icon(Icons.close_rounded),
            ),
          ],
        ),
      ),
    );
  }
}
