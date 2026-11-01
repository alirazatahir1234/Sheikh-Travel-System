import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import 'fleet_hub_notifier.dart';
import 'vehicle_commands_sheet.dart';
import '../services/fleet_realtime_service.dart';
import 'widgets/fleet_kpi_strip.dart';
import 'widgets/pulse_vehicle_overlay.dart';
import 'widgets/vehicle_live_map_sheet.dart';
import 'widgets/vehicle_pulse_icons.dart';
import '../../../shared/widgets/sg_ui.dart';

class FleetLiveMapScreen extends ConsumerStatefulWidget {
  const FleetLiveMapScreen({super.key});

  @override
  ConsumerState<FleetLiveMapScreen> createState() => _FleetLiveMapScreenState();
}

class _FleetLiveMapScreenState extends ConsumerState<FleetLiveMapScreen> {
  GoogleMapController? _map;
  int? _selectedId;
  bool _initialFitDone = false;
  bool _userMovedCamera = false;
  bool _programmaticCamera = false;
  bool _follow = false;
  List<GpsGeofenceItem> _geofences = const [];
  MapType _mapType = MapType.normal;
  double _devicePixelRatio = 2;
  final Map<FleetTrackStatus, BitmapDescriptor> _icons = {};
  final Map<FleetTrackStatus, BitmapDescriptor> _selectedIcons = {};
  bool _iconsReady = false;
  LatLng? _lastFollowPos;
  Offset? _pulseScreenPos;
  final GlobalKey _mapStackKey = GlobalKey();
  bool _pulseSyncScheduled = false;

