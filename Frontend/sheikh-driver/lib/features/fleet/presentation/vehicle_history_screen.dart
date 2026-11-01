import 'dart:async';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/errors/app_exception.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import '../domain/trip_display.dart';
import 'fleet_hub_notifier.dart';
import 'playback/playback_controls.dart';
import 'playback/playback_controller.dart';
import 'playback/playback_event_filters.dart';
import 'playback/playback_event_list.dart';
import 'playback/playback_header.dart';
import 'playback/playback_helpers.dart';
import 'playback/playback_legend.dart';
import 'playback/playback_map_fabs.dart';
import 'playback/playback_map_builder.dart';
import 'playback/playback_scrub_footer.dart';
import 'playback/playback_summary_card.dart';
import 'playback/playback_timeline.dart';
import 'widgets/pulse_vehicle_overlay.dart';

enum _HistoryPreset {
  today,
  yesterday,
  hours24,
  days3,
  days7,
  hours6,
  custom,
}

class VehicleHistoryScreen extends ConsumerStatefulWidget {
  const VehicleHistoryScreen({
    super.key,
    required this.vehicleId,
    this.tripKey,
    this.initialPreset,
    this.initialFrom,
    this.initialTo,
    this.autoPlayFull = false,
  });
  final int vehicleId;

  /// When set, skip the trip list and open this trip's playback.
  final String? tripKey;

  /// Optional preset from Playback tab: hours6, today, yesterday, days3, days7, hours24.
  final String? initialPreset;

  /// Custom range start (UTC or local ISO). Used when [initialPreset] is custom.
  final DateTime? initialFrom;

  /// Custom range end (UTC or local ISO). Used when [initialPreset] is custom.
  final DateTime? initialTo;

  /// When true (and no [tripKey]), open full-range map playback for the range.
  final bool autoPlayFull;

  @override
  ConsumerState<VehicleHistoryScreen> createState() =>
      _VehicleHistoryScreenState();
}

