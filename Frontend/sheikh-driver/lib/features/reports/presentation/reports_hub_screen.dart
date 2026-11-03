import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/fleet_reports_api.dart';
import '../domain/report_models.dart';

class ReportsHubScreen extends ConsumerStatefulWidget {
  const ReportsHubScreen({super.key});

  @override
  ConsumerState<ReportsHubScreen> createState() => _ReportsHubScreenState();
}

class _ReportsHubScreenState extends ConsumerState<ReportsHubScreen> {
  String _type = 'fuel';
  DateTime _from = DateTime.now().subtract(const Duration(days: 30));
  DateTime _to = DateTime.now();
  FleetReport? _report;
  bool _loading = false;
  String? _error;

  Future<void> _run() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final report = await ref.read(fleetReportsApiProvider).fetch(
            reportType: _type,
            from: _from,
            to: _to,
          );
      if (mounted) setState(() => _report = report);
    } catch (e) {
      if (mounted) {
        setState(() => _error = _friendlyError(e));
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _friendlyError(Object e) {
    final raw = e.toString();
    if (raw.contains('403') || raw.toLowerCase().contains('forbidden')) {
      return 'You do not have permission to view reports.';
    }
    if (raw.contains('401') || raw.toLowerCase().contains('unauthorized')) {
      return 'Session expired. Please sign in again.';
    }
    if (raw.toLowerCase().contains('socket') ||
        raw.toLowerCase().contains('timeout') ||
        raw.toLowerCase().contains('connection')) {
      return 'Could not reach the server. Check your connection and try again.';
    }
    return 'Could not load this report. Try again.';
  }

  Future<void> _pickFrom() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _from,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
    );
    if (d != null) setState(() => _from = d);
  }

  Future<void> _pickTo() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _to,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (d != null) setState(() => _to = d);
  }

  @override
  Widget build(BuildContext context) {
    final df = DateFormat('dd MMM yyyy');
    final currency = NumberFormat.compactCurrency(symbol: 'PKR ');

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: const Text('Reports')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          const SgSectionTitle('Report type'),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final opt in fleetReportTypeOptions)
                ChoiceChip(
                  label: Text(opt.label),
                  selected: _type == opt.id,
                  onSelected: (_) => setState(() => _type = opt.id),
                ),
            ],
          ),
          const SizedBox(height: 16),
          const SgSectionTitle('Date range'),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _pickFrom,
                  child: Text('From ${df.format(_from)}'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton(
                  onPressed: _pickTo,
                  child: Text('To ${df.format(_to)}'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          SgPrimaryButton(
            label: 'Run report',
            loading: _loading,
            onPressed: _run,
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            SgCard(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.error_outline,
                      color: AppColors.error, size: 20),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      _error!,
                      style:
                          const TextStyle(color: AppColors.error, height: 1.35),
                    ),
                  ),
                ],
              ),
            ),
          ],
          if (_report != null) ...[
            const SizedBox(height: 20),
            Text(
              _report!.title,
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              _report!.totalValue > 0
                  ? '${currency.format(_report!.totalValue)} · ${_report!.rows.length} rows'
                  : '${_report!.rows.length} rows',
              style: TextStyle(color: context.sg.textSecondary),
            ),
            const SizedBox(height: 12),
            if (_report!.rows.isEmpty)
              const SgCard(child: Text('No rows for this date range.'))
            else
              ..._report!.rows.map((row) {
                final previewCols = _report!.columns.take(4).toList();
                return SgCard(
                  margin: const EdgeInsets.only(bottom: 10),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        row.label.isNotEmpty ? row.label : row.key,
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                      const SizedBox(height: 6),
                      for (final col in previewCols)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 2),
                          child: Text(
                            '${col.label}: ${_formatField(row.fields[col.key], col.format)}',
                            style: TextStyle(
                              fontSize: 12,
                              color: context.sg.textSecondary,
                              height: 1.35,
                            ),
                          ),
                        ),
                      if (row.count > 0 || row.totalValue > 0)
                        Padding(
                          padding: const EdgeInsets.only(top: 4),
                          child: Text(
                            [
                              if (row.count > 0) 'Count ${row.count}',
                              if (row.totalValue > 0)
                                currency.format(row.totalValue),
                            ].join(' · '),
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                    ],
                  ),
                );
              }),
          ],
        ],
      ),
    );
  }

  String _formatField(Object? value, String format) {
    if (value == null) return '—';
    if (format == 'currency' && value is num) {
      return NumberFormat.compactCurrency(symbol: 'PKR ').format(value);
    }
    if (format == 'number' && value is num) {
      return value.toStringAsFixed(value is int ? 0 : 1);
    }
    if (format == 'date') {
      final d = DateTime.tryParse(value.toString());
      if (d != null) return DateFormat('dd MMM yyyy').format(d.toLocal());
    }
    return value.toString();
  }
}
