import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../../alerts/data/gps_alerts_api.dart';
import '../../../alerts/domain/alert_display.dart';
import '../../../alerts/domain/gps_alert_models.dart';

final vehicleAlertsTabProvider =
    FutureProvider.family<List<GpsAlertEvent>, ({int vehicleId, String preset})>(
        (ref, args) {
  return ref.read(gpsAlertsApiProvider).listEvents(
        vehicleId: args.vehicleId,
        datePreset: args.preset,
      );
});

/// Vehicle detail → Alerts tab: incident-based fleet alert list.
class VehicleAlertsTab extends ConsumerStatefulWidget {
  const VehicleAlertsTab({
    super.key,
    required this.vehicleId,
    required this.vehicleName,
    required this.plate,
  });

  final int vehicleId;
  final String vehicleName;
  final String plate;

  @override
  ConsumerState<VehicleAlertsTab> createState() => _VehicleAlertsTabState();
}

enum _SeverityFilter { all, critical, warning, info }
enum _StatusFilter { all, unacknowledged, acknowledged }
enum _TypeFilter { all, offline, overspeed, geofence, other }

class _VehicleAlertsTabState extends ConsumerState<VehicleAlertsTab> {
  static const _pageSize = 20;

  String _datePreset = 'last7';
  _SeverityFilter _severity = _SeverityFilter.all;
  _StatusFilter _status = _StatusFilter.all;
  _TypeFilter _type = _TypeFilter.all;
  int _visibleCount = _pageSize;
  bool _ackingAll = false;

  final _df = DateFormat('dd MMM, HH:mm');
  final _timeOnly = DateFormat('HH:mm');

  void _invalidate() {
    ref.invalidate(vehicleAlertsTabProvider((
      vehicleId: widget.vehicleId,
      preset: _datePreset,
    )));
  }

  List<AlertIncident> _filter(List<AlertIncident> items) {
    return items.where((inc) {
      final sev = alertDisplaySeverity(
        inc.primary,
        recovered: inc.recovered && inc.isOfflineIncident,
      );
      if (_severity != _SeverityFilter.all) {
        final match = switch (_severity) {
          _SeverityFilter.critical => sev == AlertDisplaySeverity.critical,
          _SeverityFilter.warning =>
            sev == AlertDisplaySeverity.warning ||
                sev == AlertDisplaySeverity.resolved,
          _SeverityFilter.info => sev == AlertDisplaySeverity.info,
          _SeverityFilter.all => true,
        };
        if (!match) return false;
      }

      if (_status == _StatusFilter.unacknowledged &&
          inc.primary.isAcknowledged) {
        return false;
      }
      if (_status == _StatusFilter.acknowledged &&
          !inc.primary.isAcknowledged) {
        return false;
      }

      final key = normalizeAlertEventType(inc.primary.eventType);
      if (_type != _TypeFilter.all) {
        final ok = switch (_type) {
          _TypeFilter.offline =>
            key == 'vehicle_offline' || key == 'online',
          _TypeFilter.overspeed => key == 'speed_exceeded',
          _TypeFilter.geofence =>
            key == 'geofence_enter' || key == 'geofence_exit',
          _TypeFilter.other =>
            key != 'vehicle_offline' &&
                key != 'online' &&
                key != 'speed_exceeded' &&
                key != 'geofence_enter' &&
                key != 'geofence_exit',
          _TypeFilter.all => true,
        };
        if (!ok) return false;
      }
      return true;
    }).toList();
  }

  Future<void> _acknowledgeIds(List<int> ids) async {
    final api = ref.read(gpsAlertsApiProvider);
    for (final id in ids) {
      await api.acknowledge(id);
    }
    _invalidate();
  }

