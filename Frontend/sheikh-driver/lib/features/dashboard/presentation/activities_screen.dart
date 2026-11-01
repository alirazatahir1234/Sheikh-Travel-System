import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/dashboard_api.dart';
import '../domain/dashboard_models.dart';

final activitiesListProvider =
    FutureProvider.autoDispose<List<ActivityItem>>((ref) {
  return ref.read(dashboardApiProvider).listActivities(limit: 50);
});

/// Full recent-activity list (dashboard "View All" destination).
/// Uses the same notification + GPS alert merge as the home card — no mocks.
class ActivitiesScreen extends ConsumerWidget {
  const ActivitiesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(activitiesListProvider);
    final p = context.sg;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: const Text('Recent Activity'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.invalidate(activitiesListProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(
          child: SgBrandLoader(message: 'Loading activity…'),
        ),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline,
                    size: 40, color: AppColors.error),
                const SizedBox(height: 12),
                Text('$e', textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => ref.invalidate(activitiesListProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (items) {
          if (items.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.timeline_outlined, size: 56, color: p.textMuted),
                  const SizedBox(height: 12),
                  Text(
                    'No recent activity yet',
                    style: TextStyle(color: p.textSecondary, fontSize: 16),
                  ),
                ],
              ),
            );
          }
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async {
              ref.invalidate(activitiesListProvider);
              await ref.read(activitiesListProvider.future);
            },
            child: ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              itemCount: items.length,
              separatorBuilder: (_, __) => Divider(height: 1, color: p.divider),
              itemBuilder: (context, i) {
                final item = items[i];
                return _ActivityTile(item: item);
              },
            ),
          );
        },
      ),
    );
  }
}

class _ActivityTile extends StatelessWidget {
  const _ActivityTile({required this.item});
  final ActivityItem item;

  IconData get _icon => switch (item.kind) {
        'alert' => Icons.warning_amber_rounded,
        'trip' => Icons.location_on_outlined,
        'attendance' => Icons.person_outline_rounded,
        'maintenance' => Icons.build_outlined,
        _ => Icons.notifications_none_rounded,
      };

  Color get _color => switch (item.kind) {
        'alert' => AppColors.error,
        'trip' => AppColors.primary,
        'attendance' => AppColors.primary,
        'maintenance' => AppColors.warning,
        _ => AppColors.info,
      };

  String get _time {
    final local = item.at.toLocal();
    final diff = DateTime.now().difference(local);
    if (diff.inSeconds < 60) return 'just now';
    if (diff.inMinutes < 60) return '${diff.inMinutes} min ago';
    if (diff.inHours < 24) return '${diff.inHours}h ago';
    return DateFormat('dd MMM · HH:mm').format(local);
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return InkWell(
      onTap: item.route == null ? null : () => context.push(item.route!),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: _color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(_icon, size: 20, color: _color),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.title,
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: p.textPrimary,
                    ),
                  ),
                  if (item.subtitle.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(
                      item.subtitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 13,
                        color: p.textSecondary,
                      ),
                    ),
                  ],
                  const SizedBox(height: 4),
                  Text(
                    _time,
                    style: TextStyle(
                      fontSize: 11,
                      color: p.textMuted,
                    ),
                  ),
                ],
              ),
            ),
            if (item.route != null)
              Icon(Icons.chevron_right_rounded, color: p.textMuted, size: 20),
          ],
        ),
      ),
    );
  }
}
