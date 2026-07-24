import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_outbox.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../core/offline/connectivity_provider.dart';

final offlineQueueProvider = Provider<List<OfflineOperation>>((ref) {
  ref.watch(offlinePendingCountProvider);
  return OfflineOutbox.all();
});

class OfflineQueueScreen extends ConsumerWidget {
  const OfflineQueueScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final online = ref.watch(isOnlineProvider);
    final items = ref.watch(offlineQueueProvider);
    final pending = items
        .where((o) =>
            o.status == OfflineOpStatus.pending ||
            o.status == OfflineOpStatus.failed ||
            o.status == OfflineOpStatus.syncing)
        .toList();
    final conflicts = items
        .where((o) => o.status == OfflineOpStatus.conflict)
        .toList();
    final fmt = DateFormat('dd MMM, HH:mm');

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.offlineQueue),
        actions: [
          TextButton(
            onPressed: online
                ? () async {
                    final n =
                        await ref.read(offlineSyncProvider).syncNow();
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(content: Text(l10n.syncedItems(n))),
                      );
                    }
                  }
                : null,
            child: Text(l10n.syncNow),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            color: online
                ? AppColors.success.withValues(alpha: 0.08)
                : AppColors.warning.withValues(alpha: 0.12),
            child: ListTile(
              leading: Icon(
                online ? Icons.cloud_done_outlined : Icons.cloud_off_outlined,
                color: online ? AppColors.success : AppColors.warning,
              ),
              title: Text(online ? l10n.online : l10n.offline),
              subtitle: Text(
                online ? l10n.offlineOnlineHint : l10n.offlineOfflineHint,
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            l10n.pendingSection(pending.length),
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          const SizedBox(height: 8),
          if (pending.isEmpty)
            Text(l10n.nothingPending,
                style: const TextStyle(color: AppColors.textSecondary))
          else
            ...pending.map(
              (op) => Card(
                child: ListTile(
                  title: Text(op.label),
                  subtitle: Text(
                    '${fmt.format(op.createdAt.toLocal())}'
                    '${op.lastError != null ? '\n${op.lastError}' : ''}'
                    '\nAttempts: ${op.attempts} · ${op.status.name}',
                  ),
                  isThreeLine: true,
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (op.status == OfflineOpStatus.failed ||
                          op.status == OfflineOpStatus.pending)
                        IconButton(
                          tooltip: l10n.retry,
                          icon: const Icon(Icons.refresh_rounded),
                          onPressed: online
                              ? () async {
                                  await ref
                                      .read(offlineSyncProvider)
                                      .retryOne(op.id);
                                }
                              : null,
                        ),
                      IconButton(
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () async {
                          await OfflineOutbox.remove(op.id);
                          ref.read(offlinePendingCountProvider.notifier).state =
                              OfflineOutbox.length;
                        },
                      ),
                    ],
                  ),
                ),
              ),
            ),
          if (conflicts.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text(
              l10n.conflictsSection,
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
            ),
            const SizedBox(height: 4),
            Text(
              l10n.conflictsHint,
              style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
            ),
            const SizedBox(height: 8),
            ...conflicts.map(
              (op) => Card(
                color: AppColors.error.withValues(alpha: 0.05),
                child: ListTile(
                  title: Text(op.label),
                  subtitle: Text(op.lastError ?? 'Conflict'),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      IconButton(
                        tooltip: l10n.retry,
                        icon: const Icon(Icons.refresh_rounded),
                        onPressed: online
                            ? () async {
                                await ref
                                    .read(offlineSyncProvider)
                                    .retryOne(op.id);
                              }
                            : null,
                      ),
                      IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () async {
                          await OfflineOutbox.remove(op.id);
                          ref
                              .read(offlinePendingCountProvider.notifier)
                              .state = OfflineOutbox.length;
                        },
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