  Future<void> _acknowledgeAll(List<AlertIncident> items) async {
    final ids = <int>{};
    for (final inc in items) {
      if (inc.primary.canAcknowledge) ids.add(inc.primary.id);
      ids.addAll(inc.acknowledgeIds);
    }
    if (ids.isEmpty) return;
    setState(() => _ackingAll = true);
    try {
      await _acknowledgeIds(ids.toList());
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Acknowledged ${ids.length} alert(s)')),
        );
      }
    } finally {
      if (mounted) setState(() => _ackingAll = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(vehicleAlertsTabProvider((
      vehicleId: widget.vehicleId,
      preset: _datePreset,
    )));

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e', textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: _invalidate, child: const Text('Retry')),
            ],
          ),
        ),
      ),
      data: (raw) {
        final incidents = groupAlertIncidents(raw);
        final filtered = _filter(incidents);
        final unacked =
            filtered.where((i) => !i.primary.isAcknowledged).length;
        final visible = filtered.take(_visibleCount).toList();
        final hasMore = filtered.length > visible.length;

        return RefreshIndicator(
          onRefresh: () async => _invalidate(),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          filtered.isEmpty
                              ? 'Alerts'
                              : '${filtered.length} Alerts'
                                  '${unacked > 0 ? ' · $unacked Unacknowledged' : ''}',
                          style: const TextStyle(
                            fontSize: 17,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _datePresetLabel(_datePreset),
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (unacked > 0)
                    TextButton(
                      onPressed:
                          _ackingAll ? null : () => _acknowledgeAll(filtered),
                      child: _ackingAll
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Acknowledge all'),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final lane in [
                      (_SeverityFilter.all, 'All'),
                      (_SeverityFilter.critical, 'Critical'),
                      (_SeverityFilter.warning, 'Warning'),
                      (_SeverityFilter.info, 'Info'),
                    ])
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(lane.$2),
                          selected: _severity == lane.$1,
                          onSelected: (_) => setState(() {
                            _severity = lane.$1;
                            _visibleCount = _pageSize;
                          }),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 10),
              SgCard(
                child: Column(
                  children: [
                    _FilterRow(
                      label: 'Date',
                      value: _datePresetLabel(_datePreset),
                      onTap: _pickDatePreset,
                    ),
                    const Divider(height: 1),
                    _FilterRow(
                      label: 'Type',
                      value: _typeLabel(_type),
                      onTap: _pickType,
                    ),
                    const Divider(height: 1),
                    _FilterRow(
                      label: 'Status',
                      value: _statusLabel(_status),
                      onTap: _pickStatus,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 14),
              if (filtered.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 40),
                  child: Column(
                    children: [
                      Icon(
                        Icons.notifications_none_rounded,
                        size: 48,
                        color: AppColors.primary.withValues(alpha: 0.5),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        raw.isEmpty
                            ? 'No GPS alerts in this period'
                            : 'No alerts match these filters',
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        raw.isEmpty
                            ? 'Alerts will appear when the vehicle goes offline, speeds, or hits a geofence.'
                            : 'Try a wider date range or clear filters.',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                )
              else ...[
                for (final inc in visible) ...[
                  _AlertIncidentCard(
                    incident: inc,
                    vehicleName: widget.vehicleName,
                    plate: widget.plate,
                    df: _df,
                    timeOnly: _timeOnly,
                    onOpen: () => context.push('/alerts/${inc.primary.id}'),
                    onAcknowledge: (!inc.primary.isAcknowledged &&
                            (inc.primary.canAcknowledge ||
                                inc.needsAcknowledge))
                        ? () async {
                            final ids = inc.acknowledgeIds.isNotEmpty
                                ? inc.acknowledgeIds
                                : [if (inc.primary.canAcknowledge) inc.primary.id];
                            if (ids.isEmpty) return;
                            await _acknowledgeIds(ids);
                            if (context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('Acknowledged'),
                                ),
                              );
                            }
                          }
                        : null,
                  ),
                  const SizedBox(height: 8),
                ],
                if (hasMore)
                  Center(
                    child: OutlinedButton(
                      onPressed: () => setState(
                        () => _visibleCount += _pageSize,
                      ),
                      child: Text(
                        'Load more (${filtered.length - visible.length} left)',
                      ),
                    ),
                  ),
              ],
            ],
          ),
        );
      },
    );
  }

  String _datePresetLabel(String p) => switch (p) {
        'today' => 'Today',
        'yesterday' => 'Yesterday',
        'last7' => 'Last 7 days',
        'last30' => 'Last 30 days',
        _ => p,
      };

  String _typeLabel(_TypeFilter t) => switch (t) {
        _TypeFilter.all => 'All alerts',
        _TypeFilter.offline => 'Offline / Online',
        _TypeFilter.overspeed => 'Overspeed',
        _TypeFilter.geofence => 'Geofence',
        _TypeFilter.other => 'Other',
      };

  String _statusLabel(_StatusFilter s) => switch (s) {
        _StatusFilter.all => 'All',
        _StatusFilter.unacknowledged => 'Unacknowledged',
        _StatusFilter.acknowledged => 'Acknowledged',
      };

  Future<void> _pickDatePreset() async {
    final next = await showModalBottomSheet<String>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final p in ['today', 'yesterday', 'last7', 'last30'])
              ListTile(
                title: Text(_datePresetLabel(p)),
                trailing: _datePreset == p
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () => Navigator.pop(ctx, p),
              ),
          ],
        ),
      ),
    );
    if (next == null || next == _datePreset) return;
    setState(() {
      _datePreset = next;
      _visibleCount = _pageSize;
    });
  }

  Future<void> _pickType() async {
    final next = await showModalBottomSheet<_TypeFilter>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final t in _TypeFilter.values)
              ListTile(
                title: Text(_typeLabel(t)),
                trailing: _type == t
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () => Navigator.pop(ctx, t),
              ),
          ],
        ),
      ),
    );
    if (next == null) return;
    setState(() {
      _type = next;
      _visibleCount = _pageSize;
    });
  }

  Future<void> _pickStatus() async {
    final next = await showModalBottomSheet<_StatusFilter>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final s in _StatusFilter.values)
              ListTile(
                title: Text(_statusLabel(s)),
                trailing: _status == s
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () => Navigator.pop(ctx, s),
              ),
          ],
        ),
      ),
    );
    if (next == null) return;
    setState(() {
      _status = next;
      _visibleCount = _pageSize;
    });
  }
}

