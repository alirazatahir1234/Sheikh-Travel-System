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
  ref.watch(offlineOutboxRevisionProvider);
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
    final exhausted = pending.where(isOfflineRetryExhausted).toList();
    final conflicts = items
        .where((o) => o.status == OfflineOpStatus.conflict)
        .toList();
    final fmt = DateFormat('dd MMM, HH:mm');
    final p = context.sg;

    return Scaffold(
      backgroundColor: p.surface,
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
          if (exhausted.isNotEmpty) ...[
            const SizedBox(height: 12),
            Card(
              color: AppColors.error.withValues(alpha: 0.08),
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${exhausted.length} item${exhausted.length == 1 ? '' : 's'} stopped after too many retries',
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: p.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'These actions will not sync automatically. Retry when you are online, or remove them if they are no longer needed.',
                      style: TextStyle(
                        fontSize: 13,
                        height: 1.35,
                        color: p.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: online
                          ? () async {
                              final n = await ref
                                  .read(offlineSyncProvider)
                                  .retryAllFailed();
                              if (context.mounted) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text(
                                      n > 0
                                          ? l10n.syncedItems(n)
                                          : l10n.retryStarted,
                                    ),
                                  ),
                                );
                              }
                            }
                          : null,
                      icon: const Icon(Icons.refresh_rounded, size: 18),
                      label: Text(l10n.retryAllFailed),
                    ),
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: 16),
          Text(
            l10n.pendingSection(pending.length),
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          const SizedBox(height: 8),
          if (pending.isEmpty)
            Text(l10n.nothingPending,
                style: TextStyle(color: p.textSecondary))
          else
            ...pending.map(
              (op) {
                final stopped = isOfflineRetryExhausted(op);
                return Card(
                  color: stopped
                      ? AppColors.error.withValues(alpha: 0.05)
                      : null,
                  child: ListTile(
                    title: Text(op.label),
                    subtitle: Text(
                      '${fmt.format(op.createdAt.toLocal())}'
                      '${stopped ? '\nStopped after ${op.attempts} attempts — tap retry to try again' : ''}'
                      '${op.lastError != null ? '\n${op.lastError}' : ''}'
                      '\nAttempts: ${op.attempts} · ${op.status.name}',
                    ),
                    isThreeLine: true,
                    trailing: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        if (op.status == OfflineOpStatus.failed ||
                            op.status == OfflineOpStatus.pending ||
                            stopped)
                          IconButton(
                            tooltip: stopped ? 'Reset & retry' : l10n.retry,
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
                            ref
                                .read(offlinePendingCountProvider.notifier)
                                .state = OfflineOutbox.length;
                            ref
                                .read(offlineOutboxRevisionProvider.notifier)
                                .state++;
                          },
                        ),
                      ],
                    ),
                  ),
                );
              },
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
              style: TextStyle(color: p.textSecondary, fontSize: 12),
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
                          ref
                              .read(offlineOutboxRevisionProvider.notifier)
                              .state++;
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
