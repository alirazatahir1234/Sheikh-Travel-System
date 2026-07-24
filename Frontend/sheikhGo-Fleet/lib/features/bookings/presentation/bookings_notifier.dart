import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/bookings_api.dart';
import '../domain/booking_models.dart';

class BookingsState {
  const BookingsState({
    this.items = const [],
    this.statusFilter,
    this.search = '',
    this.queueOnly = true,
  });

  final List<BookingListItem> items;
  final String? statusFilter;
  final String search;
  final bool queueOnly;

  List<BookingListItem> get visible {
    var list = items;
    if (queueOnly) {
      list = list
          .where(
            (b) =>
                b.status.toLowerCase() == 'pending' ||
                b.status.toLowerCase() == 'confirmed',
          )
          .toList();
    }
    if (statusFilter != null && statusFilter!.isNotEmpty) {
      list = list
          .where((b) => b.status.toLowerCase() == statusFilter!.toLowerCase())
          .toList();
    }
    final q = search.trim().toLowerCase();
    if (q.isNotEmpty) {
      list = list
          .where(
            (b) =>
                b.bookingNumber.toLowerCase().contains(q) ||
                (b.customerName?.toLowerCase().contains(q) ?? false) ||
                (b.routeName?.toLowerCase().contains(q) ?? false) ||
                (b.driverName?.toLowerCase().contains(q) ?? false),
          )
          .toList();
    }
    return list;
  }

  int get pendingCount =>
      items.where((b) => b.status.toLowerCase() == 'pending').length;
  int get confirmedCount =>
      items.where((b) => b.status.toLowerCase() == 'confirmed').length;
  int get unassignedCount =>
      items.where((b) => b.needsDispatch && b.isUnassigned).length;

  BookingsState copyWith({
    List<BookingListItem>? items,
    String? statusFilter,
    bool clearStatus = false,
    String? search,
    bool? queueOnly,
  }) {
    return BookingsState(
      items: items ?? this.items,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      search: search ?? this.search,
      queueOnly: queueOnly ?? this.queueOnly,
    );
  }
}

final bookingsProvider =
    AsyncNotifierProvider<BookingsNotifier, BookingsState>(BookingsNotifier.new);

class BookingsNotifier extends AsyncNotifier<BookingsState> {
  @override
  Future<BookingsState> build() => _load();

  Future<BookingsState> _load() async {
    final items = await ref.read(bookingsApiProvider).list();
    final prev = state.valueOrNull;
    return BookingsState(
      items: items,
      statusFilter: prev?.statusFilter,
      search: prev?.search ?? '',
      queueOnly: prev?.queueOnly ?? true,
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_load);
  }

  void setSearch(String value) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: value));
  }

  void setStatusFilter(String? status) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    if (status == null || status == cur.statusFilter) {
      state = AsyncData(cur.copyWith(clearStatus: true));
    } else {
      state = AsyncData(cur.copyWith(statusFilter: status, queueOnly: false));
    }
  }

  void setQueueOnly(bool value) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(
      cur.copyWith(queueOnly: value, clearStatus: value),
    );
  }
}

final bookingDetailProvider =
    FutureProvider.family<BookingDetail, int>((ref, id) {
  return ref.watch(bookingsApiProvider).getById(id);
});
