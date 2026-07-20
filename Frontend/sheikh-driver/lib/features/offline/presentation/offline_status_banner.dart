import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/connectivity_provider.dart';
import '../../../core/offline/offline_sync_service.dart';

/// Compact status strip: offline warning and/or pending sync count.
class OfflineStatusBanner extends ConsumerWidget {
  const OfflineStatusBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final online = ref.watch(isOnlineProvider);
    final pending = ref.watch(offlinePendingCountProvider);
    if (online && pending == 0) return const SizedBox.shrink();

    final color = online ? AppColors.accent : AppColors.warning;
    final text = !online
        ? 'You are offline${pending > 0 ? ' · $pending queued' : ''}'
        : '$pending action${pending == 1 ? '' : 's'} waiting to sync';

    return Material(
      color: color,
      child: InkWell(
        onTap: () => context.push('/offline-queue'),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: Row(
            children: [
              Icon(
                online ? Icons.sync : Icons.cloud_off,
                color: Colors.white,
                size: 18,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  text,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              if (online && pending > 0)
                TextButton(
                  onPressed: () => ref.read(offlineSyncProvider).syncNow(),
                  style: TextButton.styleFrom(
                    foregroundColor: Colors.white,
                    visualDensity: VisualDensity.compact,
                  ),
                  child: const Text('Sync'),
                ),
              const Icon(Icons.chevron_right, color: Colors.white70, size: 18),
            ],
          ),
        ),
      ),
    );
  }
}
