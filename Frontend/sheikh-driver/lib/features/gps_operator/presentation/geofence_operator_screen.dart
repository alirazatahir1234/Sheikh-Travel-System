import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/widgets/geofence_distance.dart';
import '../data/geofence_api.dart';
import '../domain/geofence_models.dart';
import 'geofence_detail_sheet.dart';
import 'geofence_editor_screen.dart';

enum _ZoneView { list, map }

class GeofenceOperatorScreen extends ConsumerStatefulWidget {
  const GeofenceOperatorScreen({super.key});

  @override
  ConsumerState<GeofenceOperatorScreen> createState() =>
      _GeofenceOperatorScreenState();
}

class _GeofenceOperatorScreenState
    extends ConsumerState<GeofenceOperatorScreen> {
  List<GpsGeofenceItem> _rows = const [];
  List<GeofenceVehicleState> _states = const [];
  bool _loading = true;
  String? _error;
  String? _disabledMessage;
  int? _vehicleId;
  String? _areaType;
  bool? _isActive;
  _ZoneView _view = _ZoneView.list;
  final _search = TextEditingController();
  GoogleMapController? _map;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _search.dispose();
    _map?.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final api = ref.read(geofenceApiProvider);
    try {
      final rows = await api.list(
        search: _search.text,
        areaType: _areaType,
        isActive: _isActive,
        vehicleId: _vehicleId,
      );
      List<GeofenceVehicleState> states = const [];
      try {
        states = await api.states(vehicleId: _vehicleId);
      } catch (_) {
        // Status falls back to client-side geometry when state is unavailable.
      }
      if (!mounted) return;
      setState(() {
        _rows = rows;
        _states = states;
        _disabledMessage = null;
      });
      _fitMap();
    } on GeofencingDisabledException catch (e) {
      if (mounted) setState(() => _disabledMessage = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = ErrorHandler.message(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  bool get _canManage {
    final s = ref.read(fleetSessionProvider);
    if (s == null) return false;
    return s.hasPermission(FleetPermissions.gpsGeofenceManage) ||
        s.hasPermission(FleetPermissions.gpsDeviceManage);
  }

  /// Server state first (all shapes, debounced); client geometry as fallback.
  _ZoneStatus? _statusFor(GpsGeofenceItem g, FleetVehicleLocation? v) {
    if (_vehicleId == null) return null;
    final server = _states
        .where((s) => s.geofenceId == g.id && s.vehicleId == _vehicleId)
        .firstOrNull;
    if (server != null) {
      return _ZoneStatus(server.isInside, server.lastPositionUtc);
    }
    if (v == null || !v.hasMapCoords) return null;
    final inside = isInsideGeofence(v.latitude!, v.longitude!, g);
    return inside == null ? null : _ZoneStatus(inside, v.lastUpdated);
  }

  Future<void> _openEditor([GpsGeofenceItem? existing]) async {
    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => GeofenceEditorScreen(existing: existing)),
    );
    if (saved == true) _load();
  }

  void _openDetails(GpsGeofenceItem g) {
    showGeofenceDetailSheet(
      context,
      zone: g,
      canManage: _canManage,
      onEdit: () => _openEditor(g),
    );
  }

  void _fitMap() {
    final map = _map;
    if (map == null || _rows.isEmpty) return;
    double? minLat, maxLat, minLng, maxLng;
    void add(double lat, double lng) {
      minLat = minLat == null || lat < minLat! ? lat : minLat;
      maxLat = maxLat == null || lat > maxLat! ? lat : maxLat;
      minLng = minLng == null || lng < minLng! ? lng : minLng;
      maxLng = maxLng == null || lng > maxLng! ? lng : maxLng;
    }

    for (final g in _rows) {
      if (g.isCircle) {
        final pad = g.radiusMeters / 111000;
        add(g.centerLat - pad, g.centerLng - pad);
        add(g.centerLat + pad, g.centerLng + pad);
      } else {
        for (final p in parseGeofenceRing(g.geoJson)) {
          add(p.lat, p.lng);
        }
      }
    }
    if (minLat == null) return;
    map.animateCamera(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(
          southwest: LatLng(minLat!, minLng!),
          northeast: LatLng(maxLat! + 1e-5, maxLng! + 1e-5),
        ),
        48,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final vehicles = hub?.locations ?? const <FleetVehicleLocation>[];
    final selected = _vehicleId == null
        ? null
        : vehicles.where((v) => v.vehicleId == _vehicleId).firstOrNull;

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(
        title: const Text('Geofences'),
        actions: [
          if (_disabledMessage == null)
            IconButton(
              tooltip: _view == _ZoneView.list ? 'Map view' : 'List view',
              onPressed: () => setState(() => _view =
                  _view == _ZoneView.list ? _ZoneView.map : _ZoneView.list),
              icon: Icon(
                _view == _ZoneView.list
                    ? Icons.map_outlined
                    : Icons.view_list_rounded,
              ),
            ),
          IconButton(
            tooltip: 'Refresh',
            onPressed: _load,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      floatingActionButton: _disabledMessage == null && _canManage
          ? FloatingActionButton.extended(
              onPressed: () => _openEditor(),
              icon: const Icon(Icons.add_location_alt_outlined),
              label: const Text('New zone'),
            )
          : null,
      body: _disabledMessage != null
          ? SgEmptyState(
              icon: Icons.lock_outline_rounded,
              title: 'Geofencing add-on not enabled',
              subtitle: _disabledMessage,
              actionLabel: 'Check again',
              onAction: _load,
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _filters(vehicles),
                if (selected != null && selected.hasMapCoords)
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                    child: SgCard(
                      child: Text(
                        _vehicleSummary(selected),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 13,
                          color: context.sg.textSecondary,
                        ),
                      ),
                    ),
                  ),
                Expanded(child: _body(selected)),
              ],
            ),
    );
  }

  Widget _filters(List<FleetVehicleLocation> vehicles) {
    Widget chip<T>(String label, T value, T current, ValueChanged<T> set) {
      return Padding(
        padding: const EdgeInsets.only(right: 8),
        child: ChoiceChip(
          label: Text(label),
          selected: value == current,
          onSelected: (_) {
            set(value);
            _load();
          },
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _search,
                  textInputAction: TextInputAction.search,
                  onSubmitted: (_) => _load(),
                  decoration: InputDecoration(
                    isDense: true,
                    hintText: 'Search zones',
                    prefixIcon: const Icon(Icons.search_rounded),
                    border: const OutlineInputBorder(),
                    suffixIcon: _search.text.isEmpty
                        ? null
                        : IconButton(
                            icon: const Icon(Icons.close_rounded),
                            onPressed: () {
                              _search.clear();
                              _load();
                            },
                          ),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: DropdownButtonFormField<int?>(
                  initialValue: _vehicleId,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    isDense: true,
                    labelText: 'Vehicle',
                    border: OutlineInputBorder(),
                  ),
                  items: [
                    const DropdownMenuItem(value: null, child: Text('All')),
                    ...vehicles.map(
                      (v) => DropdownMenuItem(
                        value: v.vehicleId,
                        child: Text(
                          '${v.vehicleName} · ${v.registrationNumber}',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ),
                  ],
                  onChanged: (v) {
                    setState(() => _vehicleId = v);
                    _load();
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                chip<String?>('All shapes', null, _areaType, (v) => _areaType = v),
                chip<String?>('Circle', 'circle', _areaType, (v) => _areaType = v),
                chip<String?>('Polygon', 'polygon', _areaType, (v) => _areaType = v),
                chip<String?>('Rectangle', 'rectangle', _areaType, (v) => _areaType = v),
                const SizedBox(width: 8),
                chip<bool?>('Any status', null, _isActive, (v) => _isActive = v),
                chip<bool?>('Active', true, _isActive, (v) => _isActive = v),
                chip<bool?>('Inactive', false, _isActive, (v) => _isActive = v),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _body(FleetVehicleLocation? selected) {
    if (_loading && _rows.isEmpty) return const Center(child: SgBrandLoader());
    if (_error != null) return SgErrorView(message: _error!, onRetry: _load);
    if (_view == _ZoneView.map) return _mapView(selected);
    if (_rows.isEmpty) {
      return SgEmptyState(
        icon: Icons.fence_outlined,
        title: 'No geofences match',
        subtitle: _canManage
            ? 'Adjust the filters or tap "New zone" to draw one.'
            : 'Adjust the filters, or ask an admin to create zones.',
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.fromLTRB(
          16,
          12,
          16,
          88 + MediaQuery.paddingOf(context).bottom,
        ),
        itemCount: _rows.length,
        itemBuilder: (_, i) => _zoneTile(_rows[i], selected),
      ),
    );
  }

  Widget _zoneTile(GpsGeofenceItem g, FleetVehicleLocation? selected) {
    final p = context.sg;
    final status = _statusFor(g, selected);
    final insideCount =
        _states.where((s) => s.geofenceId == g.id && s.isInside).length;
    final shape = g.isCircle
        ? 'Circle · ${g.radiusMeters.toStringAsFixed(0)} m'
        : (g.areaType.toLowerCase().contains('rect') ? 'Rectangle' : 'Polygon');
    final subtitle = [
      shape,
      if ((g.category ?? '').isNotEmpty) g.category!,
      if (_vehicleId == null) '$insideCount inside',
      if (status != null) 'pos ${formatFreshness(status.positionUtc)}',
    ].join(' · ');

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        onTap: () => _openDetails(g),
        leading: Icon(
          g.isCircle ? Icons.radio_button_unchecked : Icons.pentagon_outlined,
          color: g.isActive ? AppColors.primary : p.textMuted,
        ),
        title: Text(
          g.name,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(color: g.isActive ? p.textPrimary : p.textMuted),
        ),
        subtitle: Text(
          subtitle,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(color: p.textSecondary),
        ),
        trailing: !g.isActive
            ? StatusBadge('Inactive', color: p.textMuted)
            : status == null
                ? Icon(Icons.chevron_right_rounded, color: p.textMuted)
                : StatusBadge(
                    status.label,
                    color: status.stale
                        ? AppColors.warning
                        : (status.inside ? AppColors.success : p.textMuted),
                  ),
      ),
    );
  }

  Widget _mapView(FleetVehicleLocation? selected) {
    final circles = <Circle>{};
    final polygons = <Polygon>{};
    for (final g in _rows) {
      final color = g.isActive ? AppColors.primary : context.sg.textMuted;
      if (g.isCircle) {
        if (g.radiusMeters <= 0) continue;
        circles.add(
          Circle(
            circleId: CircleId('gf${g.id}'),
            center: LatLng(g.centerLat, g.centerLng),
            radius: g.radiusMeters,
            fillColor: color.withValues(alpha: 0.14),
            strokeColor: color.withValues(alpha: 0.7),
            strokeWidth: 2,
            consumeTapEvents: true,
            onTap: () => _openDetails(g),
          ),
        );
      } else {
        final ring = parseGeofenceRing(g.geoJson);
        if (ring.length < 3) continue;
        polygons.add(
          Polygon(
            polygonId: PolygonId('gfp${g.id}'),
            points: [for (final p in ring) LatLng(p.lat, p.lng)],
            fillColor: color.withValues(alpha: 0.14),
            strokeColor: color.withValues(alpha: 0.7),
            strokeWidth: 2,
            consumeTapEvents: true,
            onTap: () => _openDetails(g),
          ),
        );
      }
    }

    final vehicles =
        ref.read(fleetHubProvider).valueOrNull?.locations ?? const [];
    final markers = <Marker>{
      for (final v in vehicles.where((v) => v.hasMapCoords))
        Marker(
          markerId: MarkerId('v${v.vehicleId}'),
          position: LatLng(v.latitude!, v.longitude!),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            v.vehicleId == selected?.vehicleId
                ? BitmapDescriptor.hueAzure
                : BitmapDescriptor.hueRed,
          ),
          zIndexInt: v.vehicleId == selected?.vehicleId ? 2 : 1,
          infoWindow: InfoWindow(
            title: v.registrationNumber,
            snippet: 'Updated ${formatFreshness(v.lastUpdated)}',
          ),
        ),
    };

    final first = _rows.firstOrNull;
    return Stack(
      children: [
        GoogleMap(
          initialCameraPosition: CameraPosition(
            target: first != null
                ? LatLng(first.centerLat, first.centerLng)
                : const LatLng(31.5204, 74.3587),
            zoom: 11,
          ),
          circles: circles,
          polygons: polygons,
          markers: markers,
          myLocationButtonEnabled: false,
          mapToolbarEnabled: false,
          onMapCreated: (c) {
            _map = c;
            _fitMap();
          },
        ),
        if (_loading)
          const Positioned(
            top: 12,
            left: 0,
            right: 0,
            child: Center(child: SgBrandLoader()),
          ),
        if (_rows.isEmpty && !_loading)
          Positioned(
            left: 16,
            right: 16,
            top: 12,
            child: SgCard(
              child: Text(
                'No geofences match the current filters.',
                style: TextStyle(color: context.sg.textSecondary),
              ),
            ),
          ),
      ],
    );
  }

  String _vehicleSummary(FleetVehicleLocation v) {
    final inside = _rows
        .where((g) => g.isActive && _statusFor(g, v)?.inside == true)
        .map((g) => g.name)
        .toList();
    final fresh = 'Position ${formatFreshness(v.lastUpdated)}';
    if (inside.isNotEmpty) return 'Inside ${inside.join(', ')} · $fresh';
    final nearest = nearestCircleGeofence(
      lat: v.latitude!,
      lng: v.longitude!,
      fences: _rows.where((g) => g.isActive).toList(),
    );
    if (nearest == null) return 'Not inside any zone · $fresh';
    return 'Not inside any zone · nearest circle ${nearest.name} '
        '(${nearest.distanceMeters.toStringAsFixed(0)} m) · $fresh';
  }
}

class _ZoneStatus {
  const _ZoneStatus(this.inside, this.positionUtc);
  final bool inside;
  final DateTime? positionUtc;

  bool get stale =>
      positionUtc == null ||
      DateTime.now().toUtc().difference(positionUtc!.toUtc()) >
          geofenceStaleAfter;

  String get label => stale
      ? (inside ? 'Inside · stale' : 'Outside · stale')
      : (inside ? 'Inside' : 'Outside');
}
