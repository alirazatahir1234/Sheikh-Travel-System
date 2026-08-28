import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/alert_display.dart';
import '../domain/gps_alert_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import 'alerts_notifier.dart';

class AlertsScreen extends ConsumerStatefulWidget {
  const AlertsScreen({super.key});

  @override
  ConsumerState<AlertsScreen> createState() => _AlertsScreenState();

  static String titleCase(String input) {
    return input
        .split(' ')
        .where((part) => part.isNotEmpty)
        .map((part) =>
            '${part[0].toUpperCase()}${part.substring(1).toLowerCase()}')
        .join(' ');
  }

  static String statusLabel(GpsAlertEvent alert) {
    if (alert.isUnread) return 'Unread';
    if (alert.status.toLowerCase() == 'active' && alert.isRead) return 'Read';
    return titleCase(alert.status);
  }

  static Color statusColor(GpsAlertEvent alert) {
    if (alert.isUnread) return AppColors.primary;
    switch (alert.status.toLowerCase()) {
      case 'resolved':
        return AppColors.success;
      case 'archived':
        return AppColors.textMuted;
      case 'acknowledged':
        return AppColors.warning;
      default:
        return AppColors.info;
    }
  }
}

class _AlertsScreenState extends ConsumerState<AlertsScreen> {
  final Set<int> _selected = {};
  bool _selectMode = false;

