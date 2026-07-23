import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/gps_alert_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import 'alerts_notifier.dart';

class AlertsScreen extends ConsumerWidget {
  const AlertsScreen({super.key});

  Color _severityColor(String severity) {
    final s = severity.toLowerCase();
    if (s.contains('critical') || s.contains('high')) return AppColors.error;
    if (s.contains('medium') || s.contains('warn')) return AppColors.warning;
    return AppColors.info;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(alertsProvider);
    final df = DateFormat('dd MMM, HH:mm');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Alerts'),
        actions: [
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
                            for (final s in const ['critical', 'high', 'medium', 'low'])
                              FilterChip(
                                label: Text(_titleCase(s)),
                                selected:
                                    state.severityFilter?.toLowerCase() == s,
                                onSelected: (_) => ref
                                    .read(alertsProvider.notifier)
                                    .setSeverity(
                                      state.severityFilter?.toLowerCase() == s
                                          ? null
                                          : s,
                                    ),
                              ),
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
                            onTap: () => context.push('/alerts/${a.id}'),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
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
                                              _titleCase(
                                                a.eventType.replaceAll('_', ' '),
                                              ),
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
                                        a.message,
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
                                            _titleCase(a.severity),
                                            color: _severityColor(a.severity),
                                          ),
                                          StatusBadge(
                                            _statusLabel(a),
                                            color: _statusColor(a),
                                          ),
                                        ],
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

  static String _titleCase(String input) {
    return input
        .split(' ')
        .where((part) => part.isNotEmpty)
        .map((part) =>
            '${part[0].toUpperCase()}${part.substring(1).toLowerCase()}')
        .join(' ');
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

  static String _statusLabel(GpsAlertEvent alert) {
    if (alert.isUnread) return 'Unread';
    if (alert.status.toLowerCase() == 'active' && alert.isRead) return 'Read';
    return _titleCase(alert.status);
  }

  static Color _statusColor(GpsAlertEvent alert) {
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

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Alert')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (a) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    a.eventType.replaceAll('_', ' ').toUpperCase(),
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 16,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(a.message),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      StatusBadge(a.severity),
                      StatusBadge(AlertsScreen._statusLabel(a),
                          color: AlertsScreen._statusColor(a)),
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
                  _Row('Type', AlertsScreen._titleCase(a.eventType.replaceAll('_', ' '))),
                  _Row('Status', a.status),
                  _Row('Time', df.format(a.timestamp.toLocal())),
                  _Row(
                    'Coords',
                    '${a.latitude.toStringAsFixed(5)}, ${a.longitude.toStringAsFixed(5)}',
                  ),
                  _Row('Speed', '${a.speed.toStringAsFixed(0)} km/h'),
                  _Row('Priority', AlertsScreen._titleCase(a.severity)),
                  _Row('Read At', a.readAt != null ? df.format(a.readAt!.toLocal()) : 'Unread'),
                  if (a.acknowledgedAt != null)
                    _Row('Acknowledged', df.format(a.acknowledgedAt!.toLocal())),
                  if (a.resolvedAt != null)
                    _Row('Resolved', df.format(a.resolvedAt!.toLocal())),
                  if (a.archivedAt != null)
                    _Row('Archived', df.format(a.archivedAt!.toLocal())),
                  if (a.geofenceName != null) _Row('Geofence', a.geofenceName!),
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
              const SizedBox(height: 8),
              Wrap(
                spacing: 12,
                children: [
                  TextButton(
                    onPressed: () => context.push('/fleet/vehicles/${a.vehicleId}'),
                    child: const Text('Open vehicle'),
                  ),
                  TextButton(
                    onPressed: () => context.push('/fleet/map'),
                    child: const Text('View on map'),
                  ),
                ],
              )
            ],
          ],
        ),
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
