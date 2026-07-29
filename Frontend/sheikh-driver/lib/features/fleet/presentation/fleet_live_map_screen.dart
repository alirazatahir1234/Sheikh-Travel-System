import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import 'fleet_hub_notifier.dart';
import 'vehicle_commands_sheet.dart';
import 'widgets/fleet_kpi_strip.dart';
import 'widgets/geofence_distance.dart';
import 'widgets/vehicle_comms_buttons.dart';

class FleetLiveMapScreen extends ConsumerStatefulWidget {
  const FleetLiveMapScreen({super.key});

  @override
  ConsumerState<FleetLiveMapScreen> createState() => _FleetLiveMapScreenState();
}

class _FleetLiveMapScreenState extends ConsumerState<FleetLiveMapScreen> {
  GoogleMapController? _map;
  int? _selectedId;
  bool _fitted = false;
  bool _follow = false;
  List<GpsGeofenceItem> _geofences = const [];
  Timer? _refreshTimer;
  DateTime? _lastRefreshOk;
  MapType _mapType = MapType.normal;
  bool _sheetExpanded = false;

  static const _defaultCamera = CameraPosition(
    target: LatLng(24.8607, 67.0011), // Karachi default
    zoom: 11,
  );

  @override
  void initState() {
    super.initState();
    _loadGeofences();
    _refreshTimer = Timer.periodic(const Duration(seconds: 20), (_) {
      if (!mounted) return;
      ref.read(fleetHubProvider.notifier).refresh().then((_) {
        if (mounted) setState(() => _lastRefreshOk = DateTime.now());
      });
    });
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
    _refreshTimer?.cancel();
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

  Color _connectionColor() {
    final t = _lastRefreshOk;
    if (t == null) return AppColors.textMuted;
    final age = DateTime.now().difference(t);
    if (age.inSeconds < 45) return AppColors.success;
    if (age.inMinutes < 2) return AppColors.warning;
    return AppColors.error;
  }

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

  Future<void> _openExternalNav(FleetVehicleLocation v) async {
    if (!v.hasMapCoords) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${v.latitude},${v.longitude}',
    );
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
  }

