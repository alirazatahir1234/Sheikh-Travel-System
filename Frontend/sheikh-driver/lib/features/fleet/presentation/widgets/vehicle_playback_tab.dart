import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../data/fleet_api.dart';
import '../../domain/fleet_models.dart';
import '../../domain/trip_display.dart';
import '../playback/playback_helpers.dart';

enum PlaybackRangePreset {
  hours6,
  today,
  yesterday,
  days3,
  days7,
  custom,
}

extension on PlaybackRangePreset {
  String get label => switch (this) {
        PlaybackRangePreset.hours6 => 'Last 6h',
        PlaybackRangePreset.today => 'Today',
        PlaybackRangePreset.yesterday => 'Yesterday',
        PlaybackRangePreset.days3 => 'Last 3 days',
        PlaybackRangePreset.days7 => 'Last 7 days',
        PlaybackRangePreset.custom => 'Custom',
      };

  String get queryValue => switch (this) {
        PlaybackRangePreset.hours6 => 'hours6',
        PlaybackRangePreset.today => 'today',
        PlaybackRangePreset.yesterday => 'yesterday',
        PlaybackRangePreset.days3 => 'days3',
        PlaybackRangePreset.days7 => 'days7',
        PlaybackRangePreset.custom => 'custom',
      };
}

/// Vehicle detail → Playback tab: history summary + trip list before full player.
class VehiclePlaybackTab extends ConsumerStatefulWidget {
  const VehiclePlaybackTab({super.key, required this.vehicleId});

  final int vehicleId;

  @override
  ConsumerState<VehiclePlaybackTab> createState() => _VehiclePlaybackTabState();
}

class _VehiclePlaybackTabState extends ConsumerState<VehiclePlaybackTab> {
  static const _quickPresets = [
    PlaybackRangePreset.hours6,
    PlaybackRangePreset.today,
    PlaybackRangePreset.yesterday,
    PlaybackRangePreset.days3,
    PlaybackRangePreset.days7,
  ];

  PlaybackRangePreset _preset = PlaybackRangePreset.today;
  late DateTime _from;
  late DateTime _to;

  bool _loading = true;
  String? _error;
  List<GpsTrip> _trips = const [];

  final _dayFmt = DateFormat('dd MMM yyyy');
  final _rangeFmt = DateFormat('dd MMM yyyy HH:mm');
  final _lastActFmt = DateFormat('dd MMM yyyy • HH:mm');
  final _timeFmt = DateFormat('HH:mm');

  @override
  void initState() {
    super.initState();
    _applyPreset(PlaybackRangePreset.today, reload: false);
    unawaited(_load());
  }

