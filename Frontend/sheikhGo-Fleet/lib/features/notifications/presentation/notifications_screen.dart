import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/notification_models.dart';
import '../services/notification_deep_link.dart';
import 'notifications_notifier.dart';

enum _AlertTab { all, unread, trips, system }

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  _AlertTab _tab = _AlertTab.all;

  List<AppNotification> _applyTab(List<AppNotification> items) {
    switch (_tab) {
      case _AlertTab.all:
        return items;
      case _AlertTab.unread:
        return items.where((n) => !n.isRead).toList();
      case _AlertTab.trips:
        return items.where((n) {
          final c = n.category.toLowerCase();
          final t = n.type.toLowerCase();
          return c.contains('trip') ||
              t.contains('trip') ||
              t.contains('booking');
        }).toList();
      case _AlertTab.system:
        return items.where((n) {
          final c = n.category.toLowerCase();
          final t = n.type.toLowerCase();
          return !(c.contains('trip') ||
              t.contains('trip') ||
              t.contains('booking'));
        }).toList();
    }
  }

  @override
  Widget build(BuildContext context) {
    final filter = ref.watch(notificationsFilterProvider);
    final async = ref.watch(notificationsListProvider);
    final actions = ref.read(notificationsActionsProvider);
    final unreadCount =
        ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Alerts'),
        actions: [
          if (filter.mailbox == NotificationMailbox.inbox)
            TextButton(
              onPressed: () async {
                await actions.markAllRead();
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Marked all as read')),
                  );
                }
              },
              child: const Text('Mark all read'),
            ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _TabChip(
                    label: 'All',
                    selected: _tab == _AlertTab.all,
                    onTap: () => setState(() => _tab = _AlertTab.all),
                  ),
                  const SizedBox(width: 8),
                  _TabChip(
                    label: unreadCount > 0 ? 'Unread ($unreadCount)' : 'Unread',
                    selected: _tab == _AlertTab.unread,
                    onTap: () => setState(() => _tab = _AlertTab.unread),
                  ),
                  const SizedBox(width: 8),
                  _TabChip(
                    label: 'Trips',
                    selected: _tab == _AlertTab.trips,
                    onTap: () => setState(() => _tab = _AlertTab.trips),
                  ),
                  const SizedBox(width: 8),
                  _TabChip(
                    label: 'System',
                    selected: _tab == _AlertTab.system,
                    onTap: () => setState(() => _tab = _AlertTab.system),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          Expanded(
            child: async.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('$e', textAlign: TextAlign.center),
                    const SizedBox(height: 12),
                    SgPrimaryButton(
                      label: 'Retry',
                      onPressed: () =>
                          ref.invalidate(notificationsListProvider),
                    ),
                  ],
                ),
              ),
              data: (items) {
                final filtered = _applyTab(items);
                if (filtered.isEmpty) {
                  return const Center(
                    child: Text(
                      'No alerts',
                      style: TextStyle(color: AppColors.textSecondary),
                    ),
                  );
                }
                return RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () async =>
                      ref.invalidate(notificationsListProvider),
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                    itemCount: filtered.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (_, i) => _AlertCard(
                      notification: filtered[i],
                      archived:
                          filter.mailbox == NotificationMailbox.archived,
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _TabChip extends StatelessWidget {
  const _TabChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : Colors.white,
          borderRadius: BorderRadius.circular(AppRadii.pill),
          border: Border.all(
            color: selected ? AppColors.primary : AppColors.border,
          ),
          boxShadow: selected ? null : AppShadows.card,
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : AppColors.textSecondary,
          ),
        ),
      ),
    );
  }
}

class _AlertCard extends ConsumerWidget {
  const _AlertCard({
    required this.notification,
    required this.archived,
  });

  final AppNotification notification;
  final bool archived;

  String _timeAgo(DateTime dt) {
    final d = DateTime.now().difference(dt);
    if (d.inMinutes < 1) return 'Just now';
    if (d.inMinutes < 60) return '${d.inMinutes}m ago';
    if (d.inHours < 24) return '${d.inHours}h ago';
    return DateFormat('dd MMM').format(dt);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final actions = ref.read(notificationsActionsProvider);
    final color =
        notification.isRead ? AppColors.textSecondary : AppColors.primary;
    final display = notification.title.isNotEmpty
        ? notification.title
        : notification.message;

    Future<void> open() async {
      if (!notification.isRead && !archived) {
        await actions.markRead(notification.id);
      }
      final route = NotificationDeepLink.fromNotification(notification);
      if (route != null && context.mounted) {
        context.push(route);
      }
    }

    return SgCard(
      onTap: open,
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(AppRadii.sm),
            ),
            child: Icon(_iconFor(notification), color: color, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  display,
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: notification.isRead
                        ? FontWeight.w500
                        : FontWeight.w700,
                    fontSize: 14,
                  ),
                ),
                if (notification.title.isNotEmpty &&
                    notification.message.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    notification.message,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                ],
                const SizedBox(height: 6),
                Text(
                  _timeAgo(notification.createdAt.toLocal()),
                  style: const TextStyle(
                    color: AppColors.textMuted,
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ),
          if (!notification.isRead && !archived)
            Container(
              width: 8,
              height: 8,
              margin: const EdgeInsets.only(left: 6, top: 6),
              decoration: const BoxDecoration(
                color: AppColors.primary,
                shape: BoxShape.circle,
              ),
            ),
        ],
      ),
    );
  }

  IconData _iconFor(AppNotification n) {
    final t = n.type.toLowerCase();
    final c = n.category.toLowerCase();
    if (t.contains('sos') || c.contains('sos')) return Icons.sos_outlined;
    if (t.contains('cancel')) return Icons.cancel_outlined;
    if (c.contains('inspection')) return Icons.fact_check_outlined;
    if (t.contains('booking') || t.contains('trip') || c.contains('trip')) {
      return Icons.directions_car_outlined;
    }
    return Icons.notifications_outlined;
  }
}
