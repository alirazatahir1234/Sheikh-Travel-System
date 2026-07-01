import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/constants/app_theme.dart';

class _Notification {
  const _Notification({
    required this.id,
    required this.message,
    required this.type,
    required this.isRead,
    required this.createdAt,
  });

  final int id;
  final String message;
  final String type;
  final bool isRead;
  final DateTime createdAt;

  factory _Notification.fromJson(Map<String, dynamic> json) => _Notification(
        id: json['id'] as int,
        message: json['message'] as String? ?? '',
        type: json['type'] as String? ?? '',
        isRead: json['isRead'] as bool? ?? false,
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ?? DateTime.now(),
      );
}

final _notificationsProvider =
    FutureProvider.autoDispose<List<_Notification>>((ref) async {
  final dio = ref.read(dioProvider);
  final res = await dio.get<Map<String, dynamic>>(ApiEndpoints.notifications);
  final body = res.data;
  final list = (body?['data'] as List?) ?? [];
  return list.cast<Map<String, dynamic>>().map(_Notification.fromJson).toList();
});

class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notificationsAsync = ref.watch(_notificationsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: notificationsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text(e.toString())),
        data: (items) {
          if (items.isEmpty) {
            return const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.notifications_off_outlined, size: 56, color: AppColors.textSecondary),
                  SizedBox(height: 12),
                  Text('No notifications', style: TextStyle(color: AppColors.textSecondary)),
                ],
              ),
            );
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (_, i) => _NotificationCard(notification: items[i]),
          );
        },
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({required this.notification});
  final _Notification notification;

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('dd MMM, HH:mm');
    final color = notification.isRead ? AppColors.textSecondary : AppColors.primary;
    return Card(
      color: notification.isRead ? null : AppColors.primary.withValues(alpha: 0.04),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(_iconFor(notification.type), color: color, size: 18),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    notification.message,
                    style: TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: notification.isRead ? FontWeight.w400 : FontWeight.w600,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    fmt.format(notification.createdAt.toLocal()),
                    style: const TextStyle(color: AppColors.textSecondary, fontSize: 11),
                  ),
                ],
              ),
            ),
            if (!notification.isRead)
              Container(
                width: 8,
                height: 8,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
              ),
          ],
        ),
      ),
    );
  }

  IconData _iconFor(String type) => switch (type.toLowerCase()) {
        'bookingcreated' => Icons.bookmark_added_outlined,
        'tripdelayed' => Icons.schedule_outlined,
        'vehicleoffline' => Icons.gps_off_outlined,
        'paymentreceived' => Icons.payments_outlined,
        'enginecommandsent' => Icons.power_settings_new_outlined,
        _ => Icons.notifications_outlined,
      };
}