  void _applyPreset(PlaybackRangePreset preset, {bool reload = true}) {
    final now = DateTime.now();
    late DateTime from;
    late DateTime to;
    switch (preset) {
      case PlaybackRangePreset.hours6:
        to = now;
        from = now.subtract(const Duration(hours: 6));
      case PlaybackRangePreset.today:
        to = now;
        from = DateTime(now.year, now.month, now.day);
      case PlaybackRangePreset.yesterday:
        final startToday = DateTime(now.year, now.month, now.day);
        to = startToday;
        from = startToday.subtract(const Duration(days: 1));
      case PlaybackRangePreset.days3:
        to = now;
        from = now.subtract(const Duration(days: 3));
      case PlaybackRangePreset.days7:
        to = now;
        from = now.subtract(const Duration(days: 7));
      case PlaybackRangePreset.custom:
        from = _from;
        to = _to;
    }
    setState(() {
      _preset = preset;
      _from = from;
      _to = to;
    });
    if (reload) unawaited(_load());
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final trips = await ref.read(fleetApiProvider).getGpsTrips(
            vehicleId: widget.vehicleId,
            from: _from.toUtc(),
            to: _to.toUtc(),
            pageSize: 100,
          );
      if (!mounted) return;
      trips.sort((a, b) => b.startTime.compareTo(a.startTime));
      setState(() {
        _trips = trips;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
        _trips = const [];
      });
    }
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
    setState(() {
      _preset = PlaybackRangePreset.custom;
      _from = from;
      _to = to;
    });
    await _load();
  }

  String get _periodTitle => switch (_preset) {
        PlaybackRangePreset.hours6 => 'Last 6 hours',
        PlaybackRangePreset.today => 'Today',
        PlaybackRangePreset.yesterday => 'Yesterday',
        PlaybackRangePreset.days3 => 'Last 3 days',
        PlaybackRangePreset.days7 => 'Last 7 days',
        PlaybackRangePreset.custom =>
          '${_dayFmt.format(_from.toLocal())} – ${_dayFmt.format(_to.toLocal())}',
      };

  void _openHistory({String? tripKey, bool playFull = false}) {
    final params = <String, String>{};
    if (tripKey != null && tripKey.isNotEmpty) {
      params['tripKey'] = tripKey;
    } else {
      params['preset'] = _preset.queryValue;
      if (_preset == PlaybackRangePreset.custom) {
        params['from'] = _from.toUtc().toIso8601String();
        params['to'] = _to.toUtc().toIso8601String();
      }
      if (playFull) params['play'] = 'full';
    }
    final q = Uri(queryParameters: params).query;
    context.push('/fleet/vehicles/${widget.vehicleId}/history?$q');
  }

  void _openTrip(GpsTrip trip) {
    final key = trip.tripKey?.trim();
    if (key != null && key.isNotEmpty) {
      _openHistory(tripKey: key);
      return;
    }
    // No trip key — open range playback scoped to trip window via custom range.
    final params = <String, String>{
      'preset': 'custom',
      'from': trip.startTime.toUtc().toIso8601String(),
      'to': trip.endTime.toUtc().toIso8601String(),
      'play': 'full',
    };
    final q = Uri(queryParameters: params).query;
    context.push('/fleet/vehicles/${widget.vehicleId}/history?$q');
  }

  @override
  Widget build(BuildContext context) {
    final moving = filterTrips(_trips, TripListFilter.moving);
    final stops = filterTrips(_trips, TripListFilter.stops);
    final totalKm = moving.fold<double>(0, (s, t) => s + t.distanceKm);
    final driveMinutes = moving.fold<int>(0, (s, t) => s + t.durationMinutes);
    final maxSpeed = moving.isEmpty
        ? 0.0
        : moving.map(displayMaxSpeedKmh).reduce((a, b) => a > b ? a : b);
    final lastTrip = _trips.isEmpty ? null : _trips.first;
    final recentMoving = moving.take(8).toList();
    final hasData = _trips.isNotEmpty;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
        children: [
          const Text(
            'Playback History',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Select a period, review trips, then open the map player.',
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.35,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 16),
          const Text(
            'Select a period',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 13,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final p in _quickPresets)
                _RangeChip(
                  label: p.label,
                  selected: _preset == p,
                  onTap: () => _applyPreset(p),
                ),
            ],
          ),
          const SizedBox(height: 12),
          _CustomRangeCard(
            label: _preset == PlaybackRangePreset.custom
                ? '${_rangeFmt.format(_from.toLocal())} → ${_rangeFmt.format(_to.toLocal())}'
                : 'Pick custom date & time',
            selected: _preset == PlaybackRangePreset.custom,
            onTap: _pickCustomRange,
          ),
          const SizedBox(height: 20),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_error != null)
            _ErrorBlock(message: _error!, onRetry: _load)
          else if (!hasData)
            _EmptyGpsBlock(onChangeRange: _pickCustomRange)
          else ...[
            _SummaryCard(
              periodTitle: _periodTitle,
              tripCount: moving.length,
              stopCount: stops.length,
              distanceKm: totalKm,
              driveMinutes: driveMinutes,
              maxSpeedKmh: maxSpeed,
              lastActivity: lastTrip == null
                  ? null
                  : (
                      when: _lastActFmt.format(lastTrip.endTime.toLocal()),
                      place: formatTripRoute(lastTrip).primary,
                      distanceKm: lastTrip.distanceKm,
                      minutes: lastTrip.durationMinutes,
                    ),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                const Expanded(
                  child: Text(
                    'Recent Trips',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                TextButton(
                  onPressed: () => _openHistory(),
                  child: const Text('View all'),
                ),
              ],
            ),
            if (recentMoving.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  stops.isNotEmpty
                      ? 'Only stops in this period — open View all to see them.'
                      : 'No motion trips in this period.',
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
              )
            else
              for (final trip in recentMoving) ...[
                _PlaybackTripCard(
                  trip: trip,
                  timeFmt: _timeFmt,
                  dayFmt: _dayFmt,
                  onTap: () => _openTrip(trip),
                ),
                const SizedBox(height: 8),
              ],
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: () => _openHistory(playFull: true),
              icon: const Icon(Icons.play_arrow_rounded),
              label: const Text('View Playback'),
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(48),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _RangeChip extends StatelessWidget {
  const _RangeChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? AppColors.primary : Colors.white,
      borderRadius: BorderRadius.circular(20),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(20),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.border,
              width: selected ? 1.5 : 1,
            ),
          ),
          child: Text(
            label,
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 13,
              color: selected ? Colors.white : AppColors.textPrimary,
            ),
          ),
        ),
      ),
    );
  }
}