  static const _fallbackCamera = CameraPosition(
    target: LatLng(24.8607, 67.0011), // Karachi — only when no vehicles
    zoom: 11,
  );

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _devicePixelRatio = MediaQuery.devicePixelRatioOf(context);
      unawaited(_loadIcons());
    });
    _loadGeofences();
  }

  Future<void> _loadIcons() async {
    VehiclePulseIcons.clearCache();
    final next = <FleetTrackStatus, BitmapDescriptor>{};
    final selected = <FleetTrackStatus, BitmapDescriptor>{};
    for (final s in FleetTrackStatus.values) {
      next[s] = await VehiclePulseIcons.iconFor(
        s,
        devicePixelRatio: _devicePixelRatio,
      );
      selected[s] = await VehiclePulseIcons.iconFor(
        s,
        devicePixelRatio: _devicePixelRatio,
        selected: true,
      );
    }
    if (!mounted) return;
    setState(() {
      _icons
        ..clear()
        ..addAll(next);
      _selectedIcons
        ..clear()
        ..addAll(selected);
      _iconsReady = true;
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

  Set<Polygon> _geofencePolygons() {
    final out = <Polygon>{};
    for (final g in _geofences.where((x) => x.isPolygon)) {
      final points = _parseGeoJsonRing(g.geoJson);
      if (points.length < 3) continue;
      out.add(
        Polygon(
          polygonId: PolygonId('gfp${g.id}'),
          points: points,
          fillColor: AppColors.primary.withValues(alpha: 0.12),
          strokeColor: AppColors.primary.withValues(alpha: 0.55),
          strokeWidth: 2,
        ),
      );
    }
    return out;
  }

  /// Best-effort GeoJSON Polygon / MultiPolygon / Feature outer-ring parser.
  List<LatLng> _parseGeoJsonRing(String? geoJson) {
    final raw = geoJson?.trim();
    if (raw == null || raw.isEmpty) return const [];
    try {
      final decoded = jsonDecode(raw);
      List? ring;
      if (decoded is Map) {
        final geom = decoded['geometry'] is Map
            ? Map<String, dynamic>.from(decoded['geometry'] as Map)
            : decoded;
        final type = (geom['type'] ?? '').toString().toLowerCase();
        final coords = geom['coordinates'];
        if (type == 'polygon' && coords is List && coords.isNotEmpty) {
          ring = coords.first is List ? coords.first as List : null;
        } else if (type == 'multipolygon' &&
            coords is List &&
            coords.isNotEmpty) {
          final firstPoly = coords.first;
          if (firstPoly is List && firstPoly.isNotEmpty) {
            ring = firstPoly.first is List ? firstPoly.first as List : null;
          }
        } else if (coords is List &&
            coords.isNotEmpty &&
            coords.first is List) {
          // Bare coordinates array fallback.
          ring = coords.first as List;
        }
      } else if (decoded is List && decoded.isNotEmpty) {
        ring = decoded.first is List ? decoded.first as List : decoded;
      }
      if (ring == null) return const [];
      final points = <LatLng>[];
      for (final c in ring) {
        if (c is! List || c.length < 2) continue;
        final lng = (c[0] as num?)?.toDouble();
        final lat = (c[1] as num?)?.toDouble();
        if (lat == null || lng == null) continue;
        points.add(LatLng(lat, lng));
      }
      return points;
    } catch (_) {
      return const [];
    }
  }

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
  }

  BitmapDescriptor _iconFor(FleetTrackStatus status, {required bool selected}) {
    final map = selected ? _selectedIcons : _icons;
    return map[status] ??
        _icons[status] ??
        BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure);
  }

  Set<Marker> _markers(List<FleetVehicleLocation> locations) {
    return locations.where((v) => v.hasMapCoords).map(
      (v) {
        final selected = v.vehicleId == _selectedId;
        final rotates = VehiclePulseIcons.rotatesWithHeading(v.status);
        return Marker(
          markerId: MarkerId('v${v.vehicleId}'),
          position: LatLng(v.latitude!, v.longitude!),
          rotation: rotates ? (v.heading ?? 0) : 0,
          flat: true,
          anchor: const Offset(0.5, 0.5),
          zIndexInt: selected ? 10 : 1,
          icon: _iconFor(v.status, selected: selected),
          infoWindow: InfoWindow.noText,
          onTap: () => _selectVehicle(v.vehicleId),
        );
      },
    ).toSet();
  }

  Future<void> _zoomBy(double delta) async {
    final map = _map;
    if (map == null) return;
    _programmaticCamera = true;
    try {
      await map.animateCamera(CameraUpdate.zoomBy(delta));
    } catch (_) {
    } finally {
      Future<void>.delayed(const Duration(milliseconds: 400), () {
        _programmaticCamera = false;
      });
    }
  }

  void _schedulePulseSync(LatLng? target) {
    if (_pulseSyncScheduled) return;
    _pulseSyncScheduled = true;
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      _pulseSyncScheduled = false;
      if (!mounted) return;
      final offset = await mapLatLngToOverlayOffset(
        map: _map,
        target: target,
        stackKey: _mapStackKey,
        devicePixelRatio: _devicePixelRatio,
      );
      if (!mounted) return;
      if (offset != _pulseScreenPos) {
        setState(() => _pulseScreenPos = offset);
      }
    });
  }

  void _selectVehicle(int id) {
    setState(() {
      _selectedId = id;
      _follow = true;
      _userMovedCamera = false;
      _pulseScreenPos = null;
    });
    final hub = ref.read(fleetHubProvider).valueOrNull;
    final v = hub?.locations.where((e) => e.vehicleId == id).firstOrNull;
    if (v?.hasMapCoords == true) {
      final ll = LatLng(v!.latitude!, v.longitude!);
      unawaited(_centerOn(ll, zoom: 15));
      _schedulePulseSync(ll);
    }
  }

  Color _connectionColor() {
    final hub = ref.read(fleetHubProvider).valueOrNull;
    final status = hub?.realtimeStatus ?? 'disconnected';
    if (status == 'connected') return AppColors.success;
    if (status == 'reconnecting') return AppColors.warning;
    if (status == 'no_token') return AppColors.textMuted;
    return AppColors.error;
  }

  /// Prefer SignalR last event, then freshest GPS / fleet refresh from hub.
  DateTime? get _lastSyncAt {
    final hub = ref.read(fleetHubProvider).valueOrNull;
    final signalrAt = FleetRealtimeService.instance.lastLocationAt;
    final pollAt = hub?.lastGpsAt ?? hub?.lastFleetRefreshAt;
    if (signalrAt == null) return pollAt;
    if (pollAt == null) return signalrAt;
    return signalrAt.isAfter(pollAt) ? signalrAt : pollAt;
  }

  CameraPosition _initialCamera(List<FleetVehicleLocation> locations) {
    final pts = locations
        .where((v) => v.hasMapCoords)
        .map((v) => LatLng(v.latitude!, v.longitude!))
        .toList();
    if (pts.isEmpty) return _fallbackCamera;
    if (pts.length == 1) {
      return CameraPosition(target: pts.first, zoom: 14);
    }
    var sumLat = 0.0;
    var sumLng = 0.0;
    for (final p in pts) {
      sumLat += p.latitude;
      sumLng += p.longitude;
    }
    return CameraPosition(
      target: LatLng(sumLat / pts.length, sumLng / pts.length),
      zoom: 11,
    );
  }

  Future<void> _animateCamera(CameraUpdate update) async {
    final map = _map;
    if (map == null || !mounted) return;
    _programmaticCamera = true;
    try {
      await map.animateCamera(update);
    } catch (_) {
    } finally {
      Future<void>.delayed(const Duration(milliseconds: 400), () {
        _programmaticCamera = false;
      });
    }
  }

  Future<void> _fitBounds(List<FleetVehicleLocation> locations) async {
    final map = _map;
    if (map == null || _initialFitDone || _userMovedCamera) return;
    final pts = locations
        .where((v) => v.hasMapCoords)
        .map((v) => LatLng(v.latitude!, v.longitude!))
        .toList();
    if (pts.isEmpty) return;
    if (pts.length == 1) {
      await _animateCamera(CameraUpdate.newLatLngZoom(pts.first, 14));
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
      await _animateCamera(
        CameraUpdate.newLatLngBounds(
          LatLngBounds(
            southwest: LatLng(minLat, minLng),
            northeast: LatLng(maxLat, maxLng),
          ),
          56,
        ),
      );
    }
    _initialFitDone = true;
  }

  Future<void> _centerOn(LatLng target, {double? zoom}) async {
    if (zoom != null) {
      await _animateCamera(CameraUpdate.newLatLngZoom(target, zoom));
    } else {
      await _animateCamera(CameraUpdate.newLatLng(target));
    }
  }

  void _maybeFollowSelected(List<FleetVehicleLocation> visible) {
    if (!_follow || _selectedId == null) return;
    final selected =
        visible.where((v) => v.vehicleId == _selectedId).firstOrNull;
    if (selected?.hasMapCoords != true) return;
    final pos = LatLng(selected!.latitude!, selected.longitude!);
    final last = _lastFollowPos;
    if (last != null &&
        (last.latitude - pos.latitude).abs() < 0.00001 &&
        (last.longitude - pos.longitude).abs() < 0.00001) {
      return;
    }
    _lastFollowPos = pos;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_follow) return;
      unawaited(_centerOn(pos));
    });
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
    if (picked != null) _selectVehicle(picked);
  }

  Future<void> _manualRefresh({bool refit = false}) async {
    if (refit) {
      _initialFitDone = false;
      _userMovedCamera = false;
    }
    await ref.read(fleetHubProvider.notifier).refresh();
    if (refit && mounted) {
      final hub = ref.read(fleetHubProvider).valueOrNull;
      if (hub != null) unawaited(_fitBounds(hub.visible));
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(fleetHubProvider);
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session?.canUseGpsCommands ?? false;

    ref.listen<AsyncValue<FleetHubState>>(fleetHubProvider, (prev, next) {
      final hub = next.valueOrNull;
      if (hub == null) return;
      _maybeFollowSelected(hub.visible);
      if (_selectedId != null) {
        final sel =
            hub.visible.where((v) => v.vehicleId == _selectedId).firstOrNull;
        if (sel?.hasMapCoords == true) {
          _schedulePulseSync(LatLng(sel!.latitude!, sel.longitude!));
        }
      }
    });

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Row(
          children: [
            const Text('Live map'),
            const SizedBox(width: 8),
            Tooltip(
              message: () {
                final hub = async.valueOrNull;
                final status = hub?.realtimeStatus ?? 'disconnected';
                final sync = _lastSyncAt;
                final age = sync == null
                    ? 'no sync yet'
                    : '${DateTime.now().difference(sync).inSeconds}s ago';
                return 'Realtime: $status · Last update $age';
              }(),
              child: Icon(
                Icons.circle,
                size: 10,
                color: _connectionColor(),
              ),
            ),
            if (async.valueOrNull?.isRefreshing == true) ...[
              const SizedBox(width: 10),
              const SizedBox(
                width: 14,
                height: 14,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ],
          ],
        ),
        actions: [
          IconButton(
            tooltip: _follow ? 'Stop follow' : 'Follow vehicle',
            icon: Icon(
              _follow ? Icons.location_disabled : Icons.directions_run,
              color: _follow ? AppColors.primary : null,
            ),
            onPressed: _selectedId == null
                ? null
                : () {
                    setState(() {
                      _follow = !_follow;
                      if (_follow) _userMovedCamera = false;
                    });
                    if (_follow) {
                      final hub = async.valueOrNull;
                      final v = hub?.visible
                          .where((e) => e.vehicleId == _selectedId)
                          .firstOrNull;
                      if (v?.hasMapCoords == true) {
                        unawaited(
                          _centerOn(
                            LatLng(v!.latitude!, v.longitude!),
                            zoom: 15,
                          ),
                        );
                      }
                    }
                  },
          ),
          IconButton(
            tooltip: 'Refresh & fit',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => unawaited(_manualRefresh(refit: true)),
          ),
        ],
      ),
      body: async.when(
        loading: () => async.hasValue
            ? _buildMapBody(
                async.requireValue,
                canCommands: canCommands,
              )
            : const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () => unawaited(_manualRefresh(refit: true)),
        ),
        data: (hub) => _buildMapBody(hub, canCommands: canCommands),
      ),
    );
  }

  Widget _buildMapBody(
    FleetHubState hub, {
    required bool canCommands,
  }) {
    final visible = hub.visible;
    final markers = _iconsReady ? _markers(visible) : <Marker>{};
    final selected = _selectedId == null
        ? null
        : visible.where((v) => v.vehicleId == _selectedId).firstOrNull;

    // Full-bleed map with overlay KPI + controls (ERP-style large flexible layout).
    return Stack(
      key: _mapStackKey,
      children: [
        Positioned.fill(
          child: GoogleMap(
            initialCameraPosition: _initialCamera(visible),
            markers: markers,
            circles: _geofenceCircles(),
            polygons: _geofencePolygons(),
            mapType: _mapType,
            myLocationButtonEnabled: false,
            zoomControlsEnabled: false,
            mapToolbarEnabled: false,
            compassEnabled: true,
            onMapCreated: (c) {
              _map = c;
              unawaited(_fitBounds(visible));
              if (selected?.hasMapCoords == true) {
                _schedulePulseSync(
                  LatLng(selected!.latitude!, selected.longitude!),
                );
              }
            },
            onCameraMove: (_) {
              if (selected?.hasMapCoords == true) {
                _schedulePulseSync(
                  LatLng(selected!.latitude!, selected.longitude!),
                );
              }
            },
            onCameraMoveStarted: () {
              if (!_programmaticCamera) {
                _userMovedCamera = true;
                if (_follow) {
                  setState(() => _follow = false);
                }
              }
            },
            onTap: (_) => setState(() {
              _selectedId = null;
              _follow = false;
              _lastFollowPos = null;
              _pulseScreenPos = null;
            }),
          ),
        ),
        if (selected != null && selected.hasMapCoords)
          PulseVehicleOverlay(
            screenPosition: _pulseScreenPos,
            status: selected.status,
            headingDegrees: selected.heading ?? 0,
            visible: true,
            // Rings only — Google marker keeps the ERP glyph (matches ERP pulse HTML).
            showDisc: false,
            discRadius: 16,
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
          top: 8,
          left: 0,
          right: 0,
          child: Material(
            color: Colors.white.withValues(alpha: 0.92),
            elevation: 1,
            child: Padding(
              padding: const EdgeInsets.only(top: 6, bottom: 8),
              child: FleetKpiStrip(
                kpis: hub.kpis,
                selected: hub.statusFilter,
                onSelect: (s) {
                  _initialFitDone = false;
                  _userMovedCamera = false;
                  ref.read(fleetHubProvider.notifier).setStatusFilter(s);
                  WidgetsBinding.instance.addPostFrameCallback((_) {
                    if (!mounted) return;
                    final next = ref.read(fleetHubProvider).valueOrNull;
                    if (next != null) unawaited(_fitBounds(next.visible));
                  });
                },
              ),
            ),
          ),
        ),
        if (selected != null)
          Positioned.fill(
            child: VehicleLiveMapSheet(
              vehicle: selected,
              follow: _follow,
              canCommands: canCommands,
              onFollowToggle: () => setState(() {
                _follow = !_follow;
                if (_follow) _userMovedCamera = false;
              }),
              onCenter: () {
                if (selected.hasMapCoords) {
                  unawaited(
                    _centerOn(
                      LatLng(selected.latitude!, selected.longitude!),
                      zoom: 15,
                    ),
                  );
                }
              },
              onNavigate: () => _openExternalNav(selected),
              onCommands: canCommands
                  ? () => showVehicleCommandsSheet(
                        context,
                        selected.vehicleId,
                      )
                  : null,
              onClose: () => setState(() {
                _selectedId = null;
                _follow = false;
                _lastFollowPos = null;
                _pulseScreenPos = null;
              }),
            ),
          ),
        // Above sheet so +/- stay visible and tappable (ERP glass zoom cluster).
        Positioned(
          right: 12,
          top: 0,
          bottom: selected != null ? 140 : 0,
          child: Align(
            alignment: Alignment.centerRight,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                _MapFab(
                  icon: Icons.add,
                  tooltip: 'Zoom in',
                  onPressed: () => unawaited(_zoomBy(1)),
                ),
                const SizedBox(height: 8),
                _MapFab(
                  icon: Icons.remove,
                  tooltip: 'Zoom out',
                  onPressed: () => unawaited(_zoomBy(-1)),
                ),
                const SizedBox(height: 8),
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
              ],
            ),
          ),
        ),
      ],
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
      elevation: 4,
      shadowColor: Colors.black54,
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onPressed,
        child: Tooltip(
          message: tooltip,
          child: SizedBox(
            width: 44,
            height: 44,
            child: Icon(icon, size: 22, color: AppColors.textPrimary),
          ),
        ),
      ),
    );
  }
}
