import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../../auth/data/auth_repository.dart';
import '../../domain/fleet_models.dart';
import '../../domain/fleet_status.dart';
import '../../domain/gps_freshness.dart';
import '../fleet_hub_notifier.dart';
import '../vehicle_commands_sheet.dart';
import 'fleet_kpi_strip.dart';
import 'live_map_location.dart';
import 'vehicle_live_map_sheet.dart';
import 'vehicle_pulse_icons.dart';

String _formatUpdateAge(DateTime? at) {
  if (at == null) return 'No recent update';
  var sec = DateTime.now().difference(at).inSeconds;
  if (sec < 0) sec = 0;
  if (sec < 60) return 'Updated ${sec}s ago';
  if (sec < 3600) return 'Updated ${sec ~/ 60}m ago';
  if (sec < 86400) return 'Updated ${sec ~/ 3600}h ago';
  return 'Updated ${sec ~/ 86400}d ago';
}

String _formatLiveFreshness(DateTime? at) {
  if (at == null) return 'Awaiting GPS';
  var sec = DateTime.now().difference(at).inSeconds;
  if (sec < 0) sec = 0;
  if (sec <= 120) return 'Live · ${_formatUpdateAge(at)}';
  if (sec <= 600) return _formatUpdateAge(at);
  return 'GPS may be stale · ${_formatUpdateAge(at)}';
}

/// Live tracking tab: map-first with follow/center/fullscreen and compact telemetry.
class VehicleLiveMapTab extends ConsumerStatefulWidget {
  const VehicleLiveMapTab({
    super.key,
    required this.vehicleId,
    required this.vehicle,
    this.live,
    required this.gpsAsync,
    required this.onRetryGps,
  });

  final int vehicleId;
  final VehicleDetail vehicle;
  final FleetVehicleLocation? live;
  final AsyncValue<VehicleGpsInfo> gpsAsync;
  final VoidCallback onRetryGps;

  @override
  ConsumerState<VehicleLiveMapTab> createState() => _VehicleLiveMapTabState();
}

