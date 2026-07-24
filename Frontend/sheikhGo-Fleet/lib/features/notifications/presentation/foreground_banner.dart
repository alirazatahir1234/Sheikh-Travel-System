import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../presentation/notifications_notifier.dart';

/// In-app banner for FCM messages received while the app is in the foreground.
class ForegroundNotificationBanner extends ConsumerWidget {
  const ForegroundNotificationBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final event = ref.watch(foregroundBannerProvider);
    if (event == null) return const SizedBox.shrink();

    return SafeArea(
      child: Align(
        alignment: Alignment.topCenter,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
          child: Material(
            elevation: 6,
            borderRadius: BorderRadius.circular(12),
            color: AppColors.primaryDark,
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () {
                ref.read(foregroundBannerProvider.notifier).dismiss();
                final route = event.route ?? '/notifications';
                context.push(route);
              },
              child: Padding(
                padding: const EdgeInsets.fromLTRB(14, 12, 8, 12),
                child: Row(
                  children: [
                    const Icon(Icons.notifications_active, color: Colors.white, size: 22),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            event.title,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w700,
                              fontSize: 13,
                            ),
                          ),
                          if (event.body.isNotEmpty)
                            Text(
                              event.body,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: Colors.white.withValues(alpha: 0.85),
                                fontSize: 12,
                              ),
                            ),
                        ],
                      ),
                    ),
                    IconButton(
                      visualDensity: VisualDensity.compact,
                      icon: const Icon(Icons.close, color: Colors.white70, size: 18),
                      onPressed: () =>
                          ref.read(foregroundBannerProvider.notifier).dismiss(),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