class _CustomRangeCard extends StatelessWidget {
  const _CustomRangeCard({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected
          ? AppColors.primary.withValues(alpha: 0.08)
          : AppColors.cardBg,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.border,
              width: selected ? 1.5 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(
                Icons.calendar_month_rounded,
                color: selected ? AppColors.primary : AppColors.textSecondary,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Custom date & time',
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 14,
                        color: selected
                            ? AppColors.primary
                            : AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      label,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right_rounded,
                  color: AppColors.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.periodTitle,
    required this.tripCount,
    required this.stopCount,
    required this.distanceKm,
    required this.driveMinutes,
    required this.maxSpeedKmh,
    this.lastActivity,
  });

  final String periodTitle;
  final int tripCount;
  final int stopCount;
  final double distanceKm;
  final int driveMinutes;
  final double maxSpeedKmh;
  final ({
    String when,
    String place,
    double distanceKm,
    int minutes,
  })? lastActivity;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            periodTitle,
            style: const TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 15,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            '$tripCount ${tripCount == 1 ? 'trip' : 'trips'} available',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            '${distanceKm.toStringAsFixed(1)} km travelled · '
            '${formatDurationMinutes(driveMinutes)} driving',
            style: const TextStyle(
              fontSize: 13,
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            children: [
              _metaDot('$stopCount ${stopCount == 1 ? 'stop' : 'stops'}'),
              if (maxSpeedKmh > 0)
                _metaDot('Max ${maxSpeedKmh.toStringAsFixed(0)} km/h'),
            ],
          ),
          if (lastActivity != null) ...[
            const SizedBox(height: 14),
            const Divider(height: 1),
            const SizedBox(height: 12),
            const Text(
              'Last activity',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.textMuted,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              lastActivity!.when,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 2),
            Text(
              lastActivity!.place,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              '${lastActivity!.distanceKm.toStringAsFixed(1)} km · '
              '${formatDurationMinutesCompact(lastActivity!.minutes)}',
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textMuted,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _metaDot(String text) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 6,
          height: 6,
          decoration: const BoxDecoration(
            color: AppColors.primary,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: 6),
        Text(
          text,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }
}

class _PlaybackTripCard extends StatelessWidget {
  const _PlaybackTripCard({
    required this.trip,
    required this.timeFmt,
    required this.dayFmt,
    required this.onTap,
  });

  final GpsTrip trip;
  final DateFormat timeFmt;
  final DateFormat dayFmt;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final start = trip.startTime.toLocal();
    final end = trip.endTime.toLocal();
    final sameDay = start.year == end.year &&
        start.month == end.month &&
        start.day == end.day;
    final timeLabel = sameDay
        ? '${timeFmt.format(start)} → ${timeFmt.format(end)}'
        : '${dayFmt.format(start)} ${timeFmt.format(start)} → '
            '${dayFmt.format(end)} ${timeFmt.format(end)}';
    final route = formatTripRoute(trip);

    return SgCard(
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.directions_car_filled_rounded,
            color: AppColors.primary,
            size: 22,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  timeLabel,
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 14,
                  ),
                ),
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
                const SizedBox(height: 6),
                Text(
                  '${trip.distanceKm.toStringAsFixed(1)} km · '
                  '${formatDurationMinutesCompact(trip.durationMinutes)} · '
                  'Avg ${formatTripAvgSpeed(trip)}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded, color: AppColors.textMuted),
        ],
      ),
    );
  }
}

class _EmptyGpsBlock extends StatelessWidget {
  const _EmptyGpsBlock({required this.onChangeRange});

  final VoidCallback onChangeRange;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 36, horizontal: 8),
      child: Column(
        children: [
          Container(
            width: 64,
            height: 64,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.location_off_outlined,
              size: 32,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: 16),
          const Text(
            'No GPS history',
            style: TextStyle(
              fontSize: 17,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'No GPS positions were recorded for this vehicle during the selected period.',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.4,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 20),
          OutlinedButton(
            onPressed: onChangeRange,
            child: const Text('Change date range'),
          ),
        ],
      ),
    );
  }
}

class _ErrorBlock extends StatelessWidget {
  const _ErrorBlock({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 32),
      child: Column(
        children: [
          const Text(
            'Could not load playback history',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 12),
          OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}
