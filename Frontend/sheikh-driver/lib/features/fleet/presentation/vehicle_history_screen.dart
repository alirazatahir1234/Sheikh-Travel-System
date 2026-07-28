import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import 'package:share_plus/share_plus.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import 'playback/playback_controls.dart';
import 'playback/playback_helpers.dart';
import 'playback/playback_legend.dart';
import 'playback/playback_map_fabs.dart';
import 'playback/playback_map_builder.dart';
import 'playback/playback_scrub_footer.dart';
import 'playback/playback_summary_card.dart';
import 'playback/playback_timeline.dart';

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
  const VehicleHistoryScreen({super.key, required this.vehicleId});
  final int vehicleId;

  @override
  ConsumerState<VehicleHistoryScreen> createState() =>
      _VehicleHistoryScreenState();
}

class _VehicleHistoryScreenState extends ConsumerState<VehicleHistoryScreen> {
  GoogleMapController? _map;
  bool _loading = true;
  String? _error;
  HistoryReplayBundle? _bundle;
  int _index = 0;
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
  Timer? _playTimer;
  Set<Polyline> _polylines = {};
  Set<Marker> _markers = {};

  _HistoryPreset _preset = _HistoryPreset.hours6;
  late DateTime _from;
  late DateTime _to;

  final _df = DateFormat('dd MMM yyyy');
  final _tf = DateFormat('dd MMM, HH:mm');

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
    _applyPreset(_HistoryPreset.hours6, reload: false);
    unawaited(_loadVehicleIcon());
    _load();
  }

  Future<void> _loadVehicleIcon() async {
    final icon = await PlaybackMapAssets.vehicleIcon();
    if (mounted) setState(() => _vehicleIcon = icon);
  }

  @override
  void dispose() {
    _playTimer?.cancel();
    _sheetController.dispose();
    _map?.dispose();
    super.dispose();
  }

  List<TripEvent> get _filteredEvents {
    final b = _bundle;
    if (b == null) return const [];
    return filterEvents(b.events, _eventFilter);
  }

  List<int> get _eventIndices {
    final b = _bundle;
    if (b == null || b.points.isEmpty) return const [];
    return eventMarkerIndices(b.points, _filteredEvents);
  }

  void _rebuildMapLayers() {
    final b = _bundle;
    if (b == null || _vehicleIcon == null) return;
    final trail = b.trailPoints;
    final playback = b.points;
    final trailIdx = trailIndexForPlaybackIndex(trail, playback, _index);
    final polylines = buildPlaybackPolylines(
      trail: trail,
      trailIndex: trailIdx,
      stops: b.stops,
    );
    setState(() => _polylines = polylines);
    unawaited(
      buildPlaybackMarkers(
        trail: trail,
        playback: playback,
        playbackIndex: _index,
        stops: b.stops,
        events: _filteredEvents,
        vehicleIcon: _vehicleIcon!,
      ).then((m) {
        if (mounted) setState(() => _markers = m);
      }),
    );
  }

  void _stopPlay() {
    _playTimer?.cancel();
    _playTimer = null;
    if (_playing) setState(() => _playing = false);
  }

  void _togglePlay() {
    final points = _bundle?.points ?? const [];
    if (_playing) {
      _stopPlay();
      return;
    }
    if (points.isEmpty) return;
    setState(() => _playing = true);
    final ms = (400 / _speed).round().clamp(50, 2000);
    _playTimer = Timer.periodic(Duration(milliseconds: ms), (_) {
      if (!mounted || !_playing) return;
      final len = points.length;
      if (len == 0 || _index >= len - 1) {
        _stopPlay();
        return;
      }
      setState(() {
        _index++;
        _rebuildMapLayers();
      });
      _maybeFollowCamera(points[_index]);
    });
  }

  void _maybeFollowCamera(HistoryReplayPoint p) {
    if (!_follow) return;
    unawaited(
      _animateMap(
        CameraUpdate.newLatLngZoom(LatLng(p.latitude, p.longitude), 15),
      ),
    );
  }

  void _setIndex(int idx) {
    final points = _bundle?.points ?? const [];
    if (points.isEmpty) return;
    _stopPlay();
    setState(() {
      _index = idx.clamp(0, points.length - 1);
      _rebuildMapLayers();
    });
    _maybeFollowCamera(points[_index]);
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
    });
    if (reload) _load();
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
    });
    await _load();
  }

  Future<void> _load() async {
    _stopPlay();
    _map = null;
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final bundle = await ref.read(fleetApiProvider).getHistoryReplay(
            widget.vehicleId,
            from: _from.toUtc(),
            to: _to.toUtc(),
          );
      if (!mounted) return;
      setState(() {
        _bundle = bundle;
        _index = 0;
        _loading = false;
      });
      if (_vehicleIcon != null) {
        _rebuildMapLayers();
        setState(() {});
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = _friendlyError(e);
      });
    }
  }

  Future<void> _animateMap(CameraUpdate update) async {
    final map = _map;
    if (map == null || !mounted) return;
    try {
      await map.animateCamera(update);
    } catch (_) {}
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
          14,
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

  void _copyCoords() {
    final p = _bundle?.points.isNotEmpty == true
        ? _bundle!.points[_index.clamp(0, _bundle!.points.length - 1)]
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
                  title: Text('${s.durationMinutes} min'),
                  subtitle: Text(s.address ?? '${s.latitude}, ${s.longitude}'),
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

  void _showVehicleSheet() {
    final b = _bundle;
    final p = b?.points.isNotEmpty == true
        ? b!.points[_index.clamp(0, b.points.length - 1)]
        : null;
    if (b == null || p == null) return;
    final v = b.vehicle;
    showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              v?.vehicleName ?? 'Vehicle #${widget.vehicleId}',
              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
            ),
            if (v?.plateNumber != null)
              Text(v!.plateNumber!, style: const TextStyle(color: AppColors.textSecondary)),
            const SizedBox(height: 12),
            _infoRow('Speed', '${p.speedKmh.toStringAsFixed(0)} km/h'),
            _infoRow('Heading', headingToCardinal(p.heading)),
            _infoRow(
              'Ignition',
              p.ignition == null ? '—' : (p.ignition! ? 'ON' : 'OFF'),
            ),
            _infoRow('Address', p.address ?? '—'),
            if (p.batteryLevel != null)
              _infoRow('Battery', '${p.batteryLevel!.toStringAsFixed(0)}%'),
            _infoRow('Time', _tf.format(p.timestamp.toLocal())),
          ],
        ),
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          SizedBox(
            width: 90,
            child: Text(label, style: const TextStyle(color: AppColors.textMuted)),
          ),
          Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600))),
        ],
      ),
    );
  }

  Widget _kpiTile(String value, String label) {
    return Expanded(
      child: Column(
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
      ),
    );
  }

  Widget _buildTopOverlay(HistoryReplayBundle b, double distSoFar) {
    final current = b.points.isEmpty
        ? null
        : b.points[_index.clamp(0, b.points.length - 1)];
    return Column(
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
            if (p == _HistoryPreset.custom) {
              _pickCustomRange();
            } else {
              _applyPreset(p);
            }
          },
          onFitRoute: _fitRoute,
        ),
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
              _kpiTile('${distSoFar.toStringAsFixed(1)} km', 'Distance'),
              _kpiTile(
                '${b.summary?.drivingMinutes ?? b.statistics?.drivingMinutes ?? 0}m',
                'Driving',
              ),
              _kpiTile('${b.stops.length}', 'Stops'),
              _kpiTile(
                '${current?.speedKmh.toStringAsFixed(0) ?? '0'} km/h',
                'Speed',
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
                label: Text(_filterLabel(f), style: const TextStyle(fontSize: 12)),
                selected: selected,
                onSelected: (_) {
                  setState(() {
                    _eventFilter = f;
                    _rebuildMapLayers();
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
      ],
    );
  }

  void _toggleSheetExpanded() {
    if (!_sheetController.isAttached) return;
    final target = _sheetExtent < 0.28 ? 0.45 : 0.14;
    unawaited(
      _sheetController.animateTo(
        target,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOutCubic,
      ),
    );
  }

  Widget _buildBottomPanel({
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
      child: DraggableScrollableSheet(
        controller: _sheetController,
        initialChildSize: 0.18,
        minChildSize: 0.14,
        maxChildSize: 0.45,
        snap: true,
        snapSizes: const [0.14, 0.18, 0.45],
        builder: (context, scrollController) {
          return Material(
            color: Colors.white,
            elevation: 10,
            shadowColor: const Color(0x33000000),
            borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
            clipBehavior: Clip.antiAlias,
            child: SafeArea(
              top: false,
              child: ListView(
                controller: scrollController,
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
                children: [
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
                            _playing ? 'Playing' : 'Finished',
                            style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: 12,
                            ),
                          ),
                        ),
                        Text(
                          current == null
                              ? '--'
                              : _tf.format(current.timestamp.toLocal()),
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 12,
                            color: AppColors.textSecondary,
                          ),
                        ),
                        Icon(
                          _sheetExtent > 0.28
                              ? Icons.expand_more
                              : Icons.expand_less,
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
                    onPlayPause: _togglePlay,
                    onFirst: () => _setIndex(0),
                    onPrevEvent: () => _jumpEvent(-1),
                    onNextEvent: () => _jumpEvent(1),
                    onEnd: () => _setIndex(points.length - 1),
                    onSpeed: (s) {
                      setState(() => _speed = s);
                      if (_playing) {
                        _stopPlay();
                        _togglePlay();
                      }
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
                              bundle.vehicle?.vehicleName ??
                                  'Vehicle #${widget.vehicleId}',
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
                    onIndexChanged: _setIndex,
                  ),
                  const SizedBox(height: 6),
                  PlaybackScrubFooter(
                    time: current?.timestamp ?? DateTime.now(),
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
                        onChanged: (v) => setState(() => _follow = v),
                      ),
                      IconButton(
                        tooltip:
                            _infoExpanded ? 'Hide details' : 'Show details',
                        onPressed: () =>
                            setState(() => _infoExpanded = !_infoExpanded),
                        icon: Icon(
                          _infoExpanded
                              ? Icons.info_outline
                              : Icons.info_outline_rounded,
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
                          _infoRow('Address', current.address ?? '—'),
                          _infoRow(
                            'Avg / Max',
                            '${bundle?.summary?.avgSpeedKmh.toStringAsFixed(0) ?? '0'} / ${bundle?.summary?.maxSpeedKmh.toStringAsFixed(0) ?? '0'} km/h',
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final b = _bundle;
    final points = b?.points ?? const <HistoryReplayPoint>[];
    final current = points.isEmpty
        ? null
        : points[_index.clamp(0, points.length - 1)];
    final trail = b?.trailPoints ?? const [];
    final distSoFar = trail.isNotEmpty
        ? distanceAlongTrailKm(
            trail,
            trailIndexForPlaybackIndex(trail, points, _index),
          )
        : 0.0;

    final showContent = !_loading && _error == null && points.isNotEmpty;
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Playback'),
        actions: [
          PopupMenuButton<String>(
            icon: const Icon(Icons.share_outlined),
            onSelected: (v) {
              switch (v) {
                case 'text':
                  _shareSummary();
                  break;
                case 'gpx':
                  _shareGpx();
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
              PopupMenuItem(value: 'gpx', child: Text('Export GPX')),
              PopupMenuItem(value: 'csv', child: Text('Export CSV')),
              PopupMenuItem(value: 'pdf', enabled: false, child: Text('PDF report (coming soon)')),
              PopupMenuItem(value: 'coords', child: Text('Copy coordinates')),
            ],
          ),
          IconButton(
            tooltip: 'More playback actions',
            icon: const Icon(Icons.more_vert),
            onPressed: () async {
              final selected = await showMenu<String>(
                context: context,
                position: const RelativeRect.fromLTRB(1000, 80, 10, 0),
                items: [
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
                ],
              );
              if (!mounted || selected == null) return;
              if (selected == 'stats') {
                _showAnalyticsSheet();
              } else if (selected == 'layers') {
                setState(() {
                  _mapType = _mapType == MapType.normal
                      ? MapType.hybrid
                      : MapType.normal;
                });
              }
            },
          ),
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: _loading ? null : _load,
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, textAlign: TextAlign.center))
              : points.isEmpty
                  ? const Center(child: Text('No GPS history in range'))
                  : Stack(
                      children: [
                        Positioned.fill(
                          child: GoogleMap(
                            key: ValueKey(
                              '${widget.vehicleId}_${_from.millisecondsSinceEpoch}',
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
                              unawaited(_fitRoute());
                            },
                            onTap: (_) => _showVehicleSheet(),
                          ),
                        ),
                        Positioned(
                          left: 12,
                          right: 12,
                          top: 8,
                          child: _buildTopOverlay(b!, distSoFar),
                        ),
                        Positioned(
                          left: 12,
                          bottom: MediaQuery.sizeOf(context).height *
                                  _sheetExtent +
                              10,
                          child: const PlaybackLegend(),
                        ),
                        Positioned(
                          right: 12,
                          bottom: MediaQuery.sizeOf(context).height *
                                  _sheetExtent +
                              10,
                          child: PlaybackMapFabs(
                            following: _follow,
                            onFitRoute: _fitRoute,
                            onToggleFollow: () =>
                                setState(() => _follow = !_follow),
                            onToggleMapType: () => setState(() {
                              _mapType = _mapType == MapType.normal
                                  ? MapType.hybrid
                                  : MapType.normal;
                            }),
                          ),
                        ),
                        if (showContent)
                          Positioned.fill(
                            child: _buildBottomPanel(
                              points: points,
                              current: current,
                              bundle: b,
                              distSoFar: distSoFar,
                            ),
                          ),
                      ],
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
    required this.onFitRoute,
  });

  final DateTime from;
  final DateTime to;
  final DateFormat df;
  final _HistoryPreset preset;
  final String presetLabel;
  final ValueChanged<_HistoryPreset> onPreset;
  final VoidCallback onFitRoute;

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
                PopupMenuItem(value: _HistoryPreset.today, child: Text('Today')),
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
