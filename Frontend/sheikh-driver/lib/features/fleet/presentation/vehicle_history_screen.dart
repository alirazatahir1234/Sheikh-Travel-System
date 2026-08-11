import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import 'playback/playback_controls.dart';
import 'playback/playback_controller.dart';
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
  });
  final int vehicleId;

  /// When set, skip the trip list and open this trip's playback.
  final String? tripKey;

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
  bool _infoExpanded = false;
  double _sheetExtent = 0.18;
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

  _HistoryPreset _preset = _HistoryPreset.hours6;
  late DateTime _from;
  late DateTime _to;

  List<GpsTrip> _trips = const [];
  GpsTrip? _selectedTrip;
  TripDetailBundle? _tripDetail;
  List<TripStop> _allStops = const [];
  bool _includeStops = false;
  bool _playingFullRange = false;
  bool _loadingTripDetail = false;

  final _df = DateFormat('dd MMM yyyy');
  final _tf = DateFormat('dd MMM, HH:mm');
  final _tripTf = DateFormat('HH:mm');

  bool get _inTripList =>
      !_playingFullRange &&
      !_loadingTripDetail &&
      _selectedTrip == null &&
      _tripDetail == null;

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
    _applyPreset(_HistoryPreset.hours24, reload: false);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _devicePixelRatio = MediaQuery.devicePixelRatioOf(context);
      unawaited(_loadVehicleIcon());
    });
    final deepLink = widget.tripKey?.trim();
    if (deepLink != null && deepLink.isNotEmpty) {
      unawaited(_openTripByKey(deepLink));
    } else {
      unawaited(_load());
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
            0.20,
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
    return filterEvents(b.events, _eventFilter);
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
    if (!full) {
      _composeMarkers(
        vehiclePoint: _playbackPoints.isEmpty
            ? null
            : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)],
      );
      setState(() {});
      return;
    }
    _composeMarkers(
      vehiclePoint: _playbackPoints.isEmpty
          ? null
          : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)],
    );
    setState(() {});
    unawaited(
      buildPlaybackStaticMarkers(
        trail: b.trailPoints,
        playback: _playbackPoints,
        stops: b.stops,
        events: _filteredEvents,
        devicePixelRatio: _devicePixelRatio,
        vehiclePoint: _playbackPoints.isEmpty
            ? null
            : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)],
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

  void _maybeFollowCamera(HistoryReplayPoint p) {
    if (!_follow) return;
    unawaited(
      _animateMap(CameraUpdate.newLatLng(LatLng(p.latitude, p.longitude))),
    );
  }

  Future<void> _zoomBy(double delta) async {
    _bumpChromeInteraction();
    await _animateMap(CameraUpdate.zoomBy(delta));
  }

  Future<void> _centerOnVehicle() async {
    _bumpChromeInteraction();
    final pos = _displayPosition ??
        (_playbackPoints.isNotEmpty
            ? LatLng(
                _playbackPoints[_index].latitude,
                _playbackPoints[_index].longitude,
              )
            : null);
    if (pos == null) return;
    await _animateMap(CameraUpdate.newLatLng(pos));
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
    _allStops = detail?.stops ?? bundle.stops;
    final filtered = HistoryReplayBundle(
      route: bundle.route,
      playback: bundle.playback,
      stops: _includeStops ? _allStops : const [],
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
    setState(() => _includeStops = value);
    final b = _bundle;
    if (b == null) return;
    setState(() {
      _bundle = HistoryReplayBundle(
        route: b.route,
        playback: b.playback,
        stops: value ? _allStops : const [],
        events: b.events,
        summary: b.summary,
        statistics: b.statistics,
        mileageKm: b.mileageKm,
        vehicle: b.vehicle,
        gpsDeviceId: b.gpsDeviceId,
      );
    });
    _rebuildMapLayers(full: true);
  }

  String _tripWindowLabel() {
    final trip = _selectedTrip ?? _tripDetail?.trip;
    if (trip != null) {
      return '${_tripTf.format(trip.startTime.toLocal())} → '
          '${_tripTf.format(trip.endTime.toLocal())} · '
          '${trip.distanceKm.toStringAsFixed(1)} km';
    }
    if (_playingFullRange) {
      return '${_tf.format(_from.toLocal())} → ${_tf.format(_to.toLocal())}';
    }
    return '';
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
    final msg = ErrorHandler.message(e);
    if (e is DioException &&
        (e.type == DioExceptionType.receiveTimeout ||
            e.type == DioExceptionType.connectionTimeout)) {
      return 'GPS history timed out. Try a shorter range, then Retry.';
    }
    return msg;
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
    if (b == null) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final gpx = buildGpx(b.trailPoints, name: name);
    await Share.share(gpx, subject: 'GPS replay $name');
  }

  Future<void> _shareCsv() async {
    final b = _bundle;
    if (b == null) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final csv = buildCsv(b.trailPoints);
    await Share.share(csv, subject: 'GPS replay CSV $name');
  }

  Future<void> _shareKml() async {
    final b = _bundle;
    if (b == null) return;
    final name = b.vehicle?.vehicleName ?? 'Vehicle ${widget.vehicleId}';
    final kml = buildKml(b.trailPoints, name: name);
    await Share.share(kml, subject: 'GPS replay KML $name');
  }

  Future<void> _shareTripReport() async {
    final b = _bundle;
    if (b == null) return;
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
    await Share.share(report.toString(), subject: 'Trip report $name');
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
    Share.share(buildTripNarrative(b));
  }

  void _showStopsSheet(HistoryReplayBundle b) {
    if (b.stops.isEmpty) {
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
          future: _resolveStopAddresses(b),
          builder: (context, snap) {
            final addresses = snap.data;
            return ListView(
              controller: scroll,
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  'Stops & parking (${b.stops.length})',
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 12),
                for (var i = 0; i < b.stops.length; i++)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: CircleAvatar(
                      backgroundColor: b.stops[i].durationMinutes >= 120
                          ? const Color(0xFF3B82F6).withValues(alpha: 0.2)
                          : const Color(0xFFF59E0B).withValues(alpha: 0.2),
                      child: Text(
                        '${i + 1}',
                        style: TextStyle(
                          color: b.stops[i].durationMinutes >= 120
                              ? const Color(0xFF1D4ED8)
                              : const Color(0xFFB45309),
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    title: Text(
                      stopPlaybackHeadlineFor(
                        durationMinutes: b.stops[i].durationMinutes,
                        address: addresses != null && i < addresses.length
                            ? addresses[i]
                            : b.stops[i].address,
                        latitude: b.stops[i].latitude,
                        longitude: b.stops[i].longitude,
                      ),
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      [
                        if (playbackAddressSecondary(
                              addresses != null && i < addresses.length
                                  ? addresses[i]
                                  : b.stops[i].address,
                            )
                            case final locality?)
                          locality,
                        '${DateFormat('h:mm a').format(b.stops[i].startTime.toLocal())}'
                        ' · ${formatDurationMinutes(b.stops[i].durationMinutes)}',
                      ].join('\n'),
                    ),
                    isThreeLine: true,
                    onTap: () {
                      Navigator.pop(ctx);
                      final idx = indexForTimestamp(
                        _playbackPoints.isNotEmpty ? _playbackPoints : b.points,
                        b.stops[i].startTime,
                      );
                      _setIndex(idx);
                      _map?.animateCamera(
                        CameraUpdate.newLatLngZoom(
                          LatLng(b.stops[i].latitude, b.stops[i].longitude),
                          16,
                        ),
                      );
                    },
                  ),
              ],
            );
          },
        ),
      ),
    );
  }

  Future<List<String>> _resolveStopAddresses(HistoryReplayBundle b) async {
    final out = <String>[];
    for (final s in b.stops) {
      final inline = s.address?.trim();
      if (inline != null &&
          inline.isNotEmpty &&
          !isCoarsePlaybackAddress(inline)) {
        out.add(inline);
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
        final resolved = await ref.read(fleetApiProvider).reverseGeocode(
              s.latitude,
              s.longitude,
              forceRefresh: isCoarsePlaybackAddress(inline),
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
            PlaybackSummaryCard(bundle: b),
            const SizedBox(height: 12),
            if (b.stops.isNotEmpty) ...[
              const SgSectionTitle('Stops'),
              for (final s in b.stops)
                ListTile(
                  title: Text(stopPlaybackHeadline(s)),
                  subtitle: Text(
                    [
                      if (playbackAddressSecondary(s.address) case final locality?)
                        locality,
                      '${formatDurationMinutes(s.durationMinutes)}'
                      ' · ${DateFormat('h:mm a').format(s.startTime.toLocal())}',
                    ].join('\n'),
                  ),
                ),
            ],
            const SizedBox(height: 8),
            SgPrimaryButton(
              label: 'Explain this trip',
              icon: Icons.auto_awesome_outlined,
              onPressed: () {
                Navigator.pop(ctx);
                _showAiNarrative();
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _showAiNarrative() async {
    final b = _bundle;
    if (b == null) return;
    try {
      final insight = await ref.read(fleetApiProvider).getReplayInsights(
            widget.vehicleId,
            from: _from.toUtc(),
            to: _to.toUtc(),
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

    // City-only lines (or missing) → ask server, force refresh when coarse/cached.
    final needsForce = isCoarsePlaybackAddress(inline) ||
        (cached != null && isCoarsePlaybackAddress(cached));
    final fallback = (inline != null && inline.isNotEmpty)
        ? inline
        : formatPlaybackCoords(p.latitude, p.longitude);
    try {
      final resolved = await ref.read(fleetApiProvider).reverseGeocode(
            p.latitude,
            p.longitude,
            forceRefresh: needsForce,
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
                _infoRow('GPS Time', _tf.format(p.timestamp.toLocal())),
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

  /// Content-only KPI cell. Call sites wrap with [Expanded] so gesture
  /// wrappers never become the parent of an [Expanded] (ParentDataWidget).
  Widget _kpiTile(String value, String label) {
    return Column(
      children: [
        Text(
          value,
          style: const TextStyle(
            color: AppColors.success,
            fontSize: 15,
            fontWeight: FontWeight.w800,
          ),
        ),
        Text(
          label,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 11,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }

  Widget _buildTopOverlay(HistoryReplayBundle b, double distSoFar) {
    final current = _playbackPoints.isEmpty
        ? null
        : _playbackPoints[_index.clamp(0, _playbackPoints.length - 1)];
    final compact = _playing && _chromeVisible;
    final hidden = _playing && !_chromeVisible;
    return AnimatedOpacity(
      opacity: hidden ? 0 : 1,
      duration: const Duration(milliseconds: 220),
      child: IgnorePointer(
        ignoring: hidden,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            _DateBar(
              from: _from,
              to: _to,
              df: _df,
              preset: _preset,
              presetLabel: _presetLabel(_preset),
              onPreset: (p) {
                _bumpChromeInteraction();
                if (p == _HistoryPreset.custom) {
                  _pickCustomRange();
                } else {
                  _applyPreset(p);
                }
              },
              onFitRoute: () {
                _bumpChromeInteraction();
                unawaited(_fitRoute());
              },
            ),
            if (!compact) ...[
              const SizedBox(height: 6),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.96),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: AppColors.border),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: _kpiTile(
                        formatDistanceKm(effectiveDistanceKm(b)),
                        'Distance',
                      ),
                    ),
                    Expanded(
                      child: _kpiTile(
                        formatDurationMinutes(
                          effectiveDrivingMinutes(b),
                        ),
                        'Moving',
                      ),
                    ),
                    Expanded(
                      child: _kpiTile(
                        formatDurationMinutes(b.statistics?.idleMinutes ?? 0),
                        'Non-moving',
                      ),
                    ),
                    Expanded(
                      child: GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onTap: () => _showStopsSheet(b),
                        child: _kpiTile('${b.stops.length}', 'Stops'),
                      ),
                    ),
                    Expanded(
                      child: _kpiTile(
                        '${current?.speedKmh.toStringAsFixed(0) ?? '0'} km/h',
                        'Speed',
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 4),
              SizedBox(
                height: 34,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: TripEventFilter.values.length,
                  separatorBuilder: (_, __) => const SizedBox(width: 6),
                  itemBuilder: (_, i) {
                    final f = TripEventFilter.values[i];
                    final selected = _eventFilter == f;
                    return FilterChip(
                      label: Text(
                        _filterLabel(f),
                        style: const TextStyle(fontSize: 12),
                      ),
                      selected: selected,
                      onSelected: (_) {
                        _bumpChromeInteraction();
                        setState(() {
                          _eventFilter = f;
                          _rebuildMapLayers(full: true);
                        });
                      },
                      visualDensity: VisualDensity.compact,
                      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      side: BorderSide(
                        color: selected ? AppColors.success : AppColors.border,
                      ),
                      selectedColor: AppColors.success.withValues(alpha: 0.15),
                      showCheckmark: false,
                    );
                  },
                ),
              ),
            ] else ...[
              const SizedBox(height: 4),
              Align(
                alignment: Alignment.centerLeft,
                child: Chip(
                  visualDensity: VisualDensity.compact,
                  label: Text(
                    '${current?.speedKmh.toStringAsFixed(0) ?? '0'} km/h · ${distSoFar.toStringAsFixed(1)} km',
                    style: const TextStyle(
                        fontSize: 12, fontWeight: FontWeight.w600),
                  ),
                  backgroundColor: Colors.white.withValues(alpha: 0.95),
                  side: const BorderSide(color: AppColors.border),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  void _toggleSheetExpanded() {
    _bumpChromeInteraction();
    if (!_sheetController.isAttached) return;
    final target = _sheetExtent < 0.28 ? 0.50 : 0.20;
    unawaited(
      _sheetController.animateTo(
        target,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOutCubic,
      ),
    );
  }

  Widget _buildBottomSheetContent({
    required ScrollController scrollController,
    required List<HistoryReplayPoint> points,
    required HistoryReplayPoint? current,
    required HistoryReplayBundle? bundle,
    required double distSoFar,
  }) {
    return NotificationListener<DraggableScrollableNotification>(
      onNotification: (notification) {
        final next = notification.extent;
        if ((next - _sheetExtent).abs() > 0.004) {
          setState(() => _sheetExtent = next);
        }
        return false;
      },
      child: AnimatedOpacity(
        opacity: _chromeVisible || !_playing ? 1 : 0.35,
        duration: const Duration(milliseconds: 220),
        child: Material(
          color: Colors.white,
          elevation: 10,
          shadowColor: const Color(0x33000000),
          borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
          clipBehavior: Clip.antiAlias,
          child: SafeArea(
            top: false,
            child: Listener(
              onPointerDown: (_) => _bumpChromeInteraction(),
              child: ListView(
                controller: scrollController,
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
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
    return [
      GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: _toggleSheetExpanded,
        child: Row(
          children: [
            Container(
              width: 38,
              height: 4,
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                () {
                  final tripLabel = _tripWindowLabel();
                  if (tripLabel.isNotEmpty) return tripLabel;
                  return _playing
                      ? 'Playing'
                      : (_index >= points.length - 1 ? 'Finished' : 'Paused');
                }(),
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 12,
                ),
              ),
            ),
            Text(
              current == null ? '--' : _tf.format(current.timestamp.toLocal()),
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 12,
                color: AppColors.textSecondary,
              ),
            ),
            Icon(
              _sheetExtent > 0.28 ? Icons.expand_more : Icons.expand_less,
              size: 18,
              color: AppColors.textSecondary,
            ),
          ],
        ),
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
        onSpeed: (s) {
          _bumpChromeInteraction();
          _playbackController.setSpeed(s);
        },
      ),
      if (bundle != null) ...[
        const SizedBox(height: 8),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 8,
          ),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            children: [
              const Icon(
                Icons.directions_car_filled_outlined,
                size: 16,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  bundle.vehicle?.vehicleName ?? 'Vehicle #${widget.vehicleId}',
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 12,
                  ),
                ),
              ),
              if ((bundle.vehicle?.plateNumber ?? '').isNotEmpty)
                Text(
                  bundle.vehicle!.plateNumber!,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w700,
                    fontSize: 11,
                  ),
                ),
            ],
          ),
        ),
      ],
      const SizedBox(height: 8),
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
      const SizedBox(height: 6),
      PlaybackScrubFooter(
        time: _virtualTime ?? current?.timestamp ?? DateTime.now(),
        speedKmh: current?.speedKmh ?? 0,
        distanceKm: distSoFar,
      ),
      const SizedBox(height: 6),
      Row(
        children: [
          Expanded(
            child: Text(
              _follow ? 'Follow vehicle' : 'Free pan',
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          Switch(
            value: _follow,
            onChanged: (v) {
              _bumpChromeInteraction();
              setState(() => _follow = v);
            },
          ),
          IconButton(
            tooltip: _infoExpanded ? 'Hide details' : 'Show details',
            onPressed: () {
              _bumpChromeInteraction();
              setState(() => _infoExpanded = !_infoExpanded);
            },
            icon: Icon(
              _infoExpanded ? Icons.info_outline : Icons.info_outline_rounded,
            ),
          ),
        ],
      ),
      if (_infoExpanded && current != null)
        Align(
          alignment: Alignment.centerLeft,
          child: Column(
            children: [
              _infoRow(
                'Heading',
                headingToCardinal(current.heading),
              ),
              _infoRow('Address', playbackAddressLine(current)),
              _infoRow(
                'Avg / Max',
                '${bundle?.summary?.avgSpeedKmh.toStringAsFixed(0) ?? '0'} / ${bundle?.summary?.maxSpeedKmh.toStringAsFixed(0) ?? '0'} km/h',
              ),
            ],
          ),
        ),
    ];
  }

  Widget _buildTripListBody() {
    final df = DateFormat('dd MMM HH:mm');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
          child: _DateBar(
            from: _from,
            to: _to,
            df: _df,
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
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
          child: Row(
            children: [
              FilterChip(
                label: const Text('Moving trips'),
                selected: !_includeStops,
                onSelected: (_) => setState(() => _includeStops = false),
              ),
              const SizedBox(width: 8),
              FilterChip(
                label: const Text('Include stops'),
                selected: _includeStops,
                onSelected: (v) => setState(() => _includeStops = v),
              ),
            ],
          ),
        ),
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
              : _trips.isEmpty
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Text(
                              'No trips in this range.',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                fontSize: 16,
                              ),
                            ),
                            const SizedBox(height: 8),
                            const Text(
                              'Try a wider date range, or use Play full range for raw GPS history.',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 13,
                              ),
                            ),
                            const SizedBox(height: 16),
                            OutlinedButton.icon(
                              onPressed: _loading ? null : _loadFullRange,
                              icon: const Icon(Icons.timeline_rounded),
                              label: const Text('Play full range'),
                            ),
                          ],
                        ),
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                        itemCount: _trips.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (context, i) {
                          final t = _trips[i];
                          return SgCard(
                            onTap: () => unawaited(_selectTrip(t)),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  '${df.format(t.startTime.toLocal())} → ${df.format(t.endTime.toLocal())}',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    fontSize: 14,
                                  ),
                                ),
                                if ((t.startAddress ?? '').isNotEmpty ||
                                    (t.endAddress ?? '').isNotEmpty) ...[
                                  const SizedBox(height: 4),
                                  Text(
                                    [
                                      if ((t.startAddress ?? '').isNotEmpty)
                                        t.startAddress!,
                                      if ((t.endAddress ?? '').isNotEmpty)
                                        '→ ${t.endAddress!}',
                                    ].join(' '),
                                    maxLines: 2,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textSecondary,
                                    ),
                                  ),
                                ],
                                const SizedBox(height: 8),
                                Wrap(
                                  spacing: 12,
                                  runSpacing: 4,
                                  children: [
                                    Text(
                                      '${t.distanceKm.toStringAsFixed(1)} km',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    Text('${t.durationMinutes} min'),
                                    Text(
                                      'avg ${t.avgSpeedKmh.toStringAsFixed(0)} · max ${t.maxSpeedKmh.toStringAsFixed(0)} km/h',
                                    ),
                                    if ((t.status ?? '').isNotEmpty)
                                      Text(t.status!),
                                  ],
                                ),
                              ],
                            ),
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
    final b = _bundle;
    final points = _playbackPoints;
    final current =
        points.isEmpty ? null : points[_index.clamp(0, points.length - 1)];
    final distSoFar = _distSoFar;

    final showContent = !_loading && _error == null && points.isNotEmpty;
    final inList = _inTripList;
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Text(inList
            ? 'Trips'
            : (_playingFullRange ? 'Full range' : 'Trip playback')),
        leading: !inList
            ? IconButton(
                icon: const Icon(Icons.arrow_back),
                tooltip: 'Back to trips',
                onPressed: () => unawaited(_backToTripList()),
              )
            : null,
        actions: [
          if (!inList)
            PopupMenuButton<String>(
              icon: const Icon(Icons.share_outlined),
              onSelected: (v) {
                switch (v) {
                  case 'text':
                    _shareSummary();
                    break;
                  case 'report':
                    _shareTripReport();
                    break;
                  case 'gpx':
                    _shareGpx();
                    break;
                  case 'kml':
                    _shareKml();
                    break;
                  case 'csv':
                    _shareCsv();
                    break;
                  case 'coords':
                    _copyCoords();
                    break;
                }
              },
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'text', child: Text('Share summary')),
                PopupMenuItem(
                    value: 'report', child: Text('Share trip report')),
                PopupMenuItem(value: 'gpx', child: Text('Export GPX')),
                PopupMenuItem(value: 'kml', child: Text('Export KML')),
                PopupMenuItem(value: 'csv', child: Text('Export Excel/CSV')),
                PopupMenuItem(value: 'coords', child: Text('Copy coordinates')),
              ],
            ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_vert),
            onSelected: (selected) {
              switch (selected) {
                case 'stats':
                  _showAnalyticsSheet();
                  break;
                case 'layers':
                  setState(() {
                    _mapType = _mapType == MapType.normal
                        ? MapType.hybrid
                        : MapType.normal;
                  });
                  break;
                case 'full':
                  unawaited(_loadFullRange());
                  break;
                case 'trips':
                  unawaited(_backToTripList());
                  break;
                case 'stops':
                  _toggleIncludeStops(!_includeStops);
                  break;
              }
            },
            itemBuilder: (_) => [
              if (!inList) ...[
                const PopupMenuItem(
                  value: 'stats',
                  child: Text('Trip statistics'),
                ),
                PopupMenuItem(
                  value: 'layers',
                  child: Text(
                    _mapType == MapType.normal
                        ? 'Map type: Satellite'
                        : 'Map type: Standard',
                  ),
                ),
                PopupMenuItem(
                  value: 'stops',
                  child: Text(
                    _includeStops
                        ? 'Hide stop markers'
                        : 'Include stop markers',
                  ),
                ),
                const PopupMenuItem(
                  value: 'trips',
                  child: Text('Back to trip list'),
                ),
              ],
              const PopupMenuItem(
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
            onPressed: _loading
                ? null
                : () => unawaited(inList
                    ? _load()
                    : (_selectedTrip != null
                        ? _selectTrip(_selectedTrip!)
                        : _loadFullRange())),
          ),
        ],
      ),
      body: _error != null
          ? Center(
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
                          : () =>
                              unawaited(inList ? _load() : _backToTripList()),
                      icon: const Icon(Icons.refresh_rounded),
                      label: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            )
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
                                constraints.maxHeight * _sheetExtent + 10;
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
                                    onMapCreated: (c) {
                                      _map = c;
                                      _pulseScreenPos = null;
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
                                    left: 12,
                                    right: 12,
                                    top: 8,
                                    child: _buildTopOverlay(b, distSoFar),
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
                                PlaybackMapFabs(
                                  bottom: fabBottom,
                                  visible: _chromeVisible,
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
                                if (showContent)
                                  DraggableScrollableSheet(
                                    controller: _sheetController,
                                    initialChildSize: 0.20,
                                    minChildSize: 0.20,
                                    maxChildSize: 0.50,
                                    snap: true,
                                    snapSizes: const [0.20, 0.50],
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
                                if (_loading)
                                  Center(
                                    child: Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 14,
                                        vertical: 10,
                                      ),
                                      decoration: BoxDecoration(
                                        color: Colors.white
                                            .withValues(alpha: 0.94),
                                        borderRadius: BorderRadius.circular(12),
                                        border:
                                            Border.all(color: AppColors.border),
                                      ),
                                      child: const Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          SizedBox(
                                            width: 16,
                                            height: 16,
                                            child: CircularProgressIndicator(
                                              strokeWidth: 2,
                                            ),
                                          ),
                                          SizedBox(width: 10),
                                          Text('Loading…'),
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

  String _filterLabel(TripEventFilter f) {
    switch (f) {
      case TripEventFilter.all:
        return 'All';
      case TripEventFilter.stops:
        return 'Stops';
      case TripEventFilter.overspeed:
        return 'Overspeed';
      case TripEventFilter.fuel:
        return 'Fuel';
      case TripEventFilter.sos:
        return 'SOS';
      case TripEventFilter.ignition:
        return 'Ignition';
      case TripEventFilter.geofence:
        return 'Geofence';
    }
  }
}

class _DateBar extends StatelessWidget {
  const _DateBar({
    required this.from,
    required this.to,
    required this.df,
    required this.preset,
    required this.presetLabel,
    required this.onPreset,
    this.onFitRoute,
  });

  final DateTime from;
  final DateTime to;
  final DateFormat df;
  final _HistoryPreset preset;
  final String presetLabel;
  final ValueChanged<_HistoryPreset> onPreset;
  final VoidCallback? onFitRoute;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 4, 12, 4),
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
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppRadii.md),
                  border: Border.all(color: AppColors.border),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.calendar_today_outlined, size: 16),
                    const SizedBox(width: 8),
                    Text(
                      presetLabel,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                    const SizedBox(width: 4),
                    const Icon(Icons.expand_more, size: 18),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                preset == _HistoryPreset.custom
                    ? '${df.format(from.toLocal())} → ${df.format(to.toLocal())}'
                    : '',
                textAlign: TextAlign.end,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(width: 4),
            if (onFitRoute != null)
              IconButton(
                tooltip: 'Focus route',
                icon: const Icon(Icons.my_location, size: 20),
                onPressed: onFitRoute,
              ),
          ],
        ),
      ),
    );
  }
}
