import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';

class GpsTripSummary {
  const GpsTripSummary({
    required this.tripKey,
    required this.vehicleId,
    required this.vehicleName,
    this.startAt,
    this.endAt,
    this.distanceKm,
    this.maxSpeedKmh,
    this.avgSpeedKmh,
    this.durationMinutes,
  });

  final String tripKey;
  final int vehicleId;
  final String vehicleName;
  final DateTime? startAt;
  final DateTime? endAt;
  final double? distanceKm;
  final double? maxSpeedKmh;
  final double? avgSpeedKmh;
  final int? durationMinutes;

  factory GpsTripSummary.fromJson(Map<String, dynamic> json) {
    DateTime? parseDt(dynamic v) {
      if (v == null) return null;
      return DateTime.tryParse(v.toString());
    }

    return GpsTripSummary(
      tripKey: json['tripKey']?.toString() ??
          json['TripKey']?.toString() ??
          json['id']?.toString() ??
          '',
      vehicleId: (json['vehicleId'] as num?)?.toInt() ??
          (json['VehicleId'] as num?)?.toInt() ??
          0,
      vehicleName: json['vehicleName'] as String? ??
          json['VehicleName'] as String? ??
          json['plateNumber'] as String? ??
          'Vehicle',
      startAt: parseDt(json['startAt'] ?? json['StartAt'] ?? json['startTime']),
      endAt: parseDt(json['endAt'] ?? json['EndAt'] ?? json['endTime']),
      distanceKm: (json['distanceKm'] as num? ?? json['DistanceKm'] as num?)
          ?.toDouble(),
      maxSpeedKmh:
          (json['maxSpeedKmh'] as num? ?? json['MaxSpeedKmh'] as num?)
              ?.toDouble(),
      avgSpeedKmh:
          (json['avgSpeedKmh'] as num? ?? json['AvgSpeedKmh'] as num?)
              ?.toDouble(),
      durationMinutes: (json['durationMinutes'] as num? ??
              json['DurationMinutes'] as num?)
          ?.toInt(),
    );
  }
}

/// GPS-centric trip list (distinct from ops/driver trips).
class GpsTripsScreen extends ConsumerStatefulWidget {
  const GpsTripsScreen({super.key});

  @override
  ConsumerState<GpsTripsScreen> createState() => _GpsTripsScreenState();
}

class _GpsTripsScreenState extends ConsumerState<GpsTripsScreen> {
  bool _loading = true;
  String? _error;
  List<GpsTripSummary> _trips = const [];
  String _range = 'today';

  @override
  void initState() {
    super.initState();
    _load();
  }

  (DateTime, DateTime) _window() {
    final now = DateTime.now();
    switch (_range) {
      case 'yesterday':
        final start = DateTime(now.year, now.month, now.day)
            .subtract(const Duration(days: 1));
        return (start, start.add(const Duration(days: 1)));
      case 'week':
        return (now.subtract(const Duration(days: 7)), now);
      case 'month':
        return (now.subtract(const Duration(days: 30)), now);
      case 'today':
      default:
        return (DateTime(now.year, now.month, now.day), now);
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final (from, to) = _window();
      final dio = ref.read(dioProvider);
      final res = await dio.get<Map<String, dynamic>>(
        '/gps/trips',
        queryParameters: {
          'from': from.toUtc().toIso8601String(),
          'to': to.toUtc().toIso8601String(),
          'page': 1,
          'pageSize': 50,
        },
      );
      final items = ApiResponseParser.pagedItems(res.data);
      final fallback = ApiResponseParser.dataList(res.data);
      final rows = (items.isNotEmpty ? items : fallback)
          .map(GpsTripSummary.fromJson)
          .toList();
      if (mounted) setState(() => _trips = rows);
    } catch (e) {
      if (mounted) {
        setState(() => _error = e is DioException
            ? (e.message ?? e.toString())
            : e.toString());
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final df = DateFormat('dd MMM HH:mm');
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('GPS trips'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: _loading ? null : _load,
          ),
        ],
      ),
      body: Column(
        children: [
          SizedBox(
            height: 48,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              children: [
                for (final opt in const [
                  ('today', 'Today'),
                  ('yesterday', 'Yesterday'),
                  ('week', 'Weekly'),
                  ('month', 'Monthly'),
                ])
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(opt.$2),
                      selected: _range == opt.$1,
                      onSelected: (_) {
                        setState(() => _range = opt.$1);
                        _load();
                      },
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                    ? Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(_error!, textAlign: TextAlign.center),
                              const SizedBox(height: 12),
                              FilledButton(
                                  onPressed: _load, child: const Text('Retry')),
                            ],
                          ),
                        ),
                      )
                    : _trips.isEmpty
                        ? const Center(child: Text('No GPS trips in this range'))
                        : RefreshIndicator(
                            onRefresh: _load,
                            child: ListView.separated(
                              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                              itemCount: _trips.length,
                              separatorBuilder: (_, __) =>
                                  const SizedBox(height: 8),
                              itemBuilder: (context, i) {
                                final t = _trips[i];
                                return SgCard(
                                  onTap: t.vehicleId > 0
                                      ? () => context.push(
                                            '/fleet/vehicles/${t.vehicleId}/history',
                                          )
                                      : null,
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        t.vehicleName,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w700,
                                          fontSize: 15,
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        [
                                          if (t.startAt != null)
                                            df.format(t.startAt!.toLocal()),
                                          if (t.endAt != null)
                                            '→ ${df.format(t.endAt!.toLocal())}',
                                        ].join(' '),
                                        style: const TextStyle(
                                          fontSize: 12,
                                          color: AppColors.textSecondary,
                                        ),
                                      ),
                                      const SizedBox(height: 8),
                                      Wrap(
                                        spacing: 12,
                                        children: [
                                          if (t.distanceKm != null)
                                            Text(
                                              '${t.distanceKm!.toStringAsFixed(1)} km',
                                              style: const TextStyle(
                                                  fontWeight: FontWeight.w600),
                                            ),
                                          if (t.durationMinutes != null)
                                            Text('${t.durationMinutes} min'),
                                          if (t.maxSpeedKmh != null)
                                            Text(
                                              'max ${t.maxSpeedKmh!.toStringAsFixed(0)} km/h',
                                            ),
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
      ),
    );
  }
}
