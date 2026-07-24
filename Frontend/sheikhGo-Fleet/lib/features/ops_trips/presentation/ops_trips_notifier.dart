import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/ops_trips_api.dart';
import '../domain/ops_trip_models.dart';

class OpsTripsState {
  const OpsTripsState({
    this.dashboard = OpsTripsDashboard.empty,
    this.trips = const [],
    this.liveOnly = false,
    this.statusFilter,
    this.search = '',
  });

  final OpsTripsDashboard dashboard;
  final List<OpsTripListItem> trips;
  final bool liveOnly;
  final String? statusFilter;
  final String search;

  List<OpsTripListItem> get visible {
    var list = trips;
    final q = search.trim().toLowerCase();
    if (q.isNotEmpty) {
      list = list
          .where(
            (t) =>
                t.tripNumber.toLowerCase().contains(q) ||
                (t.customerName?.toLowerCase().contains(q) ?? false) ||
                (t.driverName?.toLowerCase().contains(q) ?? false) ||
                (t.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (t.pickupAddress?.toLowerCase().contains(q) ?? false),
          )
          .toList();
    }
    return list;
  }

  OpsTripsState copyWith({
    OpsTripsDashboard? dashboard,
    List<OpsTripListItem>? trips,
    bool? liveOnly,
    String? statusFilter,
    bool clearStatus = false,
    String? search,
  }) {
    return OpsTripsState(
      dashboard: dashboard ?? this.dashboard,
      trips: trips ?? this.trips,
      liveOnly: liveOnly ?? this.liveOnly,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      search: search ?? this.search,
    );
  }
}

final opsTripsProvider =
    AsyncNotifierProvider<OpsTripsNotifier, OpsTripsState>(OpsTripsNotifier.new);

class OpsTripsNotifier extends AsyncNotifier<OpsTripsState> {
  @override
  Future<OpsTripsState> build() => _load();

  Future<OpsTripsState> _load({
    bool? liveOnly,
    String? status,
    bool clearStatus = false,
  }) async {
    final api = ref.read(opsTripsApiProvider);
    final prev = state.valueOrNull;
    final useLive = liveOnly ?? prev?.liveOnly ?? false;
    final useStatus =
        clearStatus ? null : (status ?? prev?.statusFilter);

    OpsTripsDashboard dash = OpsTripsDashboard.empty;
    List<OpsTripListItem> trips = const [];

    await Future.wait([
      () async {
        try {
          dash = await api.dashboard();
        } catch (_) {}
      }(),
      () async {
        try {
          trips = useLive
              ? await api.live()
              : await api.list(status: useStatus, todayOnly: false);
        } catch (_) {}
      }(),
    ]);

    return OpsTripsState(
      dashboard: dash,
      trips: trips,
      liveOnly: useLive,
      statusFilter: useStatus,
      search: prev?.search ?? '',
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_load);
  }

  void setSearch(String q) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: q));
  }

  Future<void> setLiveOnly(bool live) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(liveOnly: live));
  }

  Future<void> setStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    final next = (status == null || cur?.statusFilter == status) ? null : status;
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => _load(
        liveOnly: false,
        status: next,
        clearStatus: next == null,
      ),
    );
  }
}

final opsTripDetailProvider =
    FutureProvider.family<OpsTripDetail, int>((ref, id) {
  return ref.read(opsTripsApiProvider).getById(id);
});