  Future<void> _searchVehicle(List<FleetVehicleLocation> visible) async {
    final picked = await showDialog<int>(
      context: context,
      builder: (ctx) => SimpleDialog(
        title: const Text('Find vehicle'),
        children: visible
            .map(
              (v) => SimpleDialogOption(
                onPressed: () => Navigator.pop(ctx, v.vehicleId),
                child: Text('${v.vehicleName} · ${v.registrationNumber}'),
              ),
            )
            .toList(),
      ),
    );
    if (picked != null) {
      setState(() {
        _selectedId = picked;
        _follow = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(fleetHubProvider);
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session != null &&
        session.hasAnyPermission(const [
          FleetPermissions.gpsCommandSend,
          FleetPermissions.gpsCommandView,
        ]);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Row(
          children: [
            const Text('Live map'),
            const SizedBox(width: 8),
            Icon(
              Icons.circle,
              size: 10,
              color: _connectionColor(),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: _follow ? 'Stop follow' : 'Follow vehicle',
            icon: Icon(
              _follow ? Icons.gps_fixed : Icons.gps_not_fixed,
              color: _follow ? AppColors.primary : null,
            ),
            onPressed: _selectedId == null
                ? null
                : () => setState(() => _follow = !_follow),
          ),
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () {
              _fitted = false;
              ref.read(fleetHubProvider.notifier).refresh().then((_) {
                if (mounted) setState(() => _lastRefreshOk = DateTime.now());
              });
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

          if (_follow && selected?.hasMapCoords == true) {
            unawaited(
              _map?.animateCamera(
                CameraUpdate.newLatLngZoom(
                  LatLng(selected!.latitude!, selected.longitude!),
                  15,
                ),
              ),
            );
          }

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
                      mapType: _mapType,
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
                    Positioned(
                      top: 12,
                      right: 12,
                      child: Column(
                        children: [
                          _MapFab(
                            icon: Icons.layers_outlined,
                            tooltip: 'Map type',
                            onPressed: () => setState(() {
                              _mapType = _mapType == MapType.normal
                                  ? MapType.hybrid
                                  : MapType.normal;
                            }),
                          ),
                          const SizedBox(height: 8),
                          _MapFab(
                            icon: Icons.search_rounded,
                            tooltip: 'Search vehicle',
                            onPressed: () => _searchVehicle(visible),
                          ),
                          const SizedBox(height: 8),
                          _MapFab(
                            icon: Icons.refresh_rounded,
                            tooltip: 'Refresh',
                            onPressed: () {
                              _fitted = false;
                              ref.read(fleetHubProvider.notifier).refresh().then((_) {
                                if (mounted) {
                                  setState(() => _lastRefreshOk = DateTime.now());
                                }
                              });
                            },
                          ),
                        ],
                      ),
                    ),
                    if (selected != null)
                      Positioned(
                        left: 12,
                        right: 12,
                        bottom: 16,
                        child: _SelectedCard(
                          vehicle: selected,
                          geofences: _geofences,
                          follow: _follow,
                          expanded: _sheetExpanded,
                          canCommands: canCommands,
                          onExpandToggle: () =>
                              setState(() => _sheetExpanded = !_sheetExpanded),
                          onFollowToggle: () =>
                              setState(() => _follow = !_follow),
                          onNavigate: () => _openExternalNav(selected),
                          onCommands: canCommands
                              ? () => showVehicleCommandsSheet(
                                    context,
                                    selected.vehicleId,
                                  )
                              : null,
                          onOpen: () => context
                              .push('/fleet/vehicles/${selected.vehicleId}'),
                          onClose: () => setState(() {
                            _selectedId = null;
                            _follow = false;
                            _sheetExpanded = false;
                          }),
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

class _MapFab extends StatelessWidget {
  const _MapFab({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      elevation: 3,
      shape: const CircleBorder(),
      child: IconButton(
        tooltip: tooltip,
        icon: Icon(icon, size: 22),
        onPressed: onPressed,
      ),
    );
  }
}

class _SelectedCard extends StatelessWidget {
  const _SelectedCard({
    required this.vehicle,
    required this.geofences,
    required this.onOpen,
    required this.onClose,
    required this.follow,
    required this.onFollowToggle,
    required this.expanded,
    required this.onExpandToggle,
    required this.onNavigate,
    required this.canCommands,
    this.onCommands,
  });

  final FleetVehicleLocation vehicle;
  final List<GpsGeofenceItem> geofences;
  final VoidCallback onOpen;
  final VoidCallback onClose;
  final bool follow;
  final VoidCallback onFollowToggle;
  final bool expanded;
  final VoidCallback onExpandToggle;
  final VoidCallback onNavigate;
  final bool canCommands;
  final VoidCallback? onCommands;

  @override
  Widget build(BuildContext context) {
    final color = fleetStatusColor(vehicle.status);
    NearestGeofenceInfo? nearest;
    if (vehicle.hasMapCoords) {
      nearest = nearestCircleGeofence(
        lat: vehicle.latitude!,
        lng: vehicle.longitude!,
        fences: geofences,
      );
    }
    return Material(
      elevation: 6,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
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
                        '${vehicle.registrationNumber} · ${vehicle.status.label} · ${vehicle.speed.toStringAsFixed(0)} km/h',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                      if ((vehicle.driverName ?? '').isNotEmpty)
                        Text(
                          'Driver ${vehicle.driverName}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                          ),
                        ),
                      if (nearest != null)
                        Text(
                          nearest.inside
                              ? 'Inside ${nearest.name}'
                              : '${nearest.name} · ${nearest.distanceMeters.toStringAsFixed(0)} m away',
                          style: const TextStyle(
                            fontSize: 11,
                            color: AppColors.textSecondary,
                          ),
                        ),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: expanded ? 'Collapse' : 'Expand',
                  onPressed: onExpandToggle,
                  icon: Icon(
                    expanded
                        ? Icons.expand_less_rounded
                        : Icons.expand_more_rounded,
                  ),
                ),
                IconButton(
                  tooltip: follow ? 'Stop follow' : 'Follow',
                  onPressed: onFollowToggle,
                  icon: Icon(
                    follow ? Icons.gps_fixed : Icons.gps_not_fixed,
                    color: follow ? AppColors.primary : AppColors.textMuted,
                  ),
                ),
                IconButton(
                  onPressed: onClose,
                  icon: const Icon(Icons.close_rounded),
                ),
              ],
            ),
            if (expanded) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  _MiniChip(
                    label: vehicle.ignition == true ? 'Ignition ON' : 'Ignition OFF',
                  ),
                  const SizedBox(width: 6),
                  _MiniChip(
                    label: vehicle.hasGps ? 'GPS' : 'No GPS',
                  ),
                  if (vehicle.batteryLevel != null) ...[
                    const SizedBox(width: 6),
                    _MiniChip(
                      label: '${vehicle.batteryLevel!.toStringAsFixed(0)}% batt',
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 8),
              VehicleCommsButtons(
                phone: vehicle.driverPhone,
                vehicleLabel: vehicle.vehicleName,
              ),
            ],
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: onNavigate,
                    child: const Text('Navigate'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton(
                    onPressed: onOpen,
                    child: const Text('Details'),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: FilledButton(
                    onPressed: () => context.push(
                      '/fleet/vehicles/${vehicle.vehicleId}/history',
                    ),
                    child: const Text('Playback'),
                  ),
                ),
                if (canCommands && onCommands != null) ...[
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton(
                      onPressed: onCommands,
                      child: const Text('Commands'),
                    ),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _MiniChip extends StatelessWidget {
  const _MiniChip({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadii.pill),
        border: Border.all(color: AppColors.border),
      ),
      child: Text(label, style: const TextStyle(fontSize: 11)),
    );
  }
}