class _VehicleHistoryScreenState extends ConsumerState<VehicleHistoryScreen>
    with TickerProviderStateMixin {
  GoogleMapController? _map;
  bool _loading = true;
  String? _error;
  HistoryReplayBundle? _bundle;
  int _index = 0;
  DateTime? _virtualTime;
  List<HistoryReplayPoint> _playbackPoints = const [];
  bool _playing = false;
  double _speed = 1;
  bool _follow = false;
  MapType _mapType = MapType.normal;
  TripEventFilter _eventFilter = TripEventFilter.all;
  double _sheetExtent = 0.28;
  final DraggableScrollableController _sheetController =
      DraggableScrollableController();
  BitmapDescriptor? _vehicleIcon;
  Offset? _pulseScreenPos;
  final GlobalKey _mapStackKey = GlobalKey();
  bool _pulseSyncScheduled = false;
  bool _didInitialCameraFocus = false;
  Set<Polyline> _polylines = {};
  Set<Marker> _staticMarkers = {};
  final Map<String, String> _addressCache = {};
  Set<Marker> _markers = {};
  double _distSoFar = 0;
  bool _chromeVisible = true;
  Timer? _chromeHideTimer;
  late final PlaybackController _playbackController;
  late final AnimationController _refreshSpinController;
  LatLng? _displayPosition;
  double? _displayHeading;
  double _devicePixelRatio = 2;
  int _lastPolylineRefreshMs = 0;
  int _lastDistanceIndex = -1;
  int _lastStopVisitSignature = -1;

  _HistoryPreset _preset = _HistoryPreset.hours6;
  late DateTime _from;
  late DateTime _to;

  List<GpsTrip> _trips = const [];
  GpsTrip? _selectedTrip;
  TripDetailBundle? _tripDetail;
  List<TripStop> _allStops = const [];
  TripListFilter _tripFilter = TripListFilter.moving;

  /// Match web history: stop/parking markers on by default.
  bool _showStopMarkers = true;
  bool _playingFullRange = false;
  bool _loadingTripDetail = false;

  final _tripTf = DateFormat('h:mm a');

  bool get _inTripList =>
      !_playingFullRange &&
      !_loadingTripDetail &&
      _selectedTrip == null &&
      _tripDetail == null;

  FleetSession? get _session => ref.read(fleetSessionProvider);

  bool get _canExportReports =>
      _session?.hasPermission(FleetPermissions.reportView) ?? false;

  /// Explain/AI narrative: require AI module + Ai.View (hide when AI unavailable).
  bool get _canExplainTrip {
    final s = _session;
    if (s == null || !s.hasAiAssistantModule) return false;
    return s.hasPermission(FleetPermissions.aiView) || s.canSeeAiTab;
  }

  String _presetLabel(_HistoryPreset preset) {
    switch (preset) {
      case _HistoryPreset.today:
        return 'Today';
      case _HistoryPreset.yesterday:
        return 'Yesterday';
      case _HistoryPreset.hours24:
        return 'Last 24 hours';
      case _HistoryPreset.days3:
        return 'Last 3 days';
      case _HistoryPreset.days7:
        return 'Last 7 days';
      case _HistoryPreset.hours6:
        return 'Last 6 hours';
      case _HistoryPreset.custom:
        return 'Custom range';
    }
  }

  @override
  void initState() {
    super.initState();
    _playbackController = PlaybackController(vsync: this)
      ..onChanged = _onPlaybackControllerChanged
      ..onFinished = () {
        if (!mounted) return;
        setState(() {
          _playing = false;
          _chromeVisible = true;
        });
      };
    _refreshSpinController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 900),
    );
    _bootstrapInitialRange();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _devicePixelRatio = MediaQuery.devicePixelRatioOf(context);
      unawaited(_loadVehicleIcon());
    });
    final deepLink = widget.tripKey?.trim();
    if (deepLink != null && deepLink.isNotEmpty) {
      unawaited(_openTripByKey(deepLink));
    } else if (widget.autoPlayFull) {
      unawaited(_loadFullRange());
    } else {
      unawaited(_load());
    }
  }

  void _bootstrapInitialRange() {
    final from = widget.initialFrom;
    final to = widget.initialTo;
    if (from != null && to != null && to.isAfter(from)) {
      _from = from;
      _to = to;
      _preset = _HistoryPreset.custom;
      return;
    }
    final mapped = _mapPresetQuery(widget.initialPreset);
    _applyPreset(mapped ?? _HistoryPreset.hours24, reload: false);
  }

  _HistoryPreset? _mapPresetQuery(String? key) {
    switch ((key ?? '').trim().toLowerCase()) {
      case 'hours6':
      case '6h':
        return _HistoryPreset.hours6;
      case 'hours24':
      case '24h':
        return _HistoryPreset.hours24;
      case 'today':
        return _HistoryPreset.today;
      case 'yesterday':
        return _HistoryPreset.yesterday;
      case 'days3':
      case '3d':
        return _HistoryPreset.days3;
      case 'days7':
      case '7d':
        return _HistoryPreset.days7;
      default:
        return null;
    }
  }

  Future<void> _loadVehicleIcon() async {
    // Blue navigation chevron (matches legend), not the live-map white car disc.
    final visible = await PlaybackMapAssets.vehicleIcon(
      devicePixelRatio: _devicePixelRatio,
    );
    if (!mounted) return;
    setState(() => _vehicleIcon = visible);
    if (_bundle != null) _rebuildMapLayers(full: true);
    _schedulePulseSync();
  }

  BitmapDescriptor _iconForPlaybackVehicle() {
    return _vehicleIcon ??
        BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure);
  }

  FleetTrackStatus _statusForPoint(HistoryReplayPoint p) {
    return resolveFleetStatus(
      speed: p.speedKmh,
      ignition: p.ignition,
      lastUpdated: p.timestamp,
      hasGps: true,
    );
  }

  void _schedulePulseSync() {
    if (_pulseSyncScheduled) return;
    _pulseSyncScheduled = true;
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      _pulseSyncScheduled = false;
      if (!mounted) return;
      final offset = await mapLatLngToOverlayOffset(
        map: _map,
        target: _displayPosition,
        stackKey: _mapStackKey,
        devicePixelRatio: _devicePixelRatio,
      );
      if (!mounted) return;
      if (offset != _pulseScreenPos) {
        setState(() => _pulseScreenPos = offset);
      }
    });
  }

  @override
  void dispose() {
    _chromeHideTimer?.cancel();
    _playbackController.dispose();
    _refreshSpinController.dispose();
    _sheetController.dispose();
    _map?.dispose();
    super.dispose();
  }

  void _bumpChromeInteraction() {
    if (!_chromeVisible) {
      setState(() => _chromeVisible = true);
    }
    _scheduleChromeHide();
  }

  void _scheduleChromeHide() {
    _chromeHideTimer?.cancel();
    if (!_playing) return;
    _chromeHideTimer = Timer(const Duration(seconds: 5), () {
      if (!mounted || !_playing) return;
      setState(() => _chromeVisible = false);
      if (_sheetController.isAttached) {
        unawaited(
          _sheetController.animateTo(
            0.26,
            duration: const Duration(milliseconds: 280),
            curve: Curves.easeOutCubic,
          ),
        );
      }
    });
  }

  void _revealChrome() {
    setState(() => _chromeVisible = true);
    _scheduleChromeHide();
  }

  void _onMapTap(LatLng _) {
    if (!_chromeVisible) {
      _revealChrome();
      return;
    }
    _bumpChromeInteraction();
    _showVehicleSheet();
  }

  List<TripEvent> get _filteredEvents {
    final b = _bundle;
    if (b == null) return const [];
    // Stops live in `bundle.stops` (Traccar report), not event types — hide
    // unrelated event pins when the Stops chip is selected.
    if (_eventFilter == TripEventFilter.stops) return const [];
    return filterEvents(b.events, _eventFilter);
  }

  /// Stops drawn on the map (toggle + Stops filter), with valid coords only.
  List<TripStop> get _mapStops {
    if (!_showStopMarkers && _eventFilter != TripEventFilter.stops) {
      return const [];
    }
    return _allStops
        .where((s) => isValidPlaybackCoord(s.latitude, s.longitude))
        .toList(growable: false);
  }

  List<int> get _eventIndices {
    if (_playbackPoints.isEmpty) return const [];
    return eventMarkerIndices(_playbackPoints, _filteredEvents);
  }

  void _syncDistSoFar() {
    final b = _bundle;
    if (b == null) {
      _distSoFar = 0;
      return;
    }
    final trail = b.trailPoints;
    final trailIdx = trailIndexForPlaybackIndex(trail, _playbackPoints, _index);
    _distSoFar = distanceAlongTrailKm(trail, trailIdx);
  }

  void _composeMarkers({
    HistoryReplayPoint? vehiclePoint,
    LatLng? positionOverride,
    double? headingOverride,
  }) {
    if (_vehicleIcon == null) return;
    final point = vehiclePoint ??
        (_playbackPoints.isNotEmpty
            ? _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)]
            : null);
    if (point == null) {
      _markers = Set.of(_staticMarkers);
      return;
    }
    _displayPosition =
        positionOverride ?? LatLng(point.latitude, point.longitude);
    _displayHeading = headingOverride ?? point.heading ?? 0;
    _markers = {
      ..._staticMarkers,
      buildVehicleMarker(
        point: point,
        vehicleIcon: _iconForPlaybackVehicle(),
        positionOverride: _displayPosition,
        headingOverride: _displayHeading,
      ),
    };
    _schedulePulseSync();
  }

  void _rebuildPolylines() {
    final b = _bundle;
    if (b == null) return;
    final trail = b.trailPoints;
    final trailIdx = trailIndexForPlaybackIndex(trail, _playbackPoints, _index);
    _polylines = buildPlaybackPolylines(
      trail: trail,
      trailIndex: trailIdx,
      stops: b.stops,
    );
  }

  void _rebuildMapLayers({bool full = true}) {
    final b = _bundle;
    if (b == null) return;
    _rebuildPolylines();
    final vehiclePoint = _playbackPoints.isEmpty
        ? null
        : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)];
    _composeMarkers(vehiclePoint: vehiclePoint);
    setState(() {});
    final playbackTime = _virtualTime ?? vehiclePoint?.timestamp;
    final visitSig = stopVisitSignature(_mapStops, playbackTime);
    if (full || visitSig != _lastStopVisitSignature) {
      _refreshStaticMarkers();
    }
  }

  void _refreshStaticMarkers() {
    final b = _bundle;
    if (b == null) return;
    final vehiclePoint = _playbackPoints.isEmpty
        ? null
        : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)];
    final playbackTime = _virtualTime ?? vehiclePoint?.timestamp;
    _lastStopVisitSignature = stopVisitSignature(_mapStops, playbackTime);
    unawaited(
      buildPlaybackStaticMarkers(
        trail: b.trailPoints,
        playback: _playbackPoints,
        stops: _mapStops,
        events: _filteredEvents,
        devicePixelRatio: _devicePixelRatio,
        vehiclePoint: vehiclePoint,
        playbackTime: playbackTime,
        onMarkerTap: _onPlaybackMarkerTap,
      ).then((staticMarkers) {
        if (!mounted) return;
        setState(() {
          _staticMarkers = staticMarkers;
          _composeMarkers(
            vehiclePoint: _playbackPoints.isEmpty
                ? null
                : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)],
          );
        });
      }),
    );
  }

  void _onPlaybackControllerChanged() {
    if (!mounted) return;
    final points = _playbackPoints;
    final nextIndex = _playbackController.index.clamp(
      0,
      points.isEmpty ? 0 : points.length - 1,
    );
    final now = DateTime.now().millisecondsSinceEpoch;
    final shouldRefreshPolyline = now - _lastPolylineRefreshMs >= 250;
    final shouldRefreshDistance = nextIndex != _lastDistanceIndex;
    final visitSig = stopVisitSignature(
      _mapStops,
      _playbackController.virtualTime ??
          (points.isEmpty ? null : points[nextIndex].timestamp),
    );
    final shouldRefreshStopColors = visitSig != _lastStopVisitSignature;
    setState(() {
      _playing = _playbackController.playing;
      _speed = _playbackController.speed;
      _index = nextIndex;
      _virtualTime = _playbackController.virtualTime;
      _displayPosition = _playbackController.displayPosition;
      _displayHeading = _playbackController.displayHeading;
      if (shouldRefreshDistance) {
        _lastDistanceIndex = nextIndex;
        _syncDistSoFar();
      }
      if (shouldRefreshPolyline) {
        _lastPolylineRefreshMs = now;
        _rebuildPolylines();
      }
      _composeMarkers(
        vehiclePoint: points.isEmpty ? null : points[_index],
        positionOverride: _displayPosition,
        headingOverride: _displayHeading,
      );
    });
    if (shouldRefreshStopColors) {
      _lastStopVisitSignature = visitSig;
      _refreshStaticMarkers();
    }
    if (_follow && _playbackController.playing && _displayPosition != null) {
      unawaited(_animateMap(CameraUpdate.newLatLng(_displayPosition!)));
    }
  }

  void _stopPlay() {
    _chromeHideTimer?.cancel();
    _playbackController.pause();
    if (_playing) {
      setState(() {
        _playing = false;
        _chromeVisible = true;
      });
    }
  }

  void _togglePlay() {
    _bumpChromeInteraction();
    if (_playing) {
      _stopPlay();
      return;
    }
    if (_playbackPoints.isEmpty) return;
    // Full-trip control clears any stop→next-stop segment bound.
    _playbackController.clearPlayUntil();
    if (_index >= _playbackPoints.length - 1) {
      _playbackController.seekToIndex(0);
    }
    final started = _playbackController.play();
    if (!started) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Need at least 2 GPS points to play'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }
    setState(() {
      _playing = true;
      _follow = true;
      _chromeVisible = true;
    });
    _scheduleChromeHide();
    if (_displayPosition != null) {
      unawaited(_animateMap(CameraUpdate.newLatLng(_displayPosition!)));
    }
  }

  /// Plays only stop N → next stop (last stop → trip end). Does not alter full-trip play.
  void _playStopSegment(int stopIndex, {List<TripStop>? stopsSource}) {
    _bumpChromeInteraction();
    final points = _playbackPoints;
    if (points.isEmpty) return;
    final stops = stopsSource ??
        (_allStops.isNotEmpty
            ? _allStops
            : (_bundle?.stops ?? const <TripStop>[]));
    final range = stopSegmentRange(
      playback: points,
      stops: stops,
      stopIndex: stopIndex,
    );
    if (range == null) return;

    final stop = stops[stopIndex];
    _stopPlay();
    _playbackController.clearPlayUntil();
    _playbackController.seekToIndex(range.startIndex);
    _index = range.startIndex;
    _virtualTime = points[range.startIndex].timestamp;
    _rebuildMapLayers(full: false);

    if (isValidPlaybackCoord(stop.latitude, stop.longitude)) {
      if (!_showStopMarkers) {
        _toggleIncludeStops(true);
      }
      final map = _map;
      if (map != null) {
        unawaited(
          map.animateCamera(
            CameraUpdate.newLatLngZoom(
              LatLng(stop.latitude, stop.longitude),
              16,
            ),
          ),
        );
      }
    }

    if (range.endIndex <= range.startIndex) {
      return;
    }
    final started = _playbackController.play(untilIndex: range.endIndex);
    if (!started) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Need at least 2 GPS points to play'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }
    setState(() {
      _playing = true;
      _follow = true;
      _chromeVisible = true;
    });
    _scheduleChromeHide();
  }

  void _maybeFollowCamera(HistoryReplayPoint p) {
    if (!_follow) return;
    unawaited(
      _animateMap(CameraUpdate.newLatLng(LatLng(p.latitude, p.longitude))),
    );
  }

  Future<void> _zoomBy(double delta) async {
    _bumpChromeInteraction();
    final map = _map;
    if (map == null || !mounted) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Map is still loading. Try again in a moment.'),
            duration: Duration(seconds: 2),
          ),
        );
      }
      return;
    }
    try {
      // Prefer absolute zoom — more reliable than zoomBy on some Android builds.
      final current = await map.getZoomLevel();
      final next = (current + delta).clamp(3.0, 20.0);
      await map.animateCamera(CameraUpdate.zoomTo(next));
    } catch (_) {
      await _animateMap(
        delta > 0 ? CameraUpdate.zoomIn() : CameraUpdate.zoomOut(),
      );
    }
  }

  Future<void> _centerOnVehicle() async {
    _bumpChromeInteraction();
    final map = _map;
    LatLng? pos = _displayPosition;
    if (pos == null && _playbackPoints.isNotEmpty) {
      final i = _index.clamp(0, _playbackPoints.length - 1);
      pos = LatLng(
        _playbackPoints[i].latitude,
        _playbackPoints[i].longitude,
      );
    }
    if (map == null || pos == null) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('No GPS position available to center on.'),
            duration: Duration(seconds: 2),
          ),
        );
      }
      return;
    }
    try {
      final zoom = await map.getZoomLevel();
      await map.animateCamera(
        CameraUpdate.newCameraPosition(
          CameraPosition(
            target: pos,
            zoom: zoom < 14 ? 16 : zoom,
            bearing: _displayHeading ??
                (_playbackPoints.isNotEmpty
                    ? (_playbackPoints[
                                _index.clamp(0, _playbackPoints.length - 1)]
                            .heading ??
                        0)
                    : 0),
          ),
        ),
      );
      if (mounted) setState(() => _follow = true);
    } catch (_) {
      await _animateMap(CameraUpdate.newLatLngZoom(pos, 16));
    }
  }

  void _setIndex(int idx) {
    _bumpChromeInteraction();
    if (_playbackPoints.isEmpty) return;
    final clamped = idx.clamp(0, _playbackPoints.length - 1);
    _stopPlay();
    _playbackController.seekToIndex(clamped);
    _rebuildMapLayers(full: false);
    _maybeFollowCamera(_playbackPoints[clamped]);
  }

  void _seekVirtualTime(DateTime time) {
    _bumpChromeInteraction();
    if (_playbackPoints.isEmpty) return;
    _stopPlay();
    _playbackController.seekToTimestamp(time);
    _rebuildMapLayers(full: false);
    if (_displayPosition != null) {
      _maybeFollowCamera(
        HistoryReplayPoint(
          timestamp: time,
          latitude: _displayPosition!.latitude,
          longitude: _displayPosition!.longitude,
          speedKmh: 0,
          heading: _displayHeading,
        ),
      );
    }
  }

  void _jumpEvent(int direction) {
    final indices = _eventIndices;
    if (indices.isEmpty) return;
    final currentTrail = _index;
    if (direction < 0) {
      for (final i in indices.reversed) {
        if (i < currentTrail) {
          _setIndex(i);
          return;
        }
      }
      _setIndex(indices.first);
    } else {
      for (final i in indices) {
        if (i > currentTrail) {
          _setIndex(i);
          return;
        }
      }
      _setIndex(indices.last);
    }
  }

  void _applyPreset(_HistoryPreset preset, {bool reload = true}) {
    final now = DateTime.now();
    late DateTime from;
    late DateTime to;
    switch (preset) {
      case _HistoryPreset.hours6:
        to = now;
        from = now.subtract(const Duration(hours: 6));
      case _HistoryPreset.hours24:
        to = now;
        from = now.subtract(const Duration(hours: 24));
      case _HistoryPreset.days3:
        to = now;
        from = now.subtract(const Duration(days: 3));
      case _HistoryPreset.days7:
        to = now;
        from = now.subtract(const Duration(days: 7));
      case _HistoryPreset.today:
        to = now;
        from = DateTime(now.year, now.month, now.day);
      case _HistoryPreset.yesterday:
        final startToday = DateTime(now.year, now.month, now.day);
        to = startToday;
        from = startToday.subtract(const Duration(days: 1));
      case _HistoryPreset.custom:
        from = _from;
        to = _to;
    }
    setState(() {
      _preset = preset;
      _from = from;
      _to = to;
      _selectedTrip = null;
      _tripDetail = null;
      _playingFullRange = false;
      _bundle = null;
      _playbackPoints = const [];
    });
    if (reload) unawaited(_load());
  }

  Future<void> _pickCustomRange() async {
    final now = DateTime.now();
    final fromDate = await showDatePicker(
      context: context,
      initialDate: _from,
      firstDate: now.subtract(const Duration(days: 365)),
      lastDate: now,
      helpText: 'From date',
    );
    if (fromDate == null || !mounted) return;
    final fromTime = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_from),
      helpText: 'From time',
    );
    if (!mounted) return;
    final from = DateTime(
      fromDate.year,
      fromDate.month,
      fromDate.day,
      fromTime?.hour ?? 0,
      fromTime?.minute ?? 0,
    );
    final toDate = await showDatePicker(
      context: context,
      initialDate: _to.isAfter(from) ? _to : from,
      firstDate: from,
      lastDate: now,
      helpText: 'To date',
    );
    if (toDate == null || !mounted) return;
    final toTime = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_to),
      helpText: 'To time',
    );
    if (!mounted) return;
    var to = DateTime(
      toDate.year,
      toDate.month,
      toDate.day,
      toTime?.hour ?? 23,
      toTime?.minute ?? 59,
    );
    if (!to.isAfter(from)) to = from.add(const Duration(hours: 1));
    if (to.difference(from) > const Duration(days: 7)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Max range is 7 days.')),
        );
      }
      to = from.add(const Duration(days: 7));
    }
    setState(() {
      _preset = _HistoryPreset.custom;
      _from = from;
      _to = to;
      _selectedTrip = null;
      _tripDetail = null;
      _playingFullRange = false;
      _bundle = null;
      _playbackPoints = const [];
    });
    await _load();
  }

  void _clearPlaybackState() {
    _stopPlay();
    _bundle = null;
    _tripDetail = null;
    _selectedTrip = null;
    _playingFullRange = false;
    _playbackPoints = const [];
    _index = 0;
    _distSoFar = 0;
    _lastDistanceIndex = -1;
    _didInitialCameraFocus = false;
    _virtualTime = null;
    _displayPosition = null;
    _displayHeading = null;
    _polylines = {};
    _staticMarkers = {};
    _markers = {};
    _playbackController.setPoints(const []);
  }

  void _applyReplayBundle(
    HistoryReplayBundle bundle, {
    GpsTrip? trip,
    TripDetailBundle? detail,
    required bool fullRange,
  }) {
    // Always keep full stops on the bundle (KPI / sheet / timeline). Map pins
    // are gated separately via `_mapStops` / `_showStopMarkers`.
    _allStops = detail?.stops ?? bundle.stops;
    final filtered = HistoryReplayBundle(
      route: bundle.route,
      playback: bundle.playback,
      stops: _allStops,
      events: bundle.events,
      summary: bundle.summary,
      statistics: bundle.statistics,
      mileageKm: bundle.mileageKm,
      vehicle: bundle.vehicle,
      gpsDeviceId: bundle.gpsDeviceId,
    );
    final playbackPoints = effectivePlaybackPoints(filtered);
    setState(() {
      _bundle = filtered;
      _playbackPoints = playbackPoints;
      _selectedTrip = trip ?? detail?.trip;
      _tripDetail = detail;
      _playingFullRange = fullRange;
      _index = 0;
      _distSoFar = 0;
      _lastDistanceIndex = -1;
      _didInitialCameraFocus = false;
      _virtualTime =
          playbackPoints.isEmpty ? null : playbackPoints.first.timestamp;
      _displayPosition = playbackPoints.isEmpty
          ? null
          : LatLng(
              playbackPoints.first.latitude,
              playbackPoints.first.longitude,
            );
      _displayHeading =
          playbackPoints.isEmpty ? null : playbackPoints.first.heading;
      _loading = false;
      _loadingTripDetail = false;
      _error = null;
      _chromeVisible = true;
      _playing = false;
    });
    _refreshSpinController.stop();
    _refreshSpinController.value = 0;
    _playbackController.setPoints(playbackPoints);
    // Start paused at beginning (product default).
    _playbackController.pause();
    _rebuildMapLayers(full: true);
    _maybeFocusPlaybackStart();
  }

  Future<void> _load() async {
    _stopPlay();
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
      _selectedTrip = null;
      _tripDetail = null;
      _playingFullRange = false;
      _bundle = null;
      _playbackPoints = const [];
      _trips = const [];
    });
    _refreshSpinController.repeat();
    try {
      final trips = await ref.read(fleetApiProvider).getGpsTrips(
            vehicleId: widget.vehicleId,
            from: _from.toUtc(),
            to: _to.toUtc(),
          );
      if (!mounted) return;
      setState(() {
        _trips = trips;
        _loading = false;
        _chromeVisible = true;
      });
      _refreshSpinController.stop();
      _refreshSpinController.value = 0;
      _playbackController.setPoints(const []);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = _friendlyError(e);
      });
      _refreshSpinController.stop();
      _refreshSpinController.value = 0;
    }
  }

  Future<void> _selectTrip(GpsTrip trip) async {
    final key = trip.tripKey?.trim();
    if (key == null || key.isEmpty) {
      // Fallback: scoped replay by trip window.
      await _playTripWindow(trip);
      return;
    }
    await _openTripByKey(key, fallbackTrip: trip);
  }

  Future<void> _playTripWindow(GpsTrip trip) async {
    _stopPlay();
    if (!mounted) return;
    setState(() {
      _loading = true;
      _loadingTripDetail = true;
      _error = null;
    });
    _refreshSpinController.repeat();
    try {
      final bundle = await ref.read(fleetApiProvider).getTripReplay(
            vehicleId: widget.vehicleId,
            from: trip.startTime.toUtc(),
            to: trip.endTime.toUtc(),
          );
      if (!mounted) return;
      _allStops = bundle.stops;
      _applyReplayBundle(bundle, trip: trip, detail: null, fullRange: false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _loadingTripDetail = false;
        _error = _friendlyError(e);
      });
      _refreshSpinController.stop();
      _refreshSpinController.value = 0;
    }
  }

  Future<void> _openTripByKey(
    String tripKey, {
    GpsTrip? fallbackTrip,
  }) async {
    _stopPlay();
    if (!mounted) return;
    setState(() {
      _loading = true;
      _loadingTripDetail = true;
      _error = null;
    });
    _refreshSpinController.repeat();
    try {
      final detail = await ref.read(fleetApiProvider).getTripDetail(tripKey);
      if (!mounted) return;
      final bundle = detail.toHistoryReplayBundle(includeStops: true);
      _applyReplayBundle(
        bundle,
        trip: detail.trip.tripKey != null ? detail.trip : fallbackTrip,
        detail: detail,
        fullRange: false,
      );
    } catch (e) {
      if (!mounted) return;
      if (fallbackTrip != null) {
        await _playTripWindow(fallbackTrip);
        return;
      }
      setState(() {
        _loading = false;
        _loadingTripDetail = false;
        _error = _friendlyError(e);
      });
      _refreshSpinController.stop();
      _refreshSpinController.value = 0;
    }
  }

  Future<void> _loadFullRange() async {
    _stopPlay();
    if (!mounted) return;
    setState(() {
      _loading = true;
      _loadingTripDetail = true;
      _error = null;
      _selectedTrip = null;
      _tripDetail = null;
    });
    _refreshSpinController.repeat();
    try {
      final bundle = await ref.read(fleetApiProvider).getHistoryReplay(
            widget.vehicleId,
            from: _from.toUtc(),
            to: _to.toUtc(),
          );
      if (!mounted) return;
      final points = effectivePlaybackPoints(bundle);
      if (points.isEmpty) {
        setState(() {
          _loading = false;
          _loadingTripDetail = false;
          _playingFullRange = false;
          _bundle = null;
          _playbackPoints = const [];
          _error =
              'No GPS positions were recorded for this vehicle during the selected period.';
        });
        _refreshSpinController.stop();
        _refreshSpinController.value = 0;
        return;
      }
      _applyReplayBundle(bundle, trip: null, detail: null, fullRange: true);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _loadingTripDetail = false;
        _error = _friendlyError(e);
      });
      _refreshSpinController.stop();
      _refreshSpinController.value = 0;
    }
  }

  Future<void> _backToTripList() async {
    _clearPlaybackState();
    _loadingTripDetail = false;
    if (_trips.isEmpty) {
      await _load();
    } else if (mounted) {
      setState(() {});
    }
  }

  void _toggleIncludeStops(bool value) {
    setState(() => _showStopMarkers = value);
    _rebuildMapLayers(full: true);
  }

  PlaybackTripMetrics get _tripMetrics => PlaybackTripMetrics.resolve(
        bundle: _bundle,
        selectedTrip: _selectedTrip ?? _tripDetail?.trip,
        stops: _allStops,
        rangeFrom: _from,
        rangeTo: _to,
      );

  String _tripWindowLabel() {
    final metrics = _tripMetrics;
    final trip = _selectedTrip ?? _tripDetail?.trip;
    if (trip != null || _playingFullRange) {
      return '${metrics.timeRangeLabel(timeFmt: _tripTf)} · ${metrics.distanceLabel}';
    }
    return '';
  }

  List<GpsTrip> get _navigableTrips =>
      filterTrips(_trips, TripListFilter.moving);

  int get _selectedTripIndex {
    final trip = _selectedTrip ?? _tripDetail?.trip;
    if (trip == null) return -1;
    final key = trip.tripKey?.trim();
    final list = _navigableTrips;
    if (key != null && key.isNotEmpty) {
      final byKey = list.indexWhere((t) => t.tripKey == key);
      if (byKey >= 0) return byKey;
    }
    return list.indexWhere(
      (t) =>
          t.startTime == trip.startTime &&
          t.endTime == trip.endTime &&
          t.vehicleId == trip.vehicleId,
    );
  }

  Future<void> _goAdjacentTrip(int delta) async {
    final list = _navigableTrips;
    final idx = _selectedTripIndex;
    if (idx < 0 || list.isEmpty) return;
    final next = idx + delta;
    if (next < 0 || next >= list.length) return;
    await _selectTrip(list[next]);
  }

  Future<void> _animateMap(CameraUpdate update) async {
    final map = _map;
    if (map == null || !mounted) return;
    try {
      await map.animateCamera(update);
    } catch (_) {}
  }

  /// Centers on the first playback GPS point once after data + map are ready.
  void _maybeFocusPlaybackStart() {
    if (_didInitialCameraFocus) return;
    if (_map == null) return;
    if (_playbackPoints.isEmpty) return;
    _didInitialCameraFocus = true;
    final first = _playbackPoints.first;
    unawaited(
      _animateMap(
        CameraUpdate.newCameraPosition(
          CameraPosition(
            target: LatLng(first.latitude, first.longitude),
            zoom: 16,
            bearing: first.heading ?? 0,
          ),
        ),
      ),
    );
  }

  String _friendlyError(Object e) {
    if (e is ForbiddenException) {
      return 'You do not have permission to view GPS trip history.';
    }
    final msg = ErrorHandler.message(e);
    final lower = msg.toLowerCase();
    if (lower.contains('403') ||
        lower.contains('forbidden') ||
        lower.contains('do not have permission')) {
      return 'You do not have permission to view GPS trip history.';
    }
    if (e is DioException &&
        (e.type == DioExceptionType.receiveTimeout ||
            e.type == DioExceptionType.connectionTimeout)) {
      return 'GPS history timed out. Try a shorter range, then Retry.';
    }
    return msg;
  }

  Future<void> _shareTextFile({
    required String content,
    required String filename,
    required String mimeType,
    required String subject,
  }) async {
    try {
      final dir = await getTemporaryDirectory();
      final file = File('${dir.path}/$filename');
      await file.writeAsString(content, flush: true);
      // Real file share (share_plus ShareParams / formerly shareXFiles).
      await SharePlus.instance.share(
        ShareParams(
          files: [XFile(file.path, mimeType: mimeType, name: filename)],
          subject: subject,
        ),
      );
    } catch (_) {
      // Fallback when file share is unavailable on the platform.
      await SharePlus.instance.share(
        ShareParams(text: content, subject: subject),
      );
    }
  }

  Future<void> _fitRoute() async {
    final trail = _bundle?.trailPoints ?? const [];
    if (trail.isEmpty) return;
    if (trail.length == 1) {
      await _animateMap(
        CameraUpdate.newLatLngZoom(
          LatLng(trail.first.latitude, trail.first.longitude),
          16,
        ),
      );
      return;
    }
    var minLat = trail.first.latitude;
    var maxLat = trail.first.latitude;
    var minLng = trail.first.longitude;
    var maxLng = trail.first.longitude;
    for (final p in trail) {
      minLat = minLat < p.latitude ? minLat : p.latitude;
      maxLat = maxLat > p.latitude ? maxLat : p.latitude;
      minLng = minLng < p.longitude ? minLng : p.longitude;
      maxLng = maxLng > p.longitude ? maxLng : p.longitude;
    }
    // Tiny / stationary trails: LatLngBounds is unreliable — zoom to start.
    if ((maxLat - minLat) < 0.0005 && (maxLng - minLng) < 0.0005) {
      await _animateMap(
        CameraUpdate.newLatLngZoom(
          LatLng(trail.first.latitude, trail.first.longitude),
          16,
        ),
      );
      return;
    }
    await _animateMap(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(
          southwest: LatLng(minLat, minLng),
          northeast: LatLng(maxLat, maxLng),
        ),
        48,
      ),
    );
  }

  Future<void> _shareGpx() async {
    final b = _bundle;
    if (b == null || !_canExportReports) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final gpx = buildGpx(b.trailPoints, name: name);
    final safe = name.replaceAll(RegExp(r'[^\w\-]+'), '_');
    await _shareTextFile(
      content: gpx,
      filename: 'sheikhgo_${safe}_${widget.vehicleId}.gpx',
      mimeType: 'application/gpx+xml',
      subject: 'GPS replay $name',
    );
  }

  Future<void> _shareCsv() async {
    final b = _bundle;
    if (b == null || !_canExportReports) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final csv = buildCsv(b.trailPoints);
    final safe = name.replaceAll(RegExp(r'[^\w\-]+'), '_');
    await _shareTextFile(
      content: csv,
      filename: 'sheikhgo_${safe}_${widget.vehicleId}.csv',
      mimeType: 'text/csv',
      subject: 'GPS replay CSV $name',
    );
  }

  Future<void> _shareKml() async {
    final b = _bundle;
    if (b == null || !_canExportReports) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final kml = buildKml(b.trailPoints, name: name);
    final safe = name.replaceAll(RegExp(r'[^\w\-]+'), '_');
    await _shareTextFile(
      content: kml,
      filename: 'sheikhgo_${safe}_${widget.vehicleId}.kml',
      mimeType: 'application/vnd.google-earth.kml+xml',
      subject: 'GPS replay KML $name',
    );
  }

  Future<void> _shareTripReport() async {
    final b = _bundle;
    if (b == null || !_canExportReports) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final plate = b.vehicle?.plateNumber ?? '';
    final stats = PlaybackStats.fromBundle(b);
    final dist = stats.distanceKm;
    final drive = stats.drivingMinutes;
    final idle = stats.idleMinutes;
    final report = StringBuffer()
      ..writeln('SheikhGo Trip Report')
      ..writeln(name)
      ..writeln(plate)
      ..writeln('Distance: ${formatDistanceKm(dist)}')
      ..writeln('Driving: ${formatDurationMinutes(drive)}')
      ..writeln('Idle: ${formatDurationMinutes(idle)}')
      ..writeln('Stops: ${b.stops.length}')
      ..writeln(
        'Avg / Max: ${stats.avgSpeedKmh.toStringAsFixed(0)} / '
        '${stats.maxSpeedKmh.toStringAsFixed(0)} km/h',
      )
      ..writeln()
      ..writeln(buildTripNarrative(b));
    final safe = name.replaceAll(RegExp(r'[^\w\-]+'), '_');
    await _shareTextFile(
      content: report.toString(),
      filename: 'sheikhgo_trip_report_${safe}_${widget.vehicleId}.txt',
      mimeType: 'text/plain',
      subject: 'Trip report $name',
    );
  }

  void _copyCoords() {
    final p = _playbackPoints.isNotEmpty
        ? _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)]
        : null;
    if (p == null) return;
    Clipboard.setData(
      ClipboardData(text: '${p.latitude}, ${p.longitude}'),
    );
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Coordinates copied')),
    );
  }

  void _shareSummary() {
    final b = _bundle;
    if (b == null) return;
    unawaited(
      SharePlus.instance.share(ShareParams(text: buildTripNarrative(b))),
    );
  }

  void _showStopsSheet([HistoryReplayBundle? bundle]) {
    final stops = _allStops.isNotEmpty
        ? _allStops
        : (bundle?.stops ?? _bundle?.stops ?? const <TripStop>[]);
    if (stops.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No stops in this range')),
      );
      return;
    }
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.5,
        maxChildSize: 0.9,
        builder: (_, scroll) => FutureBuilder<List<String>>(
          future: _resolveStopAddresses(stops),
          builder: (context, snap) {
            final addresses = snap.data;
            return ListView(
              controller: scroll,
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  'Stops & parking (${stops.length})',
                  style: const TextStyle(
                      fontSize: 18, fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 12),
                for (var i = 0; i < stops.length; i++)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: CircleAvatar(
                      backgroundColor: stops[i].durationMinutes >= 120
                          ? const Color(0xFFDBEAFE)
                          : const Color(0xFFE0F2FE),
                      child: Icon(
                        stops[i].durationMinutes >= 120
                            ? Icons.local_parking
                            : Icons.pause_circle,
                        color: stops[i].durationMinutes >= 120
                            ? const Color(0xFF1D4ED8)
                            : const Color(0xFF0C4A6E),
                        size: 18,
                      ),
                    ),
                    title: Text(
                      stops[i].durationMinutes >= 120 ? 'Parking' : 'Stop',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      [
                        playbackAddressPrimary(
                          addresses != null && i < addresses.length
                              ? addresses[i]
                              : stops[i].address,
                          lat: stops[i].latitude,
                          lng: stops[i].longitude,
                        ),
                        if (playbackNearbyLine(
                          addresses != null && i < addresses.length
                              ? addresses[i]
                              : stops[i].address,
                        )
                            case final nearby?)
                          nearby,
                        if (playbackAddressSecondary(
                          addresses != null && i < addresses.length
                              ? addresses[i]
                              : stops[i].address,
                        )
                            case final locality?)
                          locality,
                        formatStopWindow(
                          startTime: stops[i].startTime,
                          endTime: stops[i].endTime,
                        ),
                        'Duration: ${formatDurationMinutesCompact(stops[i].durationMinutes)}',
                      ].join('\n'),
                    ),
                    isThreeLine: true,
                    onTap: () {
                      Navigator.pop(ctx);
                      _playStopSegment(i, stopsSource: stops);
                    },
                  ),
              ],
            );
          },
        ),
      ),
    );
  }

  Future<List<String>> _resolveStopAddresses(List<TripStop> stops) async {
    final out = <String>[];
    for (final s in stops) {
      final inline = s.address?.trim();
      if (inline != null &&
          inline.isNotEmpty &&
          !isCoarsePlaybackAddress(inline)) {
        out.add(inline);
        continue;
      }
      if (!isValidPlaybackCoord(s.latitude, s.longitude)) {
        out.add(formatPlaybackCoords(s.latitude, s.longitude));
        continue;
      }
      final key =
          '${s.latitude.toStringAsFixed(5)},${s.longitude.toStringAsFixed(5)}';
      final cached = _addressCache[key];
      if (cached != null && !isCoarsePlaybackAddress(cached)) {
        out.add(cached);
        continue;
      }
      try {
        // Existing reverse-geocode path — do not forceRefresh (cost control).
        final resolved = await ref.read(fleetApiProvider).reverseGeocode(
              s.latitude,
              s.longitude,
              forceRefresh: false,
            );
        if (resolved != null && resolved.trim().isNotEmpty) {
          final line = resolved.trim();
          _addressCache[key] = line;
          out.add(line);
          continue;
        }
      } catch (_) {}
      out.add(
        inline != null && inline.isNotEmpty
            ? inline
            : formatPlaybackCoords(s.latitude, s.longitude),
      );
    }
    return out;
  }

  void _showAnalyticsSheet() {
    final b = _bundle;
    if (b == null) return;
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.55,
        maxChildSize: 0.9,
        builder: (_, scroll) => ListView(
          controller: scroll,
          padding: const EdgeInsets.all(16),
          children: [
            const Text(
              'Trip analytics',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 12),
            PlaybackSummaryCard(
              bundle: b,
              selectedTrip: _selectedTrip ?? _tripDetail?.trip,
              stops: _allStops,
            ),
            const SizedBox(height: 12),
            if (_allStops.isNotEmpty)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.pause_circle_outline),
                title: Text('${_allStops.length} stops & parking'),
                subtitle:
                    const Text('Open list for address, time, and duration'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () {
                  Navigator.pop(ctx);
                  _showStopsSheet();
                },
              ),
            if (_canExplainTrip) ...[
              const SizedBox(height: 8),
              SgPrimaryButton(
                label: 'Explain this trip',
                icon: Icons.auto_awesome_outlined,
                onPressed: () {
                  Navigator.pop(ctx);
                  unawaited(_showAiNarrative());
                },
              ),
            ],
          ],
        ),
      ),
    );
  }

  /// Selected trip window (not the list date-range `_from`/`_to`).
  (DateTime from, DateTime to) _insightWindow() {
    final trip = _selectedTrip ?? _tripDetail?.trip;
    if (trip != null) {
      return (trip.startTime, trip.endTime);
    }
    final points = _playbackPoints.isNotEmpty
        ? _playbackPoints
        : (_bundle?.trailPoints ?? const <HistoryReplayPoint>[]);
    if (points.length >= 2) {
      return (points.first.timestamp, points.last.timestamp);
    }
    if (points.length == 1) {
      return (points.first.timestamp, points.first.timestamp);
    }
    return (_from, _to);
  }

  Future<void> _showAiNarrative() async {
    final b = _bundle;
    if (b == null || !_canExplainTrip) return;
    final window = _insightWindow();
    try {
      final insight = await ref.read(fleetApiProvider).getReplayInsights(
            widget.vehicleId,
            from: window.$1.toUtc(),
            to: window.$2.toUtc(),
          );
      if (!mounted) return;
      showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(insight.title),
          content: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(insight.summary),
                if (insight.bullets.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  for (final line in insight.bullets)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Text('• $line'),
                    ),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Close'),
            ),
          ],
        ),
      );
    } catch (_) {
      if (!mounted) return;
      showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Trip summary'),
          content: Text(buildTripNarrative(b)),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Close'),
            ),
          ],
        ),
      );
    }
  }

  void _onPlaybackMarkerTap(PlaybackMarkerTap tap) {
    if (_playbackPoints.isEmpty) return;
    if (tap.eventType == 'Stop' && tap.stopIndex != null) {
      _playStopSegment(tap.stopIndex!, stopsSource: _mapStops);
      return;
    }
    final next = tap.playbackIndex.clamp(0, _playbackPoints.length - 1);
    _setIndex(next);
    _showVehicleSheet(
      eventType: tap.eventType,
      eventLabel: tap.eventLabel,
      stopDurationMinutes: tap.stopDurationMinutes,
    );
  }

  Future<String> _resolveAddressLine(HistoryReplayPoint p) async {
    final inline = p.address?.trim();
    final key =
        '${p.latitude.toStringAsFixed(5)},${p.longitude.toStringAsFixed(5)}';
    final cached = _addressCache[key];
    if (cached != null && !isCoarsePlaybackAddress(cached)) return cached;

    // Prefer cache / Nominatim — avoid forceRefresh storms on history open.
    final fallback = (inline != null && inline.isNotEmpty)
        ? inline
        : formatPlaybackCoords(p.latitude, p.longitude);
    try {
      final resolved = await ref.read(fleetApiProvider).reverseGeocode(
            p.latitude,
            p.longitude,
            forceRefresh: false,
          );
      if (resolved != null && resolved.trim().isNotEmpty) {
        final line = resolved.trim();
        _addressCache[key] = line;
        return line;
      }
    } catch (_) {}
    return fallback;
  }

  void _showVehicleSheet({
    String? eventType,
    String? eventLabel,
    int? stopDurationMinutes,
  }) {
    final b = _bundle;
    final p = _playbackPoints.isNotEmpty
        ? _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)]
        : null;
    if (b == null || p == null) return;
    final v = b.vehicle;
    final initialAddress = playbackAddressLine(p);
    final status = _statusForPoint(p).name;
    showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => FutureBuilder<String>(
        future: _resolveAddressLine(p),
        initialData: initialAddress,
        builder: (context, snap) {
          final addressLine = snap.data ?? initialAddress;
          return Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  v?.vehicleName ?? 'Vehicle #${widget.vehicleId}',
                  style: const TextStyle(
                      fontWeight: FontWeight.w800, fontSize: 16),
                ),
                if (v?.plateNumber != null)
                  Text(v!.plateNumber!,
                      style: const TextStyle(color: AppColors.textSecondary)),
                if (eventType != null) ...[
                  const SizedBox(height: 8),
                  _infoRow('Event', eventLabel ?? eventType),
                  if (stopDurationMinutes != null)
                    _infoRow('Stop duration', '$stopDurationMinutes min'),
                ],
                const SizedBox(height: 12),
                _infoRow(
                  'Location',
                  playbackAddressPrimary(
                    addressLine,
                    lat: p.latitude,
                    lng: p.longitude,
                  ),
                  multiline: true,
                ),
                if (playbackAddressSecondary(addressLine) case final locality?)
                  _infoRow('Area', locality, multiline: true),
                _infoRow(
                  'Coordinates',
                  formatPlaybackCoords(p.latitude, p.longitude),
                ),
                _infoRow('GPS Time', formatPlaybackDateTime(p.timestamp)),
                _infoRow('Speed', '${p.speedKmh.toStringAsFixed(0)} km/h'),
                _infoRow('Heading', headingToCardinal(p.heading)),
                _infoRow(
                    'Status', status[0].toUpperCase() + status.substring(1)),
                _infoRow(
                  'Ignition',
                  p.ignition == null ? '—' : (p.ignition! ? 'ON' : 'OFF'),
                ),
                if (snap.connectionState == ConnectionState.waiting &&
                    isCoarsePlaybackAddress(addressLine))
                  const Padding(
                    padding: EdgeInsets.only(bottom: 6),
                    child: Text(
                      'Resolving exact location…',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textMuted,
                      ),
                    ),
                  ),
                TextButton.icon(
                  onPressed: () async {
                    final uri = Uri.parse(
                      'https://www.google.com/maps/search/?api=1&query=${p.latitude},${p.longitude}',
                    );
                    // ignore: deprecated_member_use — share_plus already used; url_launcher preferred
                    await launchUrl(uri, mode: LaunchMode.externalApplication);
                  },
                  icon: const Icon(Icons.map_outlined, size: 18),
                  label: const Text('View on Google Maps'),
                  style: TextButton.styleFrom(
                    padding: EdgeInsets.zero,
                    visualDensity: VisualDensity.compact,
                  ),
                ),
                if (p.batteryLevel != null)
                  _infoRow('Battery', '${p.batteryLevel!.toStringAsFixed(0)}%'),
                if (p.satellites != null)
                  _infoRow('Accuracy', '${p.satellites} satellites'),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _infoRow(String label, String value, {bool multiline = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment:
            multiline ? CrossAxisAlignment.start : CrossAxisAlignment.center,
        children: [
          SizedBox(
            width: 90,
            child:
                Text(label, style: const TextStyle(color: AppColors.textMuted)),
          ),
          Expanded(
            child: Text(
              value,
              maxLines: multiline ? 4 : 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPlaybackChromeHeader(HistoryReplayBundle b) {
    final hidden = _playing && !_chromeVisible;
    final vehicleAsync = ref.watch(vehicleDetailProvider(widget.vehicleId));
    final name = b.vehicle?.vehicleName ??
        vehicleAsync.valueOrNull?.name ??
        'Vehicle #${widget.vehicleId}';
    final plate =
        b.vehicle?.plateNumber ?? vehicleAsync.valueOrNull?.registrationNumber;
    final metrics = _tripMetrics;
    final dateLabel = DateFormat('dd MMM').format(metrics.startTime.toLocal());
    final canExport = _canExportReports;

    return AnimatedOpacity(
      opacity: hidden ? 0 : 1,
      duration: const Duration(milliseconds: 220),
      child: IgnorePointer(
        ignoring: hidden,
        child: SafeArea(
          bottom: false,
          child: PlaybackHeader(
            onBack: () => unawaited(_backToTripList()),
            vehicleName: name,
            plateNumber: plate,
            dateLabel: dateLabel,
            onDateTap: () {
              _bumpChromeInteraction();
              if (_playingFullRange) {
                unawaited(_pickCustomRange());
              } else {
                unawaited(_backToTripList());
              }
            },
            moreButton: PopupMenuButton<String>(
              tooltip: 'More actions',
              icon: const Icon(Icons.more_vert_rounded),
              onSelected: (selected) {
                switch (selected) {
                  case 'stats':
                    _showAnalyticsSheet();
                  case 'stopsList':
                    _showStopsSheet();
                  case 'stops':
                    _toggleIncludeStops(!_showStopMarkers);
                  case 'trips':
                    unawaited(_backToTripList());
                  case 'full':
                    unawaited(_loadFullRange());
                  case 'text':
                    _shareSummary();
                  case 'report':
                    unawaited(_shareTripReport());
                  case 'gpx':
                    unawaited(_shareGpx());
                  case 'kml':
                    unawaited(_shareKml());
                  case 'csv':
                    unawaited(_shareCsv());
                  case 'coords':
                    _copyCoords();
                }
              },
              itemBuilder: (_) => [
                const PopupMenuItem(
                  value: 'stats',
                  child: Text('Trip statistics'),
                ),
                if (_allStops.isNotEmpty)
                  const PopupMenuItem(
                    value: 'stopsList',
                    child: Text('Stops & parking'),
                  ),
                PopupMenuItem(
                  value: 'stops',
                  child: Text(
                    _showStopMarkers
                        ? 'Hide stop markers'
                        : 'Show stop markers',
                  ),
                ),
                const PopupMenuItem(
                  value: 'trips',
                  child: Text('Back to trip list'),
                ),
                const PopupMenuItem(
                  value: 'full',
                  child: Text('Play full range'),
                ),
                const PopupMenuItem(
                  value: 'text',
                  child: Text('Share summary'),
                ),
                if (canExport) ...[
                  const PopupMenuItem(
                    value: 'report',
                    child: Text('Share trip report'),
                  ),
                  const PopupMenuItem(
                    value: 'gpx',
                    child: Text('Export GPX'),
                  ),
                  const PopupMenuItem(
                    value: 'kml',
                    child: Text('Export KML'),
                  ),
                  const PopupMenuItem(
                    value: 'csv',
                    child: Text('Export CSV'),
                  ),
                ],
                const PopupMenuItem(
                  value: 'coords',
                  child: Text('Copy coordinates'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLiveSpeedBadge({
    required HistoryReplayPoint? current,
    required double distSoFar,
  }) {
    final hidden = _playing && !_chromeVisible;
    final p = Theme.of(context).extension<SgPalette>() ?? SgPalette.light;
    return AnimatedOpacity(
      opacity: hidden ? 0 : 1,
      duration: const Duration(milliseconds: 220),
      child: IgnorePointer(
        ignoring: hidden,
        child: Material(
          color: p.card.withValues(alpha: 0.96),
          elevation: 2,
          borderRadius: BorderRadius.circular(AppRadii.md),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  formatOptionalSpeedKmh(current?.speedKmh),
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 13,
                    color: p.textPrimary,
                  ),
                ),
                Text(
                  '${_speed}x · ${distSoFar.toStringAsFixed(1)} km',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w600,
                    color: p.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _toggleSheetExpanded() {
    _bumpChromeInteraction();
    if (!_sheetController.isAttached) return;
    final target = _sheetExtent < 0.36 ? 0.62 : 0.26;
    unawaited(
      _sheetController.animateTo(
        target,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOutCubic,
      ),
    );
  }

  void _onEventFilterSelected(TripEventFilter f) {
    _bumpChromeInteraction();
    setState(() {
      _eventFilter = f;
      if (f == TripEventFilter.stops) {
        _showStopMarkers = true;
      }
      _rebuildMapLayers(full: true);
    });
  }

  Widget _buildBottomSheetContent({
    required ScrollController scrollController,
    required List<HistoryReplayPoint> points,
    required HistoryReplayPoint? current,
    required HistoryReplayBundle? bundle,
    required double distSoFar,
  }) {
    final p = context.sg;
    return NotificationListener<DraggableScrollableNotification>(
      onNotification: (notification) {
        final next = notification.extent;
        if ((next - _sheetExtent).abs() > 0.004) {
          setState(() => _sheetExtent = next);
        }
        return false;
      },
      child: AnimatedOpacity(
        opacity: _chromeVisible || !_playing ? 1 : 0.92,
        duration: const Duration(milliseconds: 220),
        child: Material(
          color: p.card,
          elevation: 12,
          shadowColor: const Color(0x33000000),
          borderRadius: const BorderRadius.vertical(top: Radius.circular(22)),
          clipBehavior: Clip.antiAlias,
          child: SafeArea(
            top: false,
            child: Listener(
              onPointerDown: (_) => _bumpChromeInteraction(),
              child: ListView(
                controller: scrollController,
                padding: const EdgeInsets.fromLTRB(14, 8, 14, 16),
                children: _bottomPanelChildren(
                  points: points,
                  current: current,
                  bundle: bundle,
                  distSoFar: distSoFar,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  List<Widget> _bottomPanelChildren({
    required List<HistoryReplayPoint> points,
    required HistoryReplayPoint? current,
    required HistoryReplayBundle? bundle,
    required double distSoFar,
  }) {
    final p = context.sg;
    final metrics = _tripMetrics;
    final expanded = _sheetExtent > 0.36;
    final tripIdx = _selectedTripIndex;
    final nav = _navigableTrips;
    final clock = _virtualTime ?? current?.timestamp;

    return [
      GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: _toggleSheetExpanded,
        child: Column(
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: p.border,
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: Text(
                    () {
                      final tripLabel = _tripWindowLabel();
                      if (tripLabel.isNotEmpty) return tripLabel;
                      return _playing
                          ? 'Playing'
                          : (_index >= points.length - 1
                              ? 'Finished'
                              : 'Paused');
                    }(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 13,
                      color: p.textPrimary,
                    ),
                  ),
                ),
                if (clock != null)
                  Text(
                    formatPlaybackClock(clock),
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
                      color: p.textSecondary,
                    ),
                  ),
                Icon(
                  expanded ? Icons.expand_more : Icons.expand_less,
                  size: 20,
                  color: p.textSecondary,
                ),
              ],
            ),
          ],
        ),
      ),
      const SizedBox(height: 10),
      Row(
        children: [
          Expanded(
            child: _metricChip(context, 'Distance', metrics.distanceLabel),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: _metricChip(context, 'Moving', metrics.movingLabel),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: _metricChip(context, 'Max', metrics.maxSpeedLabel),
          ),
        ],
      ),
      const SizedBox(height: 10),
      PlaybackTimeline(
        playback: points,
        stops: bundle?.stops ?? const [],
        events: bundle?.events ?? const [],
        filteredEvents: _filteredEvents,
        index: _index,
        virtualTime: _virtualTime,
        onIndexChanged: (i) {
          _bumpChromeInteraction();
          _setIndex(i);
        },
        onVirtualTimeChanged: _seekVirtualTime,
        onScrubStart: _stopPlay,
        onScrubEnd: _bumpChromeInteraction,
      ),
      const SizedBox(height: 4),
      PlaybackControls(
        playing: _playing,
        speed: _speed,
        atEnd: !_playing && points.isNotEmpty && _index >= points.length - 1,
        onPlayPause: _togglePlay,
        onStop: () {
          _stopPlay();
          _setIndex(0);
        },
        onFirst: () => _setIndex(0),
        onPrevPoint: () => _setIndex(_index - 1),
        onNextPoint: () => _setIndex(_index + 1),
        onPrevEvent: () => _jumpEvent(-1),
        onNextEvent: () => _jumpEvent(1),
        onEnd: () => _setIndex(points.length - 1),
        canPrevTrip: tripIdx > 0,
        canNextTrip: tripIdx >= 0 && tripIdx < nav.length - 1,
        onPrevTrip: () => unawaited(_goAdjacentTrip(-1)),
        onNextTrip: () => unawaited(_goAdjacentTrip(1)),
        onSpeed: (s) {
          _bumpChromeInteraction();
          _playbackController.setSpeed(s);
        },
      ),
      const SizedBox(height: 8),
      PlaybackScrubFooter(
        time: clock ?? DateTime.now(),
        speedKmh: current?.speedKmh ?? 0,
        distanceKm: distSoFar,
        playbackSpeed: _speed,
      ),
      if (expanded) ...[
        const SizedBox(height: 14),
        Text(
          'Event filters',
          style: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 12,
            color: p.textSecondary,
          ),
        ),
        const SizedBox(height: 8),
        PlaybackEventFilters(
          selected: _eventFilter,
          onSelected: _onEventFilterSelected,
        ),
        const SizedBox(height: 14),
        if (bundle != null)
          PlaybackSummaryCard(
            bundle: bundle,
            selectedTrip: _selectedTrip ?? _tripDetail?.trip,
            stops: _allStops,
          ),
        const SizedBox(height: 14),
        Row(
          children: [
            Text(
              'Event timeline',
              style: TextStyle(
                fontWeight: FontWeight.w800,
                fontSize: 13,
                color: p.textPrimary,
              ),
            ),
            const Spacer(),
            TextButton(
              onPressed: () => _showStopsSheet(bundle),
              child: Text('${metrics.stopCount} stops'),
            ),
          ],
        ),
        PlaybackEventList(
          playback: points,
          stops: _eventFilter == TripEventFilter.stops ||
                  _eventFilter == TripEventFilter.all
              ? _allStops
              : const [],
          events: _filteredEvents,
          onJump: (i) {
            _bumpChromeInteraction();
            _setIndex(i);
          },
          vertical: true,
          maxItems: 40,
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: Text(
                _follow ? 'Follow vehicle' : 'Free pan',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: p.textPrimary,
                ),
              ),
            ),
            Switch.adaptive(
              value: _follow,
              onChanged: (v) {
                _bumpChromeInteraction();
                setState(() => _follow = v);
              },
            ),
          ],
        ),
        if (current != null) ...[
          _infoRow('Heading', headingToCardinal(current.heading)),
          _infoRow('Address', playbackAddressLine(current)),
          _infoRow(
            'Avg / Max',
            '${metrics.avgSpeedLabel} / ${metrics.maxSpeedLabel}',
          ),
        ],
      ],
    ];
  }

  Widget _metricChip(BuildContext context, String label, String value) {
    final p = context.sg;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
      decoration: BoxDecoration(
        color: p.chipBg,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: p.border),
      ),
      child: Column(
        children: [
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 13,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w600,
              color: p.textMuted,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTripListBody() {
    final vehicleAsync = ref.watch(vehicleDetailProvider(widget.vehicleId));
    final vehicleName = vehicleAsync.valueOrNull?.name ??
        _trips.firstOrNull?.vehicleName ??
        'Vehicle #${widget.vehicleId}';
    final plate = vehicleAsync.valueOrNull?.registrationNumber ??
        _trips.firstOrNull?.plateNumber ??
        '';
    final visibleTrips = filterTrips(_trips, _tripFilter);
    final rangeSummary = tripRangeSummaryLabel(_trips);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
          child: _DateBar(
            from: _from,
            to: _to,
            preset: _preset,
            presetLabel: _presetLabel(_preset),
            onPreset: (p) {
              if (p == _HistoryPreset.custom) {
                unawaited(_pickCustomRange());
              } else {
                _applyPreset(p);
              }
            },
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 10, 12, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SegmentedButton<TripListFilter>(
                segments: [
                  ButtonSegment(
                    value: TripListFilter.moving,
                    label: const Text('Motion'),
                    tooltip: tripFilterTooltip(TripListFilter.moving),
                  ),
                  ButtonSegment(
                    value: TripListFilter.stops,
                    label: const Text('Stops'),
                    tooltip: tripFilterTooltip(TripListFilter.stops),
                  ),
                  ButtonSegment(
                    value: TripListFilter.all,
                    label: const Text('All'),
                    tooltip: tripFilterTooltip(TripListFilter.all),
                  ),
                ],
                selected: {_tripFilter},
                onSelectionChanged: (next) {
                  if (next.isEmpty) return;
                  setState(() => _tripFilter = next.first);
                },
                style: ButtonStyle(
                  visualDensity: VisualDensity.compact,
                  textStyle: WidgetStateProperty.all(
                    const TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                  ),
                  foregroundColor: WidgetStateProperty.resolveWith((states) {
                    if (states.contains(WidgetState.selected)) {
                      return Colors.white;
                    }
                    return AppColors.textPrimary;
                  }),
                  backgroundColor: WidgetStateProperty.resolveWith((states) {
                    if (states.contains(WidgetState.selected)) {
                      return AppColors.primary;
                    }
                    return Colors.white;
                  }),
                  side: WidgetStateProperty.resolveWith((states) {
                    if (states.contains(WidgetState.selected)) {
                      return const BorderSide(color: AppColors.primary);
                    }
                    return const BorderSide(
                        color: AppColors.border, width: 1.2);
                  }),
                ),
                showSelectedIcon: false,
              ),
              const SizedBox(height: 6),
              Text(
                tripFilterHelperText(_tripFilter),
                style: const TextStyle(
                  fontSize: 11,
                  color: AppColors.textSecondary,
                  height: 1.35,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
        if (!_loading && _trips.isNotEmpty) ...[
          if (rangeSummary.isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
              child: Container(
                width: double.infinity,
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.06),
                  borderRadius: BorderRadius.circular(AppRadii.md),
                  border: Border.all(
                    color: AppColors.primary.withValues(alpha: 0.14),
                  ),
                ),
                child: Text(
                  rangeSummary,
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                  ),
                ),
              ),
            ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 6, 16, 2),
            child: Text(
              tripFilterCountLabel(
                visible: visibleTrips.length,
                total: _trips.length,
                filter: _tripFilter,
              ),
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: AppColors.textSecondary,
              ),
            ),
          ),
        ],
        Expanded(
          child: _loading
              ? const Center(
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                      SizedBox(width: 10),
                      Text('Loading trips...'),
                    ],
                  ),
                )
              : visibleTrips.isEmpty
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              _trips.isEmpty
                                  ? 'No trips found'
                                  : _tripFilter == TripListFilter.moving
                                      ? 'No motion trips in this range'
                                      : _tripFilter == TripListFilter.stops
                                          ? 'No stops in this range'
                                          : 'No trips in this range',
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                                fontSize: 16,
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              _trips.isEmpty
                                  ? 'There are no trips for the selected period.'
                                  : 'Try All, or choose a wider date range.',
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 13,
                              ),
                            ),
                            const SizedBox(height: 16),
                            if (_trips.isEmpty)
                              OutlinedButton.icon(
                                onPressed: _loading ? null : _loadFullRange,
                                icon: const Icon(Icons.timeline_rounded),
                                label: const Text('Play full range'),
                              )
                            else
                              OutlinedButton(
                                onPressed: () => setState(
                                  () => _tripFilter = TripListFilter.all,
                                ),
                                child: const Text('Show all trips'),
                              ),
                          ],
                        ),
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        // Extra top padding so first card is not clipped under filters.
                        padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                        itemCount: visibleTrips.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 6),
                        itemBuilder: (context, i) {
                          final t = visibleTrips[i];
                          return _TripListCard(
                            trip: t,
                            vehicleName: t.vehicleName ?? vehicleName,
                            plate: t.plateNumber ?? plate,
                            // Single-vehicle history — hide redundant header chrome.
                            showVehicleHeader: false,
                            onTap: () => unawaited(_selectTrip(t)),
                          );
                        },
                      ),
                    ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final canView = ref.watch(fleetSessionProvider)?.hasPermission(
              FleetPermissions.gpsView,
            ) ??
        false;
    if (!canView) {
      return Scaffold(
        backgroundColor: AppColors.surface,
        appBar: AppBar(title: const Text('Trip history')),
        body: const SgEmptyState(
          icon: Icons.lock_outline_rounded,
          title: 'GPS access required',
          subtitle:
              'You do not have permission to view trip history or playback. Ask an admin for GPS.View.',
        ),
      );
    }

    final b = _bundle;
    final points = _playbackPoints;
    final current =
        points.isEmpty ? null : points[_index.clamp(0, points.length - 1)];
    final distSoFar = _distSoFar;

    final showContent = !_loading && _error == null && points.isNotEmpty;
    final inList = _inTripList;
    final palette = context.sg;
    return Scaffold(
      backgroundColor: inList ? palette.surface : Colors.black,
      appBar: inList
          ? AppBar(
              title: const Text('Trip history'),
              actions: [
                PopupMenuButton<String>(
                  icon: const Icon(Icons.more_vert),
                  onSelected: (selected) {
                    switch (selected) {
                      case 'full':
                        unawaited(_loadFullRange());
                    }
                  },
                  itemBuilder: (_) => const [
                    PopupMenuItem(
                      value: 'full',
                      child: Text('Play full range'),
                    ),
                  ],
                ),
                IconButton(
                  tooltip: 'Refresh',
                  icon: RotationTransition(
                    turns: _refreshSpinController,
                    child: const Icon(Icons.refresh_rounded),
                  ),
                  onPressed: _loading ? null : () => unawaited(_load()),
                ),
              ],
            )
          : null,
      body: _error != null
          ? (_error!.toLowerCase().contains('permission')
              ? SgEmptyState(
                  icon: Icons.lock_outline_rounded,
                  title: 'Access denied',
                  subtitle: _error,
                  actionLabel: _loading ? null : 'Retry',
                  onAction: _loading
                      ? null
                      : () => unawaited(inList ? _load() : _backToTripList()),
                )
              : Center(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error!, textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        FilledButton.icon(
                          onPressed: _loading
                              ? null
                              : () => unawaited(
                                    inList ? _load() : _backToTripList(),
                                  ),
                          icon: const Icon(Icons.refresh_rounded),
                          label: const Text('Retry'),
                        ),
                      ],
                    ),
                  ),
                ))
          : inList
              ? _buildTripListBody()
              : points.isEmpty && _loading
                  ? const Center(
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
                          SizedBox(width: 10),
                          Text('Loading trip...'),
                        ],
                      ),
                    )
                  : points.isEmpty && !_loading
                      ? Center(
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 20),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Text(
                                  'No GPS points for this trip.',
                                  textAlign: TextAlign.center,
                                ),
                                const SizedBox(height: 12),
                                FilledButton(
                                  onPressed: () => unawaited(_backToTripList()),
                                  child: const Text('Back to trips'),
                                ),
                              ],
                            ),
                          ),
                        )
                      : LayoutBuilder(
                          builder: (context, constraints) {
                            final fabBottom =
                                constraints.maxHeight * _sheetExtent + 12;
                            return Stack(
                              key: _mapStackKey,
                              children: [
                                Positioned.fill(
                                  child: GoogleMap(
                                    key: ValueKey(
                                      '${widget.vehicleId}_${_selectedTrip?.tripKey ?? _from.millisecondsSinceEpoch}_$_playingFullRange',
                                    ),
                                    mapType: _mapType,
                                    initialCameraPosition: CameraPosition(
                                      target: LatLng(
                                        points.first.latitude,
                                        points.first.longitude,
                                      ),
                                      zoom: 13,
                                    ),
                                    polylines: _polylines,
                                    markers: _markers,
                                    myLocationButtonEnabled: false,
                                    zoomControlsEnabled: false,
                                    zoomGesturesEnabled: true,
                                    scrollGesturesEnabled: true,
                                    rotateGesturesEnabled: true,
                                    tiltGesturesEnabled: true,
                                    mapToolbarEnabled: false,
                                    onMapCreated: (c) {
                                      _map = c;
                                      _pulseScreenPos = null;
                                      _didInitialCameraFocus = false;
                                      _maybeFocusPlaybackStart();
                                      _schedulePulseSync();
                                    },
                                    onCameraMove: (_) => _schedulePulseSync(),
                                    onCameraIdle: () {
                                      _schedulePulseSync();
                                    },
                                    onTap: _onMapTap,
                                  ),
                                ),
                                if (_displayPosition != null && current != null)
                                  Positioned.fill(
                                    child: PulseVehicleOverlay(
                                      screenPosition: _pulseScreenPos,
                                      status: _statusForPoint(current),
                                      headingDegrees: _displayHeading ??
                                          current.heading ??
                                          0,
                                      visible: true,
                                      showDisc: false,
                                    ),
                                  ),
                                if (b != null)
                                  Positioned(
                                    left: 10,
                                    right: 10,
                                    top: 0,
                                    child: _buildPlaybackChromeHeader(b),
                                  ),
                                if (b != null)
                                  Positioned(
                                    right: 12,
                                    top: MediaQuery.paddingOf(context).top + 64,
                                    child: _buildLiveSpeedBadge(
                                      current: current,
                                      distSoFar: distSoFar,
                                    ),
                                  ),
                                Positioned(
                                  left: 12,
                                  bottom: fabBottom,
                                  child: AnimatedOpacity(
                                    opacity: _chromeVisible ? 1 : 0,
                                    duration: const Duration(milliseconds: 220),
                                    child: IgnorePointer(
                                      ignoring: !_chromeVisible,
                                      child: const PlaybackLegend(),
                                    ),
                                  ),
                                ),
                                if (showContent)
                                  DraggableScrollableSheet(
                                    controller: _sheetController,
                                    initialChildSize: 0.28,
                                    minChildSize: 0.24,
                                    maxChildSize: 0.78,
                                    snap: true,
                                    snapSizes: const [0.28, 0.48, 0.72],
                                    builder: (context, scrollController) {
                                      return _buildBottomSheetContent(
                                        scrollController: scrollController,
                                        points: points,
                                        current: current,
                                        bundle: b,
                                        distSoFar: distSoFar,
                                      );
                                    },
                                  ),
                                // FABs sit above the sheet so controls receive taps.
                                PlaybackMapFabs(
                                  bottom: fabBottom,
                                  visible: _chromeVisible || !_playing,
                                  mapType: _mapType,
                                  onZoomIn: () => unawaited(_zoomBy(1)),
                                  onZoomOut: () => unawaited(_zoomBy(-1)),
                                  onCenter: () => unawaited(_centerOnVehicle()),
                                  onMapType: (t) {
                                    _bumpChromeInteraction();
                                    setState(() => _mapType = t);
                                  },
                                  onFitRoute: () {
                                    _bumpChromeInteraction();
                                    unawaited(_fitRoute());
                                  },
                                ),
                                if (_loading)
                                  Center(
                                    child: Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 14,
                                        vertical: 10,
                                      ),
                                      decoration: BoxDecoration(
                                        color: palette.card
                                            .withValues(alpha: 0.94),
                                        borderRadius: BorderRadius.circular(12),
                                        border:
                                            Border.all(color: palette.border),
                                      ),
                                      child: Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          const SizedBox(
                                            width: 16,
                                            height: 16,
                                            child: CircularProgressIndicator(
                                              strokeWidth: 2,
                                            ),
                                          ),
                                          const SizedBox(width: 10),
                                          Text(
                                            'Loading…',
                                            style: TextStyle(
                                              color: palette.textPrimary,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                              ],
                            );
                          },
                        ),
    );
  }
}

