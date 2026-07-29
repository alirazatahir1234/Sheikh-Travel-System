import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';

class GpsMileageDashboardScreen extends ConsumerStatefulWidget {
  const GpsMileageDashboardScreen({super.key});

  @override
  ConsumerState<GpsMileageDashboardScreen> createState() =>
      _GpsMileageDashboardScreenState();
}

class _GpsMileageDashboardScreenState
    extends ConsumerState<GpsMileageDashboardScreen> {
  bool _loading = true;
  String? _error;
  Map<String, dynamic> _data = const {};
  String _range = 'today';

  @override
  void initState() {
    super.initState();
    _load();
  }

  (DateTime, DateTime) _window() {
    final now = DateTime.now();
    switch (_range) {
      case 'week':
        return (now.subtract(const Duration(days: 7)), now);
      case 'month':
        return (now.subtract(const Duration(days: 30)), now);
      case 'year':
        return (now.subtract(const Duration(days: 365)), now);
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
      final res = await ref.read(dioProvider).get<Map<String, dynamic>>(
        '/gps/analytics/distance',
        queryParameters: {
          'from': from.toUtc().toIso8601String(),
          'to': to.toUtc().toIso8601String(),
        },
      );
      if (mounted) {
        setState(() => _data = ApiResponseParser.dataMap(res.data));
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  num? _n(String a, [String? b]) =>
      (_data[a] ?? (b == null ? null : _data[b])) as num?;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Mileage dashboard'),
        actions: [
          IconButton(
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh_rounded),
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
                  ('week', 'Weekly'),
                  ('month', 'Monthly'),
                  ('year', 'Yearly'),
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
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                          children: [
                            SgCard(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text(
                                    'Distance',
                                    style: TextStyle(
                                        color: AppColors.textSecondary),
                                  ),
                                  const SizedBox(height: 6),
                                  Text(
                                    '${(_n('distanceKm', 'DistanceKm') ?? _n('totalDistanceKm', 'TotalDistanceKm') ?? 0).toStringAsFixed(1)} km',
                                    style: const TextStyle(
                                      fontSize: 28,
                                      fontWeight: FontWeight.w800,
                                    ),
                                  ),
                                  const SizedBox(height: 12),
                                  Text(
                                    'Avg daily: ${(_n('avgDailyKm', 'AvgDailyKm') ?? 0).toStringAsFixed(1)} km',
                                    style: const TextStyle(
                                      color: AppColors.textSecondary,
                                    ),
                                  ),
                                  Text(
                                    'Driving: ${(_n('drivingMinutes', 'DrivingMinutes') ?? 0)} min',
                                    style: const TextStyle(
                                      color: AppColors.textSecondary,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 12),
                            const Text(
                              'Open vehicle history playback for route-level mileage and trip trends.',
                              style: TextStyle(
                                color: AppColors.textSecondary,
                                height: 1.4,
                              ),
                            ),
                          ],
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}
