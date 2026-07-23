import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';

enum _HistoryPreset { hours6, hours24, days3, today, yesterday, custom }

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

  _HistoryPreset _preset = _HistoryPreset.hours6;
  late DateTime _from;
  late DateTime _to;

  final _df = DateFormat('dd MMM yyyy');
  final _tf = DateFormat('dd MMM, HH:mm');

  @override
  void initState() {
    super.initState();
    _applyPreset(_HistoryPreset.hours6, reload: false);
    _load();
  }

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
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
      case _HistoryPreset.today:
        to = now;
        from = DateTime(now.year, now.month, now.day);
      case _HistoryPreset.yesterday:
        final startToday = DateTime(now.year, now.month, now.day);
        to = startToday;
        from = startToday.subtract(const Duration(days: 1));
      case _HistoryPreset.custom:
        // Keep existing from/to when switching to custom.
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
    if (!to.isAfter(from)) {
      to = from.add(const Duration(hours: 1));
    }
    if (to.difference(from) > const Duration(days: 7)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Max range is 7 days. Narrowing to 7 days.'),
          ),
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
      await _fitRoute();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = _friendlyError(e);
      });
    }
  }

  String _friendlyError(Object e) {
    if (e is DioException &&
        (e.type == DioExceptionType.receiveTimeout ||
            e.type == DioExceptionType.connectionTimeout)) {
      return 'GPS history timed out. Try a shorter date range (e.g. Last 6 hours), then Retry.';
    }
    return ErrorHandler.message(e);
  }

  Future<void> _fitRoute() async {
    final map = _map;
    final pts = _bundle?.points ?? const [];
    if (map == null || pts.isEmpty) return;
    if (pts.length == 1) {
      await map.animateCamera(
        CameraUpdate.newLatLngZoom(
          LatLng(pts.first.latitude, pts.first.longitude),
          14,
        ),
      );
      return;
    }
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
        48,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final points = _bundle?.points ?? const <HistoryReplayPoint>[];
    final current = points.isEmpty
        ? null
        : points[_index.clamp(0, points.length - 1)];

    final polyline = Polyline(
      polylineId: const PolylineId('route'),
      color: AppColors.primary,
      width: 4,
      points: [
        for (final p in points) LatLng(p.latitude, p.longitude),
      ],
    );

    final markers = <Marker>{
      if (current != null)
        Marker(
          markerId: const MarkerId('playhead'),
          position: LatLng(current.latitude, current.longitude),
          rotation: current.heading ?? 0,
          infoWindow: InfoWindow(
            title: '${current.speedKmh.toStringAsFixed(0)} km/h',
            snippet: _tf.format(current.timestamp.toLocal()),
          ),
        ),
    };

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('History playback'),
        actions: [
          IconButton(
            tooltip: 'Custom range',
            icon: const Icon(Icons.date_range_rounded),
            onPressed: _pickCustomRange,
          ),
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: _loading ? null : _load,
          ),
        ],
      ),
      body: Column(
        children: [
          _DateFilterBar(
            preset: _preset,
            from: _from,
            to: _to,
            df: _df,
            onPreset: (p) {
              if (p == _HistoryPreset.custom) {
                _pickCustomRange();
              } else {
                _applyPreset(p);
              }
            },
            onCustom: _pickCustomRange,
          ),
          Expanded(
            child: _loading
                ? const Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircularProgressIndicator(),
                        SizedBox(height: 12),
                        Text(
                          'Loading GPS history…',
                          style: TextStyle(color: AppColors.textSecondary),
                        ),
                      ],
                    ),
                  )
                : _error != null
                    ? Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(
                                Icons.timer_off_outlined,
                                size: 40,
                                color: AppColors.warning,
                              ),
                              const SizedBox(height: 12),
                              Text(
                                _error!,
                                textAlign: TextAlign.center,
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                ),
                              ),
                              const SizedBox(height: 16),
                              FilledButton(
                                onPressed: _load,
                                child: const Text('Retry'),
                              ),
                              const SizedBox(height: 8),
                              TextButton(
                                onPressed: () =>
                                    _applyPreset(_HistoryPreset.hours6),
                                child: const Text('Use last 6 hours'),
                              ),
                            ],
                          ),
                        ),
                      )
                    : points.isEmpty
                        ? Center(
                            child: Padding(
                              padding: const EdgeInsets.all(24),
                              child: Column(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  const Text(
                                    'No GPS history in this range.',
                                    textAlign: TextAlign.center,
                                  ),
                                  const SizedBox(height: 8),
                                  TextButton(
                                    onPressed: _pickCustomRange,
                                    child: const Text('Pick another range'),
                                  ),
                                ],
                              ),
                            ),
                          )
                        : GoogleMap(
                            initialCameraPosition: CameraPosition(
                              target: LatLng(
                                points.first.latitude,
                                points.first.longitude,
                              ),
                              zoom: 13,
                            ),
                            polylines: {polyline},
                            markers: markers,
                            myLocationButtonEnabled: false,
                            onMapCreated: (c) {
                              _map = c;
                              _fitRoute();
                            },
                          ),
          ),
          if (!_loading && _error == null && points.isNotEmpty)
            Material(
              elevation: 8,
              color: Colors.white,
              child: SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        current == null
                            ? ''
                            : '${_tf.format(current.timestamp.toLocal())} · '
                                '${current.speedKmh.toStringAsFixed(0)} km/h'
                                '${_bundle?.mileageKm != null ? ' · ${_bundle!.mileageKm!.toStringAsFixed(1)} km' : ''}',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      Slider(
                        value: _index.toDouble(),
                        min: 0,
                        max: (points.length - 1).toDouble(),
                        divisions:
                            points.length > 1 ? points.length - 1 : null,
                        onChanged: (v) {
                          setState(() => _index = v.round());
                          final p = points[_index];
                          _map?.animateCamera(
                            CameraUpdate.newLatLng(
                              LatLng(p.latitude, p.longitude),
                            ),
                          );
                        },
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

class _DateFilterBar extends StatelessWidget {
  const _DateFilterBar({
    required this.preset,
    required this.from,
    required this.to,
    required this.df,
    required this.onPreset,
    required this.onCustom,
  });

  final _HistoryPreset preset;
  final DateTime from;
  final DateTime to;
  final DateFormat df;
  final ValueChanged<_HistoryPreset> onPreset;
  final VoidCallback onCustom;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            InkWell(
              onTap: onCustom,
              borderRadius: BorderRadius.circular(AppRadii.md),
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
                    const Icon(
                      Icons.calendar_month_rounded,
                      color: AppColors.primary,
                      size: 20,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        '${df.format(from.toLocal())}  →  ${df.format(to.toLocal())}',
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                        ),
                      ),
                    ),
                    const Icon(
                      Icons.edit_calendar_outlined,
                      size: 18,
                      color: AppColors.textMuted,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              height: 48,
              child: ListView(
                scrollDirection: Axis.horizontal,
                children: [
                  _chip('6h', _HistoryPreset.hours6),
                  _chip('24h', _HistoryPreset.hours24),
                  _chip('3d', _HistoryPreset.days3),
                  _chip('Today', _HistoryPreset.today),
                  _chip('Yesterday', _HistoryPreset.yesterday),
                  _chip('Custom', _HistoryPreset.custom),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _chip(String label, _HistoryPreset value) {
    final selected = preset == value;
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: FilterChip(
        label: Text(label),
        selected: selected,
        onSelected: (_) => onPreset(value),
        visualDensity: VisualDensity.compact,
      ),
    );
  }
}