class _VehicleLiveMapTabState extends ConsumerState<VehicleLiveMapTab> {
  GoogleMapController? _map;
  BitmapDescriptor? _icon;
  FleetTrackStatus? _iconStatus;
  bool _follow = true;
  bool _mapReady = false;
  bool _ignoreCameraMove = false;
  MapType _mapType = MapType.normal;
  LatLng? _lastCameraTarget;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadIcon());
  }

  @override
  void didUpdateWidget(covariant VehicleLiveMapTab oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (_status != _iconStatus) {
      _loadIcon();
    }
    final next = _position;
    if (next == null || !_follow || _map == null || !_mapReady) return;
    if (_lastCameraTarget != null &&
        (_lastCameraTarget!.latitude - next.latitude).abs() < 0.00001 &&
        (_lastCameraTarget!.longitude - next.longitude).abs() < 0.00001) {
      return;
    }
    _lastCameraTarget = next;
    _animateTo(next);
  }

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
  }

  Future<void> _loadIcon() async {
    final status = _status;
    final icon = await VehiclePulseIcons.iconFor(
      status,
      devicePixelRatio: MediaQuery.devicePixelRatioOf(context),
    );
    if (!mounted) return;
    setState(() {
      _icon = icon;
      _iconStatus = status;
    });
  }

  Future<void> _animateTo(LatLng target,
      {double? zoom, double? bearing}) async {
    final map = _map;
    if (map == null) return;
    _ignoreCameraMove = true;
    try {
      if (zoom != null || bearing != null) {
        await map.animateCamera(
          CameraUpdate.newCameraPosition(
            CameraPosition(
              target: target,
              zoom: zoom ?? 16,
              bearing: bearing ?? _heading,
            ),
          ),
        );
      } else {
        await map.animateCamera(CameraUpdate.newLatLng(target));
      }
    } finally {
      // Allow Google Maps to finish gesture callbacks before re-enabling follow.
      await Future<void>.delayed(const Duration(milliseconds: 350));
      _ignoreCameraMove = false;
    }
  }

  FleetTrackStatus get _status {
    final live = widget.live;
    final gps = widget.gpsAsync.valueOrNull;
    return live?.status ??
        resolveFleetStatus(
          speed: live?.speed ?? gps?.speed,
          ignition: live?.ignition ?? gps?.lastIgnition,
          lastUpdated: live?.lastUpdated ?? gps?.lastUpdate,
          hasGps: hasValidFleetCoords(
            live?.latitude ?? gps?.latitude,
            live?.longitude ?? gps?.longitude,
          ),
          isGpsValid: live?.isGpsValid,
        );
  }

  String get _speedLabel => formatDisplaySpeedLabel(
        speed: _speed,
        ignition: widget.live?.ignition ??
            widget.gpsAsync.valueOrNull?.lastIgnition,
        status: _status,
        latitude: widget.live?.latitude ??
            widget.gpsAsync.valueOrNull?.latitude,
        longitude: widget.live?.longitude ??
            widget.gpsAsync.valueOrNull?.longitude,
        isGpsValid: widget.live?.isGpsValid ?? true,
      );

  LatLng? get _position {
    final live = widget.live;
    final gps = widget.gpsAsync.valueOrNull;
    final lat = live?.latitude ?? gps?.latitude;
    final lng = live?.longitude ?? gps?.longitude;
    if (!hasValidFleetCoords(lat, lng)) return null;
    return LatLng(lat!, lng!);
  }

  double get _heading =>
      widget.live?.heading ?? widget.gpsAsync.valueOrNull?.heading ?? 0;

  double get _speed =>
      widget.live?.speed ?? widget.gpsAsync.valueOrNull?.speed ?? 0;

  Future<void> _centerOnVehicle({bool animate = true}) async {
    final pos = _position;
    final map = _map;
    if (pos == null || map == null) return;
    _lastCameraTarget = pos;
    if (!animate) {
      _ignoreCameraMove = true;
      try {
        await map.moveCamera(
          CameraUpdate.newCameraPosition(
            CameraPosition(target: pos, zoom: 16, bearing: _heading),
          ),
        );
      } finally {
        await Future<void>.delayed(const Duration(milliseconds: 200));
        _ignoreCameraMove = false;
      }
      return;
    }
    await _animateTo(pos, zoom: 16, bearing: _heading);
  }

  Future<void> _zoomBy(double delta) async {
    final map = _map;
    if (map == null) return;
    _ignoreCameraMove = true;
    try {
      await map.animateCamera(CameraUpdate.zoomBy(delta));
    } finally {
      await Future<void>.delayed(const Duration(milliseconds: 350));
      _ignoreCameraMove = false;
    }
  }

  void _openFullscreen() {
    if (_position == null) return;
    Navigator.of(context, rootNavigator: true).push(
      MaterialPageRoute<void>(
        fullscreenDialog: true,
        builder: (_) => _LiveMapFullscreenPage(
          vehicleId: widget.vehicleId,
          vehicleName: widget.vehicle.name,
          plate: widget.vehicle.registrationNumber,
          gpsFallback: widget.gpsAsync.valueOrNull,
          follow: _follow,
          mapType: _mapType,
          onFollowChanged: (v) {
            if (mounted) setState(() => _follow = v);
          },
        ),
      ),
    );
  }

  FleetVehicleLocation _sheetVehicle(VehicleGpsInfo? gps) {
    final live = widget.live;
    final v = widget.vehicle;
    return FleetVehicleLocation(
      vehicleId: widget.vehicleId,
      vehicleName: v.name,
      registrationNumber: v.registrationNumber,
      status: _status,
      latitude: live?.latitude ?? gps?.latitude,
      longitude: live?.longitude ?? gps?.longitude,
      lastUpdated: live?.lastUpdated ?? gps?.lastUpdate,
      speed: live?.speed ?? gps?.speed ?? 0,
      driverName: live?.driverName ?? v.driverName,
      driverPhone: live?.driverPhone ?? v.driverPhone,
      hasGps: live?.hasGps ?? v.hasGpsDevice,
      ignition: live?.ignition ?? gps?.lastIgnition,
      heading: live?.heading ?? gps?.heading,
      batteryLevel: live?.batteryLevel ?? gps?.batteryLevel,
      gsmSignal: live?.gsmSignal ?? gps?.gsmSignal,
      address: live?.address ?? gps?.address,
      alarmType: live?.alarmType,
      vehicleType: live?.vehicleType ?? v.vehicleType,
      serviceAlert: live?.serviceAlert ?? v.serviceAlert,
    );
  }

  Future<void> _openExternalNav() async {
    final pos = _position;
    if (pos == null) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${pos.latitude},${pos.longitude}',
    );
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
  }

  @override
  Widget build(BuildContext context) {
    // Prefer GPS snapshot, but do not block Live if hub already has coords
    // (e.g. transient /Vehicles/{id}/gps failure).
    final gps = widget.gpsAsync.valueOrNull;
    final gpsError = widget.gpsAsync.hasError ? widget.gpsAsync.error : null;

    if (widget.gpsAsync.isLoading && gps == null && widget.live == null) {
      return const Center(child: SgBrandLoader());
    }

    if (gpsError != null && gps == null && _position == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$gpsError', textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: widget.onRetryGps,
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    return Builder(
      builder: (context) {
        final pos = _position;
        if (pos == null) {
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            children: [
              SgCard(
                child: Column(
                  children: [
                    Icon(Icons.gps_off, size: 40, color: context.sg.textMuted),
                    const SizedBox(height: 10),
                    const Text(
                      'No live GPS position yet',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      gps?.deviceName ??
                          (gpsError != null
                              ? 'Tracker details unavailable. Pull to retry.'
                              : 'Tracker may be offline or awaiting first fix.'),
                      textAlign: TextAlign.center,
                      style: TextStyle(color: context.sg.textSecondary),
                    ),
                    if (gpsError != null) ...[
                      const SizedBox(height: 12),
                      TextButton(
                        onPressed: widget.onRetryGps,
                        child: const Text('Retry tracker details'),
                      ),
                    ],
                  ],
                ),
              ),
              if (gps != null) ...[
                const SizedBox(height: 12),
                _TelemetryCard(
                  vehicle: widget.vehicle,
                  gps: gps,
                  live: widget.live,
                ),
              ],
              const SizedBox(height: 12),
              _LiveActions(
                vehicleId: widget.vehicleId,
                hasDevice: gps?.gpsDeviceId != null,
                canCommands:
                    ref.watch(fleetSessionProvider)?.canUseGpsCommands ?? false,
              ),
            ],
          );
        }

        final rotates = VehiclePulseIcons.rotatesWithHeading(_status);
        final canCommands =
            ref.watch(fleetSessionProvider)?.canUseGpsCommands ?? false;
        final sheetVehicle = _sheetVehicle(gps);
        return Stack(
          children: [
            Positioned.fill(
              child: GoogleMap(
                initialCameraPosition: CameraPosition(
                  target: pos,
                  zoom: 15.5,
                  bearing: rotates ? _heading : 0,
                ),
                markers: {
                  Marker(
                    markerId: MarkerId('v${widget.vehicleId}'),
                    position: pos,
                    rotation: rotates ? _heading : 0,
                    flat: true,
                    anchor: const Offset(0.5, 0.5),
                    icon: _icon ??
                        BitmapDescriptor.defaultMarkerWithHue(
                          BitmapDescriptor.hueAzure,
                        ),
                    infoWindow: InfoWindow(
                      title: widget.vehicle.name,
                      snippet: '$_speedLabel · ${_status.label}',
                    ),
                  ),
                },
                mapType: _mapType,
                liteModeEnabled: false,
                zoomControlsEnabled: false,
                myLocationButtonEnabled: false,
                mapToolbarEnabled: false,
                compassEnabled: true,
                onMapCreated: (c) async {
                  _map = c;
                  _mapReady = true;
                  await _centerOnVehicle(animate: false);
                },
                onCameraMoveStarted: () {
                  if (_ignoreCameraMove) return;
                  // User gesture → pause follow until they re-enable.
                  if (_follow) setState(() => _follow = false);
                },
              ),
            ),
            Positioned(
              right: 10,
              top: 10,
              child: _MapFabColumn(
                follow: _follow,
                onZoomIn: () => _zoomBy(1),
                onZoomOut: () => _zoomBy(-1),
                onCenter: () {
                  setState(() => _follow = true);
                  _centerOnVehicle();
                },
                onToggleFollow: () {
                  setState(() => _follow = !_follow);
                  if (_follow) _centerOnVehicle();
                },
                onToggleMapType: () {
                  setState(() {
                    _mapType = _mapType == MapType.normal
                        ? MapType.hybrid
                        : MapType.normal;
                  });
                },
                onFullscreen: _openFullscreen,
              ),
            ),
            if (gpsError != null && gps == null)
              Positioned(
                left: 12,
                right: 72,
                top: 10,
                child: Material(
                  color: context.sg.card.withValues(alpha: 0.96),
                  borderRadius: BorderRadius.circular(AppRadii.md),
                  elevation: 2,
                  child: Padding(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            'Tracker details failed to load.',
                            style: TextStyle(
                              fontSize: 12,
                              color: context.sg.textSecondary,
                            ),
                          ),
                        ),
                        TextButton(
                          onPressed: widget.onRetryGps,
                          child: const Text('Retry'),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            Positioned(
              left: 12,
              right: 12,
              bottom: 12,
              child: TweenAnimationBuilder<double>(
                tween: Tween(begin: 0, end: 1),
                duration: const Duration(milliseconds: 220),
                curve: Curves.easeOutCubic,
                builder: (context, t, child) {
                  return Opacity(
                    opacity: t.clamp(0.0, 1.0),
                    child: Transform.translate(
                      offset: Offset(0, (1 - t) * 18),
                      child: child,
                    ),
                  );
                },
                child: VehicleLiveMapSheet(
                  vehicle: sheetVehicle,
                  follow: _follow,
                  showClose: false,
                  showDetailsAction: false,
                  canCommands: canCommands && gps?.gpsDeviceId != null,
                  onFollowToggle: () {
                    setState(() => _follow = !_follow);
                    if (_follow) _centerOnVehicle();
                  },
                  onCenter: () {
                    setState(() => _follow = true);
                    _centerOnVehicle();
                  },
                  onNavigate: () => _openExternalNav(),
                  onCommands: canCommands && gps?.gpsDeviceId != null
                      ? () =>
                          showVehicleCommandsSheet(context, widget.vehicleId)
                      : null,
                  onClose: () {},
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _MapFabColumn extends StatelessWidget {
  const _MapFabColumn({
    required this.follow,
    required this.onZoomIn,
    required this.onZoomOut,
    required this.onCenter,
    required this.onToggleFollow,
    required this.onToggleMapType,
    required this.onFullscreen,
  });

  final bool follow;
  final VoidCallback onZoomIn;
  final VoidCallback onZoomOut;
  final VoidCallback onCenter;
  final VoidCallback onToggleFollow;
  final VoidCallback onToggleMapType;
  final VoidCallback onFullscreen;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        _MapFab(icon: Icons.add, tooltip: 'Zoom in', onTap: onZoomIn),
        const SizedBox(height: 6),
        _MapFab(icon: Icons.remove, tooltip: 'Zoom out', onTap: onZoomOut),
        const SizedBox(height: 6),
        _MapFab(
          icon: Icons.my_location,
          tooltip: 'Center vehicle',
          onTap: onCenter,
        ),
        const SizedBox(height: 6),
        _MapFab(
          icon: follow ? Icons.location_disabled : Icons.directions_run,
          tooltip: follow ? 'Follow on' : 'Follow off',
          active: follow,
          onTap: onToggleFollow,
        ),
        const SizedBox(height: 6),
        _MapFab(
          icon: Icons.layers_outlined,
          tooltip: 'Map type',
          onTap: onToggleMapType,
        ),
        const SizedBox(height: 6),
        _MapFab(
          icon: Icons.fullscreen,
          tooltip: 'Full screen',
          onTap: onFullscreen,
        ),
      ],
    );
  }
}

class _MapFab extends StatelessWidget {
  const _MapFab({
    required this.icon,
    required this.onTap,
    required this.tooltip,
    this.active = false,
  });

  final IconData icon;
  final VoidCallback onTap;
  final String tooltip;
  final bool active;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? AppColors.primary : context.sg.card,
      elevation: 2,
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: Tooltip(
          message: tooltip,
          child: SizedBox(
            width: 40,
            height: 40,
            child: Icon(
              icon,
              size: 20,
              color: active ? Colors.white : context.sg.textPrimary,
            ),
          ),
        ),
      ),
    );
  }
}

class _TelemetryCard extends ConsumerWidget {
  const _TelemetryCard({
    required this.vehicle,
    required this.gps,
    this.live,
  });

  final VehicleDetail vehicle;
  final VehicleGpsInfo gps;
  final FleetVehicleLocation? live;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final speed = live?.speed ?? gps.speed ?? 0;
    final ignition = live?.ignition ?? gps.lastIgnition;
    final status = live?.status ??
        resolveFleetStatus(
          speed: speed,
          ignition: ignition,
          lastUpdated: live?.lastUpdated ?? gps.lastUpdate,
          hasGps: hasValidFleetCoords(
            live?.latitude ?? gps.latitude,
            live?.longitude ?? gps.longitude,
          ),
          isGpsValid: live?.isGpsValid,
        );
    final battery = live?.batteryLevel ?? gps.batteryLevel;
    final last = live?.lastUpdated ?? gps.lastUpdate;
    final online = gps.gpsOnline ||
        vehicle.gpsOnline ||
        (last != null && DateTime.now().difference(last).inMinutes <= 10);
    final df = DateFormat('dd MMM yyyy, HH:mm');
    final speedLabel = formatDisplaySpeedLabel(
      speed: speed,
      ignition: ignition,
      status: status,
      latitude: live?.latitude ?? gps.latitude,
      longitude: live?.longitude ?? gps.longitude,
      isGpsValid: live?.isGpsValid ?? true,
    );

    return SgCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  gps.deviceName ?? 'Tracker',
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 15,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: (online ? AppColors.success : context.sg.textMuted)
                      .withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.pill),
                ),
                child: Text(
                  online ? 'ONLINE' : 'OFFLINE',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w800,
                    color: online ? AppColors.success : context.sg.textMuted,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          const Divider(height: 1),
          const SizedBox(height: 8),
          _metric(context, 'Speed', speedLabel),
          _metric(
            context,
            'Ignition',
            ignition == null ? 'Not available' : (ignition ? 'ON' : 'OFF'),
          ),
          _metric(
            context,
            'Battery',
            battery != null
                ? '${battery.toStringAsFixed(0)}%'
                : 'Not available',
          ),
          _metric(
            context,
            'GPS',
            formatGpsFreshness(
              latitude: live?.latitude ?? gps.latitude,
              longitude: live?.longitude ?? gps.longitude,
              lastUpdated: last,
            ),
          ),
          _metric(
            context,
            'Last update',
            last != null ? df.format(last.toLocal()) : '—',
          ),
          _metric(
            context,
            'Odometer',
            gps.totalDistanceKm != null
                ? '${gps.totalDistanceKm!.toStringAsFixed(0)} km'
                : '—',
          ),
          _metric(context, 'IMEI', gps.uniqueId ?? vehicle.gpsImei ?? '—'),
          _metric(
            context,
            'Address',
            _telemetryAddress(ref) ?? 'Resolving…',
          ),
        ],
      ),
    );
  }

  String? _telemetryAddress(WidgetRef ref) {
    final inline = (live?.address ?? gps.address)?.trim();
    if (inline != null &&
        inline.isNotEmpty &&
        !liveMapAddressNeedsReverseGeocode(inline)) {
      return inline;
    }
    final lat = live?.latitude ?? gps.latitude;
    final lng = live?.longitude ?? gps.longitude;
    if (!hasValidFleetCoords(lat, lng)) {
      return (inline != null &&
              inline.isNotEmpty &&
              !isLiveMapCoordsOnlyAddress(inline))
          ? inline
          : null;
    }
    final key = liveMapCoordKey(lat, lng);
    final async = ref.watch(
      vehicleLocationProvider((
        vehicleId: vehicle.id,
        coordKey: key,
      )),
    );
    final line = async.valueOrNull?.displayLine.trim();
    if (line != null && line.isNotEmpty && !isLiveMapCoordsOnlyAddress(line)) {
      return line;
    }
    if (inline != null &&
        inline.isNotEmpty &&
        !isLiveMapCoordsOnlyAddress(inline)) {
      return inline;
    }
    // Still loading → null so the metric shows "Resolving…"
    if (async.isLoading && !async.hasValue) return null;
    if (lat != null && lng != null) {
      return formatLiveMapCoords(lat, lng);
    }
    return null;
  }

  Widget _metric(BuildContext context, String label, String value) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 96,
            child: Text(
              label,
              style: TextStyle(
                fontSize: 12,
                color: p.textMuted,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: p.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LiveActions extends StatelessWidget {
  const _LiveActions({
    required this.vehicleId,
    required this.hasDevice,
    required this.canCommands,
  });

  final int vehicleId;
  final bool hasDevice;
  final bool canCommands;

  @override
  Widget build(BuildContext context) {
    if (!hasDevice || !canCommands) {
      return const SizedBox.shrink();
    }
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton.icon(
        onPressed: () => showVehicleCommandsSheet(context, vehicleId),
        icon: const Icon(Icons.power_settings_new),
        label: const Text('Engine / GPS commands'),
      ),
    );
  }
}

/// Immersive full-screen live map (hides shell bottom nav via root navigator).
class _LiveMapFullscreenPage extends ConsumerStatefulWidget {
  const _LiveMapFullscreenPage({
    required this.vehicleId,
    required this.vehicleName,
    required this.plate,
    this.gpsFallback,
    required this.follow,
    required this.mapType,
    required this.onFollowChanged,
  });

  final int vehicleId;
  final String vehicleName;
  final String plate;
  final VehicleGpsInfo? gpsFallback;
  final bool follow;
  final MapType mapType;
  final ValueChanged<bool> onFollowChanged;

  @override
  ConsumerState<_LiveMapFullscreenPage> createState() =>
      _LiveMapFullscreenPageState();
}

class _LiveMapFullscreenPageState
    extends ConsumerState<_LiveMapFullscreenPage> {
  GoogleMapController? _map;
  BitmapDescriptor? _icon;
  FleetTrackStatus? _iconStatus;
  late bool _follow;
  late MapType _mapType;
  bool _ignoreCameraMove = false;
  LatLng? _lastCameraTarget;
  bool _mapReady = false;

  @override
  void initState() {
    super.initState();
    _follow = widget.follow;
    _mapType = widget.mapType;
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadIcon());
  }

  @override
  void dispose() {
    SystemChrome.setEnabledSystemUIMode(
      SystemUiMode.manual,
      overlays: SystemUiOverlay.values,
    );
    _map?.dispose();
    super.dispose();
  }

  FleetVehicleLocation? _liveFrom(WidgetRef ref) {
    final locs = ref.watch(fleetHubProvider).valueOrNull?.locations ?? const [];
    return locs.where((v) => v.vehicleId == widget.vehicleId).firstOrNull;
  }

  LatLng? _positionOf(FleetVehicleLocation? live) {
    final gps = widget.gpsFallback;
    final lat = live?.latitude ?? gps?.latitude;
    final lng = live?.longitude ?? gps?.longitude;
    if (!hasValidFleetCoords(lat, lng)) return null;
    return LatLng(lat!, lng!);
  }

  String? _fullscreenAddress(
    WidgetRef ref,
    FleetVehicleLocation? live,
    VehicleGpsInfo? gps,
    LatLng? pos,
  ) {
    final inline = (live?.address ?? gps?.address)?.trim();
    if (inline != null &&
        inline.isNotEmpty &&
        !liveMapAddressNeedsReverseGeocode(inline)) {
      return inline;
    }
    if (pos == null) {
      return (inline != null &&
              inline.isNotEmpty &&
              !isLiveMapCoordsOnlyAddress(inline))
          ? inline
          : null;
    }
    final key = liveMapCoordKey(pos.latitude, pos.longitude);
    final info = ref
        .watch(
          vehicleLocationProvider((
            vehicleId: widget.vehicleId,
            coordKey: key,
          )),
        )
        .valueOrNull;
    final line = info?.displayLine.trim();
    if (line != null && line.isNotEmpty && !isLiveMapCoordsOnlyAddress(line)) {
      return line;
    }
    if (inline != null &&
        inline.isNotEmpty &&
        !isLiveMapCoordsOnlyAddress(inline)) {
      return inline;
    }
    return null;
  }

  Future<void> _loadIcon([FleetTrackStatus? status]) async {
    final s = status ?? FleetTrackStatus.offline;
    final icon = await VehiclePulseIcons.iconFor(
      s,
      devicePixelRatio: MediaQuery.devicePixelRatioOf(context),
    );
    if (!mounted) return;
    setState(() {
      _icon = icon;
      _iconStatus = s;
    });
  }

  Future<void> _animateTo(LatLng target) async {
    final map = _map;
    if (map == null) return;
    _ignoreCameraMove = true;
    try {
      await map.animateCamera(CameraUpdate.newLatLng(target));
    } finally {
      await Future<void>.delayed(const Duration(milliseconds: 350));
      _ignoreCameraMove = false;
    }
  }

  Future<void> _centerOnVehicle(LatLng pos, double heading) async {
    if (_map == null) return;
    _lastCameraTarget = pos;
    _ignoreCameraMove = true;
    try {
      await _map!.animateCamera(
        CameraUpdate.newCameraPosition(
          CameraPosition(target: pos, zoom: 16, bearing: heading),
        ),
      );
    } finally {
      await Future<void>.delayed(const Duration(milliseconds: 350));
      _ignoreCameraMove = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final live = _liveFrom(ref);
    final gps = widget.gpsFallback;
    final pos = _positionOf(live);
    final heading = live?.heading ?? gps?.heading ?? 0;
    final speed = live?.speed ?? gps?.speed ?? 0;
    final lastUpdate = live?.lastUpdated ?? gps?.lastUpdate;
    final address = _fullscreenAddress(ref, live, gps, pos);
    final status = live?.status ??
        resolveFleetStatus(
          speed: live?.speed ?? gps?.speed,
          ignition: live?.ignition ?? gps?.lastIgnition,
          lastUpdated: lastUpdate,
          hasGps: pos != null,
        );

    if (_iconStatus != status) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _loadIcon(status);
      });
    }

    ref.listen(fleetHubProvider, (prev, next) {
      if (!_follow || !_mapReady) return;
      final locs = next.valueOrNull?.locations ?? const [];
      final v = locs.where((x) => x.vehicleId == widget.vehicleId).firstOrNull;
      final lat = v?.latitude ?? gps?.latitude;
      final lng = v?.longitude ?? gps?.longitude;
      if (!hasValidFleetCoords(lat, lng)) return;
      final nextPos = LatLng(lat!, lng!);
      if (_lastCameraTarget != null &&
          (_lastCameraTarget!.latitude - nextPos.latitude).abs() < 0.00001 &&
          (_lastCameraTarget!.longitude - nextPos.longitude).abs() < 0.00001) {
        return;
      }
      _lastCameraTarget = nextPos;
      _animateTo(nextPos);
    });

    final color = fleetStatusColor(status);
    const fallback = LatLng(31.5204, 74.3587);

    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          GoogleMap(
            initialCameraPosition: CameraPosition(
              target: pos ?? fallback,
              zoom: 16,
              bearing: heading,
            ),
            markers: pos == null
                ? {}
                : {
                    Marker(
                      markerId: MarkerId('v${widget.vehicleId}'),
                      position: pos,
                      rotation: VehiclePulseIcons.rotatesWithHeading(status)
                          ? heading
                          : 0,
                      flat: true,
                      anchor: const Offset(0.5, 0.5),
                      icon: _icon ??
                          BitmapDescriptor.defaultMarkerWithHue(
                            BitmapDescriptor.hueAzure,
                          ),
                    ),
                  },
            mapType: _mapType,
            zoomControlsEnabled: false,
            myLocationButtonEnabled: false,
            mapToolbarEnabled: false,
            compassEnabled: true,
            onMapCreated: (c) async {
              _map = c;
              _mapReady = true;
              if (pos != null) await _centerOnVehicle(pos, heading);
            },
            onCameraMoveStarted: () {
              if (_ignoreCameraMove) return;
              if (_follow) {
                setState(() => _follow = false);
                widget.onFollowChanged(false);
              }
            },
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: [
                  Material(
                    color: context.sg.card,
                    shape: const CircleBorder(),
                    child: IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.of(context).pop(),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Material(
                      color: context.sg.card.withValues(alpha: 0.94),
                      borderRadius: BorderRadius.circular(AppRadii.md),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 8,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              widget.vehicleName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 14,
                                color: context.sg.textPrimary,
                              ),
                            ),
                            Text(
                              widget.plate,
                              style: TextStyle(
                                fontSize: 11,
                                color: context.sg.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Material(
                    color: context.sg.card,
                    shape: const CircleBorder(),
                    child: IconButton(
                      tooltip: 'Exit full screen',
                      icon: const Icon(Icons.fullscreen_exit),
                      onPressed: () => Navigator.of(context).pop(),
                    ),
                  ),
                ],
              ),
            ),
          ),
          Positioned(
            right: 12,
            top: MediaQuery.paddingOf(context).top + 72,
            child: Column(
              children: [
                _MapFab(
                  icon: Icons.add,
                  tooltip: 'Zoom in',
                  onTap: () async {
                    _ignoreCameraMove = true;
                    try {
                      await _map?.animateCamera(CameraUpdate.zoomBy(1));
                    } finally {
                      await Future<void>.delayed(
                        const Duration(milliseconds: 350),
                      );
                      _ignoreCameraMove = false;
                    }
                  },
                ),
                const SizedBox(height: 6),
                _MapFab(
                  icon: Icons.remove,
                  tooltip: 'Zoom out',
                  onTap: () async {
                    _ignoreCameraMove = true;
                    try {
                      await _map?.animateCamera(CameraUpdate.zoomBy(-1));
                    } finally {
                      await Future<void>.delayed(
                        const Duration(milliseconds: 350),
                      );
                      _ignoreCameraMove = false;
                    }
                  },
                ),
                const SizedBox(height: 6),
                _MapFab(
                  icon: Icons.my_location,
                  tooltip: 'Center vehicle',
                  onTap: () {
                    if (pos == null) return;
                    setState(() => _follow = true);
                    widget.onFollowChanged(true);
                    _centerOnVehicle(pos, heading);
                  },
                ),
              ],
            ),
          ),
          Positioned(
            left: 12,
            right: 12,
            bottom: 28,
            child: Material(
              color: context.sg.card.withValues(alpha: 0.96),
              borderRadius: BorderRadius.circular(AppRadii.md),
              elevation: 3,
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Row(
                  children: [
                    Container(
                      width: 10,
                      height: 10,
                      decoration:
                          BoxDecoration(color: color, shape: BoxShape.circle),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            '${status.label} · ${speed.toStringAsFixed(0)} km/h',
                            style: const TextStyle(fontWeight: FontWeight.w800),
                          ),
                          if ((address ?? '').trim().isNotEmpty)
                            Text(
                              address!,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 12,
                                color: context.sg.textSecondary,
                              ),
                            ),
                          Text(
                            _formatLiveFreshness(lastUpdate),
                            style: TextStyle(
                              fontSize: 11,
                              color: context.sg.textMuted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    IconButton(
                      tooltip: _follow ? 'Following' : 'Follow',
                      onPressed: () {
                        setState(() => _follow = !_follow);
                        widget.onFollowChanged(_follow);
                        if (_follow && pos != null) {
                          _centerOnVehicle(pos, heading);
                        }
                      },
                      icon: Icon(
                        _follow
                            ? Icons.location_disabled
                            : Icons.directions_run,
                        color: _follow ? AppColors.primary : null,
                      ),
                    ),
                    IconButton(
                      tooltip: 'Map type',
                      onPressed: () {
                        setState(() {
                          _mapType = _mapType == MapType.normal
                              ? MapType.hybrid
                              : MapType.normal;
                        });
                      },
                      icon: const Icon(Icons.layers_outlined),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