class _FilterRow extends StatelessWidget {
  const _FilterRow({
    required this.label,
    required this.value,
    required this.onTap,
  });

  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        child: Row(
          children: [
            SizedBox(
              width: 64,
              child: Text(
                label,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textMuted,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            Expanded(
              child: Text(
                value,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
            const Icon(Icons.expand_more, color: AppColors.textMuted),
          ],
        ),
      ),
    );
  }
}

class _AlertIncidentCard extends StatelessWidget {
  const _AlertIncidentCard({
    required this.incident,
    required this.vehicleName,
    required this.plate,
    required this.df,
    required this.timeOnly,
    required this.onOpen,
    this.onAcknowledge,
  });

  final AlertIncident incident;
  final String vehicleName;
  final String plate;
  final DateFormat df;
  final DateFormat timeOnly;
  final VoidCallback onOpen;
  final VoidCallback? onAcknowledge;

  @override
  Widget build(BuildContext context) {
    final a = incident.primary;
    final meta = alertTypeMeta(a.eventType);
    final sev = alertDisplaySeverity(
      a,
      recovered: incident.recovered && incident.isOfflineIncident,
    );
    final sevColor = _sevColor(sev);
    final title = meta.title;
    final body = meta.description.isNotEmpty
        ? meta.description
        : (a.message.trim().isNotEmpty ? a.message : 'Alert recorded.');

    final startLocal = a.timestamp.toLocal();
    String whenLine;
    if (incident.isOfflineIncident && incident.endedAt != null) {
      final endLocal = incident.endedAt!.toLocal();
      final sameDay = startLocal.year == endLocal.year &&
          startLocal.month == endLocal.month &&
          startLocal.day == endLocal.day;
      whenLine = sameDay
          ? '${DateFormat('dd MMM').format(startLocal)}, '
              '${timeOnly.format(startLocal)} → ${timeOnly.format(endLocal)}'
          : '${df.format(startLocal)} → ${df.format(endLocal)}';
    } else {
      whenLine = df.format(startLocal);
    }

    final dur = formatAlertDuration(incident.duration);
    final showAckButton = onAcknowledge != null;
    final showAcked = a.isAcknowledged && !showAckButton;

    return SgCard(
      onTap: onOpen,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: sevColor,
                  shape: BoxShape.circle,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  title.toUpperCase(),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 13,
                    letterSpacing: 0.2,
                  ),
                ),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: sevColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  alertSeverityLabel(sev),
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w800,
                    color: sevColor,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            vehicleName,
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
          ),
          if (plate.trim().isNotEmpty)
            Text(
              plate,
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          const SizedBox(height: 8),
          Text(
            body,
            style: const TextStyle(
              fontSize: 13,
              color: AppColors.textSecondary,
              height: 1.35,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            whenLine,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
            ),
          ),
          if (dur.isNotEmpty && incident.isOfflineIncident) ...[
            const SizedBox(height: 2),
            Text(
              incident.recovered
                  ? 'Offline duration: $dur'
                  : 'Offline for $dur (ongoing)',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: sevColor,
              ),
            ),
          ],
          if (a.geofenceName != null && a.geofenceName!.trim().isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              a.geofenceName!,
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textMuted,
              ),
            ),
          ],
          const SizedBox(height: 10),
          Row(
            children: [
              const Spacer(),
              if (showAcked) ...[
                const Icon(Icons.check_circle,
                    size: 16, color: AppColors.success),
                const SizedBox(width: 6),
                Text(
                  a.acknowledgedAt != null
                      ? 'Acknowledged · ${df.format(a.acknowledgedAt!.toLocal())}'
                      : 'Acknowledged',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.success,
                  ),
                ),
              ] else if (showAckButton)
                TextButton(
                  onPressed: onAcknowledge,
                  child: const Text('Acknowledge'),
                ),
            ],
          ),
        ],
      ),
    );
  }

  Color _sevColor(AlertDisplaySeverity s) => switch (s) {
        AlertDisplaySeverity.critical => const Color(0xFFDC2626),
        AlertDisplaySeverity.warning => const Color(0xFFD97706),
        AlertDisplaySeverity.info => const Color(0xFF2563EB),
        AlertDisplaySeverity.resolved => const Color(0xFF16A34A),
      };
}