class _DateBar extends StatelessWidget {
  const _DateBar({
    required this.from,
    required this.to,
    required this.preset,
    required this.presetLabel,
    required this.onPreset,
  });

  final DateTime from;
  final DateTime to;
  final _HistoryPreset preset;
  final String presetLabel;
  final ValueChanged<_HistoryPreset> onPreset;

  @override
  Widget build(BuildContext context) {
    // Compact 12h strings reduce AM/PM wrap on narrow phones.
    final rangeFmt = DateFormat('dd MMM, h:mma');
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(8, 4, 4, 4),
        child: Row(
          children: [
            PopupMenuButton<_HistoryPreset>(
              onSelected: onPreset,
              itemBuilder: (_) => const [
                PopupMenuItem(
                    value: _HistoryPreset.today, child: Text('Today')),
                PopupMenuItem(
                  value: _HistoryPreset.yesterday,
                  child: Text('Yesterday'),
                ),
                PopupMenuItem(
                  value: _HistoryPreset.hours24,
                  child: Text('Last 24 hours'),
                ),
                PopupMenuItem(
                  value: _HistoryPreset.days3,
                  child: Text('Last 3 days'),
                ),
                PopupMenuItem(
                  value: _HistoryPreset.days7,
                  child: Text('Last 7 days'),
                ),
                PopupMenuItem(
                  value: _HistoryPreset.hours6,
                  child: Text('Last 6 hours'),
                ),
                PopupMenuItem(
                  value: _HistoryPreset.custom,
                  child: Text('Custom range'),
                ),
              ],
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppRadii.md),
                  border: Border.all(color: AppColors.border),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.calendar_today_outlined, size: 16),
                    const SizedBox(width: 6),
                    Text(
                      presetLabel,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                    const SizedBox(width: 2),
                    const Icon(Icons.expand_more, size: 18),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                '${rangeFmt.format(from.toLocal())} → ${rangeFmt.format(to.toLocal())}',
                textAlign: TextAlign.end,
                maxLines: 1,
                softWrap: false,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TripListCard extends StatelessWidget {
  const _TripListCard({
    required this.trip,
    required this.vehicleName,
    required this.plate,
    required this.onTap,
    this.showVehicleHeader = true,
  });

  final GpsTrip trip;
  final String vehicleName;
  final String plate;
  final VoidCallback onTap;

  /// When false (single-vehicle history), skip redundant plate/name header.
  final bool showVehicleHeader;

  @override
  Widget build(BuildContext context) {
    final start = trip.startTime.toLocal();
    final end = trip.endTime.toLocal();
    final sameDay = start.year == end.year &&
        start.month == end.month &&
        start.day == end.day;
    final dayFmt = DateFormat('dd MMM');
    // Compact AM/PM (no space) to avoid wrap on small phones.
    final timeFmt = DateFormat('h:mma');
    final timeLabel = sameDay
        ? '${dayFmt.format(start)} · ${timeFmt.format(start)}–${timeFmt.format(end)}'
        : '${dayFmt.format(start)} ${timeFmt.format(start)} → ${dayFmt.format(end)} ${timeFmt.format(end)}';

    final route = formatTripRoute(trip);
    final stop = isStopTrip(trip);
    final status = tripStatusLabel(trip);
    final statusColor =
        stop ? const Color(0xFFF59E0B) : const Color(0xFF16A34A);
    final idLabel = tripIdLabel(trip);
    final plateText = plate.trim();
    final nameText = vehicleName.trim();

    return SgCard(
      onTap: onTap,
      padding: const EdgeInsets.fromLTRB(12, 10, 10, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: showVehicleHeader
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(Icons.directions_car_filled_rounded,
                              size: 20, color: AppColors.primary),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  plateText.isNotEmpty ? plateText : nameText,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                    fontSize: 15,
                                  ),
                                ),
                                if (plateText.isNotEmpty &&
                                    nameText.isNotEmpty &&
                                    nameText.toLowerCase() !=
                                        plateText.toLowerCase())
                                  Text(
                                    nameText,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textSecondary,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      )
                    : Text(
                        timeLabel,
                        maxLines: 1,
                        softWrap: false,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 14,
                        ),
                      ),
              ),
              const SizedBox(width: 8),
              StatusBadge(
                status,
                color: statusColor,
                radius: AppRadii.md,
              ),
              const SizedBox(width: 2),
              const Icon(
                Icons.chevron_right,
                color: AppColors.textMuted,
                size: 20,
              ),
            ],
          ),
          if (idLabel.isNotEmpty) ...[
            const SizedBox(height: 2),
            Text(
              idLabel,
              style: const TextStyle(
                fontSize: 10,
                color: AppColors.textMuted,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          if (showVehicleHeader) ...[
            const SizedBox(height: 8),
            Text(
              timeLabel,
              maxLines: 1,
              softWrap: false,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 13,
              ),
            ),
          ],
          const SizedBox(height: 4),
          Text(
            route.primary,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 13,
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w600,
            ),
          ),
          if ((route.secondary ?? '').isNotEmpty) ...[
            const SizedBox(height: 2),
            Text(
              route.secondary!,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: route.addressMissing
                    ? const Color(0xFFB45309)
                    : AppColors.textMuted,
              ),
            ),
          ],
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: _TripStat(
                  label: 'Distance',
                  value: '${trip.distanceKm.toStringAsFixed(1)} km',
                ),
              ),
              Expanded(
                child: _TripStat(
                  label: 'Duration',
                  value: '${trip.durationMinutes} min',
                ),
              ),
              Expanded(
                child: _TripStat(
                  label: 'Avg',
                  value: formatTripAvgSpeed(trip),
                ),
              ),
              Expanded(
                child: _TripStat(
                  label: 'Max',
                  value: formatTripMaxSpeed(trip),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TripStat extends StatelessWidget {
  const _TripStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Text(
          label,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 10,
            color: AppColors.textMuted,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          textAlign: TextAlign.center,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 13,
          ),
        ),
      ],
    );
  }
}
