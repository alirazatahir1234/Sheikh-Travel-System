import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/notifications_api.dart';
import '../domain/notification_models.dart';

enum NotificationMailbox { inbox, archived }

class NotificationsFilter {
  const NotificationsFilter({
    this.mailbox = NotificationMailbox.inbox,
    this.category,
  });

  final NotificationMailbox mailbox;
  final String? category;

  NotificationsFilter copyWith({
    NotificationMailbox? mailbox,
    String? category,
    bool clearCategory = false,
  }) =>
      NotificationsFilter(
        mailbox: mailbox ?? this.mailbox,
        category: clearCategory ? null : (category ?? this.category),
      );
}

final notificationsFilterProvider =
    StateProvider.autoDispose<NotificationsFilter>(
  (ref) => const NotificationsFilter(),
);

final notificationsListProvider =
    FutureProvider.autoDispose<List<AppNotification>>((ref) async {
  final filter = ref.watch(notificationsFilterProvider);
  final items = await ref.read(notificationsApiProvider).list(
        archived: filter.mailbox == NotificationMailbox.archived,
      );
  final cat = filter.category;
  if (cat == null || cat.isEmpty) return items;
  return items.where((n) => n.category == cat).toList();
});

final unreadNotificationsCountProvider =
    FutureProvider.autoDispose<int>((ref) async {
  ref.watch(notificationsListProvider);
  return ref.read(notificationsApiProvider).unreadCount();
});

final foregroundBannerProvider =
    StateNotifierProvider<ForegroundBannerNotifier, ForegroundBannerEvent?>(
  (ref) => ForegroundBannerNotifier(),
);

class ForegroundBannerNotifier extends StateNotifier<ForegroundBannerEvent?> {
  ForegroundBannerNotifier() : super(null);

  Timer? _timer;

  void show(ForegroundBannerEvent event) {
    _timer?.cancel();
    state = event;
    _timer = Timer(const Duration(seconds: 5), dismiss);
  }

  void dismiss() {
    _timer?.cancel();
    state = null;
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

class NotificationsActions {
  NotificationsActions(this._ref);
  final Ref _ref;

  NotificationsApi get _api => _ref.read(notificationsApiProvider);

  void _refresh() {
    _ref.invalidate(notificationsListProvider);
    _ref.invalidate(unreadNotificationsCountProvider);
  }

  Future<void> markRead(int id) async {
    await _api.markRead([id]);
    _refresh();
  }

  Future<void> markAllRead() async {
    await _api.markRead(null);
    _refresh();
  }

  Future<void> archive(int id) async {
    await _api.archive([id]);
    _refresh();
  }

  Future<void> restore(int id) async {
    await _api.restore([id]);
    _refresh();
  }

  Future<void> delete(int id) async {
    await _api.delete(id);
    _refresh();
  }
}

final notificationsActionsProvider = Provider<NotificationsActions>(
  (ref) => NotificationsActions(ref),
);
