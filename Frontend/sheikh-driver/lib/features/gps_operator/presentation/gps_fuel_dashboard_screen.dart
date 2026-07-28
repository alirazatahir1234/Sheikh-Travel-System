import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';

class GpsFuelDashboardScreen extends ConsumerStatefulWidget {
  const GpsFuelDashboardScreen({super.key});

  @override
  ConsumerState<GpsFuelDashboardScreen> createState() =>
      _GpsFuelDashboardScreenState();
}

class _GpsFuelDashboardScreenState extends ConsumerState<GpsFuelDashboardScreen> {
  bool _loading = true;
  String? _error;
  Map<String, dynamic> _data = const {};

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final now = DateTime.now();
      final from = DateTime(now.year, now.month, now.day);
      final res = await ref.read(dioProvider).get<Map<String, dynamic>>(
        ApiEndpoints.gpsFuelAnalytics,
        queryParameters: {
          'from': from.toUtc().toIso8601String(),
          'to': now.toUtc().toIso8601String(),
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
    final currency = NumberFormat.compactCurrency(symbol: '');
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Fuel dashboard'),
        actions: [
          IconButton(
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: _loading
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
                      const SgSectionTitle('Today'),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: [
                          _kpi(
                            'Consumed',
                            '${(_n('consumedLiters', 'ConsumedLiters') ?? _n('todayLiters', 'TodayLiters') ?? 0).toStringAsFixed(1)} L',
                          ),
                          _kpi(
                            'Cost',
                            currency.format(
                              (_n('todayCost', 'TodayCost') ??
                                      _n('cost', 'Cost') ??
                                      0)
                                  .toDouble(),
                            ),
                          ),
                          _kpi(
                            'Refills',
                            '${_n('refillCount', 'RefillCount') ?? _n('refills', 'Refills') ?? 0}',
                          ),
                          _kpi(
                            'Efficiency',
                            '${(_n('avgEfficiency', 'AvgEfficiency') ?? _n('efficiency', 'Efficiency') ?? 0).toStringAsFixed(1)} km/L',
                          ),
                        ],
                      ),
                      const SizedBox(height: 20),
                      const Text(
                        'Open a vehicle → Fuel tab for per-vehicle history. Theft and refill detection follow GPS analytics when telemetry is available.',
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                      ),
                    ],
                  ),
                ),
    );
  }

  Widget _kpi(String label, String value) {
    return SizedBox(
      width: 160,
      child: SgCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label,
                style: const TextStyle(
                    fontSize: 12, color: AppColors.textSecondary)),
            const SizedBox(height: 6),
            Text(value,
                style:
                    const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          ],
        ),
      ),
    );
  }
}
