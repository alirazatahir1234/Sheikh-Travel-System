import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../data/timeline_api.dart';
import '../domain/timeline_event_model.dart';

final _timelineProvider = FutureProvider.autoDispose<List<TimelineEvent>>(
  (ref) => ref.read(timelineApiProvider).getTimeline(),
);

class TimelineScreen extends ConsumerWidget {
  const TimelineScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(_timelineProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Activity Timeline'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(_timelineProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _ErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(_timelineProvider),
        ),
        data: (events) {
          if (events.isEmpty) {
            return const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.history, size: 64, color: AppColors.textSecondary),
                  SizedBox(height: 12),
                  Text('No activity yet',
                      style: TextStyle(color: AppColors.textSecondary, fontSize: 16)),
                ],
              ),
            );
          }
          return _TimelineList(events: events);
        },
      ),
    );
  }
}

class _TimelineList extends StatelessWidget {
  const _TimelineList({required this.events});
  final List<TimelineEvent> events;

  @override
  Widget build(BuildContext context) {
    // Group by date
    final grouped = <String, List<TimelineEvent>>{};
    for (final e in events) {
      final key = DateFormat('EEEE, d MMMM yyyy').format(e.eventTime.toLocal());
      grouped.putIfAbsent(key, () => []).add(e);
    }
    final dates = grouped.keys.toList();

    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
      itemCount: dates.length,
      itemBuilder: (context, i) {
        final date = dates[i];
        final dayEvents = grouped[date]!;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 20, bottom: 8, left: 48),
              child: Text(
                date,
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textSecondary,
                  letterSpacing: 0.5,
                ),
              ),
            ),
            ...dayEvents.asMap().entries.map((entry) {
              final isLast = entry.key == dayEvents.length - 1 && i == dates.length - 1;
              return _TimelineTile(
                event: entry.value,
                isLast: isLast,
              );
            }),
          ],
        );
      },
    );
  }
}

class _TimelineTile extends StatelessWidget {
  const _TimelineTile({required this.event, required this.isLast});
  final TimelineEvent event;
  final bool isLast;

  static const _iconMap = <String, IconData>{
    'tripassigned': Icons.assignment,
    'tripstarted': Icons.play_circle_outline,
    'tripcompleted': Icons.check_circle_outline,
    'triprejected': Icons.cancel_outlined,
    'checkin': Icons.login,
    'checkout': Icons.logout,
    'fuelsubmitted': Icons.local_gas_station,
    'violationrecorded': Icons.warning_amber_outlined,
    'breakstart': Icons.coffee_outlined,
    'breakend': Icons.coffee,
  };

  static const _colorMap = <String, Color>{
    'tripassigned': AppColors.primary,
    'tripstarted': AppColors.primaryLight,
    'tripcompleted': AppColors.success,
    'triprejected': AppColors.error,
    'checkin': AppColors.success,
    'checkout': AppColors.warning,
    'fuelsubmitted': AppColors.warning,
    'violationrecorded': AppColors.error,
    'breakstart': AppColors.textSecondary,
    'breakend': AppColors.textSecondary,
  };

  @override
  Widget build(BuildContext context) {
    final key = event.eventType.toLowerCase().replaceAll(' ', '');
    final color = _colorMap[key] ?? AppColors.accent;
    final icon = _iconMap[key] ?? Icons.circle_outlined;
    final timeFmt = DateFormat('HH:mm');

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Timeline column
          SizedBox(
            width: 48,
            child: Column(
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(icon, color: color, size: 18),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      color: AppColors.divider,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          // Content
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          event.title,
                          style: const TextStyle(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                            fontSize: 14,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        timeFmt.format(event.eventTime.toLocal()),
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                  if (event.description != null) ...[
                    const SizedBox(height: 3),
                    Text(
                      event.description!,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 13,
                      ),
                    ),
                  ],
                  if (event.status != null) ...[
                    const SizedBox(height: 5),
                    Container(
                      padding:
                          const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        event.status!,
                        style: TextStyle(
                          color: color,
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.cloud_off, size: 48, color: AppColors.textSecondary),
              const SizedBox(height: 12),
              Text(message, textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textSecondary)),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
}