  Color _severityColor(String severity) {
    final s = severity.toLowerCase();
    if (s.contains('critical') || s.contains('high')) return AppColors.error;
    if (s.contains('medium') || s.contains('warn')) return AppColors.warning;
    return AppColors.info;
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(alertsProvider);
    final df = DateFormat('dd MMM, HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Alerts'),
        actions: [
          IconButton(
            tooltip: _selectMode ? 'Done selecting' : 'Bulk select',
            icon: Icon(
              _selectMode ? Icons.done_rounded : Icons.checklist_rounded,
            ),
            onPressed: () => setState(() {
              _selectMode = !_selectMode;
              if (!_selectMode) _selected.clear();
            }),
          ),
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(alertsProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () => ref.read(alertsProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (state) {
          final visible = state.visible;
          final laneCounts = <String, int>{
            'critical': 0,
            'high': 0,
            'medium': 0,
            'low': 0,
          };
          for (final a in visible) {
            final s = a.severity.toLowerCase();
            if (laneCounts.containsKey(s)) {
              laneCounts[s] = laneCounts[s]! + 1;
            }
          }
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => ref.read(alertsProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Wrap(
                      spacing: 10,
                      runSpacing: 10,
                      children: [
                        _Stat('Today', '${state.stats.today}'),
                        _Stat('Unread', '${state.stats.unread}',
                            warn: state.stats.unread > 0),
                        _Stat('Active', '${state.stats.active}'),
                        _Stat('Critical', '${state.stats.critical}',
                            warn: state.stats.critical > 0),
                        _Stat('Resolved', '${state.stats.resolved}'),
                        _Stat('Archived', '${state.stats.archived}'),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final lane in const [
                          ('Critical', 'critical'),
                          ('High', 'high'),
                          ('Medium', 'medium'),
                          ('Low', 'low'),
                        ])
                          FilterChip(
                            label: Text(
                              '${lane.$1} (${laneCounts[lane.$2] ?? 0})',
                            ),
                            selected: state.severityFilter == lane.$2,
                            onSelected: (_) => ref
                                .read(alertsProvider.notifier)
                                .setSeverity(
                                  state.severityFilter == lane.$2
                                      ? null
                                      : lane.$2,
                                ),
                          ),
                      ],
                    ),
                  ),
                ),
                if (_selectMode && _selected.isNotEmpty)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: Wrap(
                        spacing: 8,
                        children: [
                          FilledButton(
                            onPressed: () async {
                              final ids = _selected.toList();
                              for (final id in ids) {
                                await ref
                                    .read(alertsProvider.notifier)
                                    .markRead(id);
                              }
                              setState(() {
                                _selected.clear();
                                _selectMode = false;
                              });
                            },
                            child: Text('Mark read (${_selected.length})'),
                          ),
                          OutlinedButton(
                            onPressed: () async {
                              final ids = _selected.toList();
                              for (final id in ids) {
                                await ref
                                    .read(alertsProvider.notifier)
                                    .resolve(id);
                              }
                              setState(() {
                                _selected.clear();
                                _selectMode = false;
                              });
                            },
                            child: Text('Resolve (${_selected.length})'),
                          ),
                        ],
                      ),
                    ),
                  ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            for (final filter in const [
                              ('Unread', 'unread'),
                              ('Read', 'read'),
                              ('Acknowledged', 'acknowledged'),
                              ('Resolved', 'resolved'),
                              ('Archived', 'archived'),
                            ])
                              FilterChip(
                                label: Text(filter.$1),
                                selected: _isLifecycleSelected(state, filter.$2),
                                onSelected: (_) => ref
                                    .read(alertsProvider.notifier)
                                    .setLifecycle(
                                      _isLifecycleSelected(state, filter.$2)
                                          ? null
                                          : filter.$2,
                                    ),
                              ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            for (final s in const [
                              'critical',
                              'high',
                              'medium',
                              'low',
                            ])
                              FilterChip(
                                label: Text(AlertsScreen.titleCase(s)),
                                selected: state.severityFilter == s,
                                onSelected: (_) => ref
                                    .read(alertsProvider.notifier)
                                    .setSeverity(
                                      state.severityFilter == s ? null : s,
                                    ),
                              ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            for (final preset in const ['today', 'yesterday', 'last7', 'last30'])
                              FilterChip(
                                label: Text(_datePresetLabel(preset)),
                                selected: state.datePreset == preset,
                                onSelected: (_) => ref
                                    .read(alertsProvider.notifier)
                                    .setDatePreset(preset),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SliverToBoxAdapter(child: SizedBox(height: 8)),
                if (visible.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: Center(child: Text('No alerts')),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final a = visible[i];
                        return Dismissible(
                          key: ValueKey(a.id),
                          direction: a.canArchive
                              ? DismissDirection.horizontal
                              : a.canMarkRead
                                  ? DismissDirection.startToEnd
                                  : DismissDirection.none,
                          confirmDismiss: (direction) async {
                            final messenger = ScaffoldMessenger.of(context);
                            if (direction == DismissDirection.startToEnd &&
                                a.canMarkRead) {
                              await ref
                                  .read(alertsProvider.notifier)
                                  .markRead(a.id);
                              messenger.showSnackBar(
                                const SnackBar(
                                  content: Text('Alert marked as read'),
                                ),
                              );
                            } else if (direction ==
                                    DismissDirection.endToStart &&
                                a.canArchive) {
                              await ref
                                  .read(alertsProvider.notifier)
                                  .archive(a.id);
                              messenger.showSnackBar(
                                const SnackBar(
                                  content: Text('Alert archived'),
                                ),
                              );
                            }
                            return false;
                          },
                          background: const _SwipeBackground(
                            color: AppColors.success,
                            icon: Icons.mark_email_read_rounded,
                            label: 'Mark Read',
                            alignEnd: false,
                          ),
                          secondaryBackground: const _SwipeBackground(
                            color: AppColors.warning,
                            icon: Icons.archive_outlined,
                            label: 'Archive',
                            alignEnd: true,
                          ),
                          child: SgCard(
                            margin: const EdgeInsets.only(bottom: 10),
                            onTap: () {
                              if (_selectMode) {
                                setState(() {
                                  if (_selected.contains(a.id)) {
                                    _selected.remove(a.id);
                                  } else {
                                    _selected.add(a.id);
                                  }
                                });
                              } else {
                                context.push('/alerts/${a.id}');
                              }
                            },
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                if (_selectMode)
                                  Checkbox(
                                    value: _selected.contains(a.id),
                                    onChanged: (_) {
                                      setState(() {
                                        if (_selected.contains(a.id)) {
                                          _selected.remove(a.id);
                                        } else {
                                          _selected.add(a.id);
                                        }
                                      });
                                    },
                                  ),
                                Container(
                                  width: 8,
                                  height: 56,
                                  decoration: BoxDecoration(
                                    color: _severityColor(a.severity),
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              alertTitle(a),
                                              style: TextStyle(
                                                fontWeight: a.isUnread
                                                    ? FontWeight.w800
                                                    : FontWeight.w700,
                                                fontSize: 13,
                                              ),
                                            ),
                                          ),
                                          if (a.isUnread)
                                            const Padding(
                                              padding:
                                                  EdgeInsets.only(left: 8),
                                              child: Icon(
                                                Icons.markunread_rounded,
                                                size: 16,
                                                color: AppColors.primary,
                                              ),
                                            ),
                                        ],
                                      ),
                                      const SizedBox(height: 2),
                                      Text(
                                        alertDescription(a),
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(fontSize: 13),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        [
                                          a.vehicleName ??
                                              'Vehicle #${a.vehicleId}',
                                          if (a.driverName != null)
                                            a.driverName!,
                                          df.format(a.timestamp.toLocal()),
                                        ].join(' · '),
                                        style: const TextStyle(
                                          fontSize: 11,
                                          color: AppColors.textMuted,
                                        ),
                                      ),
                                      const SizedBox(height: 8),
                                      Wrap(
                                        spacing: 6,
                                        runSpacing: 6,
                                        children: [
                                          StatusBadge(
                                            AlertsScreen.titleCase(a.severity),
                                            color: _severityColor(a.severity),
                                          ),
                                          StatusBadge(
                                            AlertsScreen.statusLabel(a),
                                            color: AlertsScreen.statusColor(a),
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                ),
                                if (a.canAcknowledge)
                                  Padding(
                                    padding: const EdgeInsets.only(left: 8),
                                    child: FilledButton.tonal(
                                      onPressed: () async {
                                        await ref
                                            .read(alertsProvider.notifier)
                                            .acknowledge(a.id);
                                        ScaffoldMessenger.of(context)
                                            .showSnackBar(
                                          const SnackBar(
                                            content: Text('Acknowledged'),
                                          ),
                                        );
                                      },
                                      child: const Text('Acknowledge'),
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  static bool _isLifecycleSelected(AlertsState state, String value) {
    return (value == 'unread' && state.readStateFilter == 'unread') ||
        (value == 'read' && state.readStateFilter == 'read') ||
        state.statusFilter == value;
  }

  static String _datePresetLabel(String preset) {
    switch (preset) {
      case 'today':
        return 'Today';
      case 'yesterday':
        return 'Yesterday';
      case 'last7':
        return 'Last 7';
      case 'last30':
        return 'Last 30';
      default:
        return preset;
    }
  }
}

class _Stat extends StatelessWidget {
  const _Stat(this.label, this.value, {this.warn = false});
  final String label;
  final String value;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 100,
      child: SgCard(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        child: Column(
          children: [
            Text(
              value,
              style: TextStyle(
                fontWeight: FontWeight.w800,
                fontSize: 16,
                color: warn ? AppColors.error : AppColors.textPrimary,
              ),
            ),
            Text(
              label,
              style:
                  const TextStyle(fontSize: 11, color: AppColors.textMuted),
            ),
          ],
        ),
      ),
    );
  }
}

class _SwipeBackground extends StatelessWidget {
  const _SwipeBackground({
    required this.color,
    required this.icon,
    required this.label,
    required this.alignEnd,
  });

  final Color color;
  final IconData icon;
  final String label;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 18),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(AppRadii.md),
      ),
      alignment: alignEnd ? Alignment.centerRight : Alignment.centerLeft,
      child: Row(
        mainAxisAlignment:
            alignEnd ? MainAxisAlignment.end : MainAxisAlignment.start,
        children: [
          if (alignEnd) ...[
            Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w700)),
            const SizedBox(width: 8),
            Icon(icon, color: color),
          ] else ...[
            Icon(icon, color: color),
            const SizedBox(width: 8),
            Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w700)),
          ],
        ],
      ),
    );
  }
}

class AlertDetailScreen extends ConsumerWidget {
  const AlertDetailScreen({super.key, required this.alertId});
  final int alertId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(alertDetailProvider(alertId));
    final df = DateFormat('dd MMM yyyy, HH:mm');
    final tf = DateFormat('HH:mm:ss');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Alert')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (a) {
          final meta = alertTypeMeta(a.eventType);
          final sev = alertDisplaySeverity(a, recovered: a.isResolved);
          final duration = a.resolvedAt != null
              ? formatAlertDuration(a.resolvedAt!.difference(a.timestamp))
              : '';
          final hasCoords = a.latitude != 0 || a.longitude != 0;

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              SgCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      meta.title,
                      style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 18,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      meta.description.isNotEmpty
                          ? meta.description
                          : a.message,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.35,
                      ),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        StatusBadge(
                          alertSeverityLabel(sev),
                          color: switch (sev) {
                            AlertDisplaySeverity.critical =>
                              const Color(0xFFDC2626),
                            AlertDisplaySeverity.warning =>
                              const Color(0xFFD97706),
                            AlertDisplaySeverity.info =>
                              const Color(0xFF2563EB),
                            AlertDisplaySeverity.resolved => AppColors.success,
                          },
                        ),
                        StatusBadge(AlertsScreen.statusLabel(a),
                            color: AlertsScreen.statusColor(a)),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              SgCard(
                child: Column(
                  children: [
                    _Row('Vehicle', a.vehicleName ?? '#${a.vehicleId}'),
                    _Row('Driver', a.driverName ?? '—'),
                    _Row('Started', df.format(a.timestamp.toLocal())),
                    if (a.resolvedAt != null)
                      _Row('Recovered', df.format(a.resolvedAt!.toLocal())),
                    if (duration.isNotEmpty && isOfflineAlertType(a.eventType))
                      _Row('Duration', duration),
                    _Row(
                      'Last GPS update',
                      tf.format(a.timestamp.toLocal()),
                    ),
                    _Row('Last speed', '${a.speed.toStringAsFixed(0)} km/h'),
                    if (hasCoords)
                      _Row(
                        'Last GPS position',
                        '${a.latitude.toStringAsFixed(5)}, ${a.longitude.toStringAsFixed(5)}',
                      ),
                    if (a.geofenceName != null) _Row('Geofence', a.geofenceName!),
                    _Row(
                      'Status',
                      a.isAcknowledged
                          ? 'Acknowledged'
                          : AlertsScreen.titleCase(a.status),
                    ),
                    if (a.acknowledgedAt != null)
                      _Row(
                        'Acknowledged',
                        df.format(a.acknowledgedAt!.toLocal()),
                      ),
                    if (a.archivedAt != null)
                      _Row('Archived', df.format(a.archivedAt!.toLocal())),
                    if ((a.resolutionNotes ?? '').isNotEmpty)
                      _Row('Notes', a.resolutionNotes!),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              if (a.canMarkRead)
                SgPrimaryButton(
                  label: 'Mark Read',
                  onPressed: () async {
                    await ref.read(alertsProvider.notifier).markRead(a.id);
                    ref.invalidate(alertDetailProvider(alertId));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Alert marked as read')),
                      );
                    }
                  },
                  icon: Icons.mark_email_read_rounded,
                ),
              if (a.canMarkRead) const SizedBox(height: 8),
              if (a.canAcknowledge)
                SgPrimaryButton(
                  label: 'Acknowledge',
                  onPressed: () async {
                    await ref.read(alertsProvider.notifier).acknowledge(a.id);
                    ref.invalidate(alertDetailProvider(alertId));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Alert acknowledged')),
                      );
                    }
                  },
                ),
              if (a.canResolve) ...[
                const SizedBox(height: 8),
                OutlinedButton(
                  onPressed: () async {
                    await ref.read(alertsProvider.notifier).resolve(a.id);
                    ref.invalidate(alertDetailProvider(alertId));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Alert resolved')),
                      );
                    }
                  },
                  child: const Text('Resolve'),
                ),
              ],
              if (a.canArchive) ...[
                const SizedBox(height: 8),
                OutlinedButton(
                  onPressed: () async {
                    await ref.read(alertsProvider.notifier).archive(a.id);
                    ref.invalidate(alertDetailProvider(alertId));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Alert archived')),
                      );
                    }
                  },
                  child: const Text('Archive'),
                ),
              ],
              if (a.vehicleId > 0) ...[
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: () =>
                            context.push('/fleet/vehicles/${a.vehicleId}'),
                        icon: const Icon(Icons.directions_car_outlined),
                        label: const Text('View Vehicle'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: hasCoords
                            ? () async {
                                final uri = Uri.parse(
                                  'https://www.google.com/maps/search/?api=1&query=${a.latitude},${a.longitude}',
                                );
                                if (await canLaunchUrl(uri)) {
                                  await launchUrl(
                                    uri,
                                    mode: LaunchMode.externalApplication,
                                  );
                                }
                              }
                            : () => context.push('/fleet/map'),
                        icon: const Icon(Icons.place_outlined),
                        label: const Text('View Location'),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(
            width: 100,
            child: Text(
              label,
              style: const TextStyle(color: AppColors.textMuted, fontSize: 13),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
