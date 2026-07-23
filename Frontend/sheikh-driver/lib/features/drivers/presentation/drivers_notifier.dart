import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/drivers_api.dart';
import '../domain/driver_models.dart';

enum DriverLicenseAlertFilter { none, expiring, expired }

class DriversHubState {
  const DriversHubState({
    this.stats = DriverStats.empty,
    this.drivers = const [],
    this.ranking = const [],
    this.search = '',
    this.statusFilter,
    this.licenseAlert = DriverLicenseAlertFilter.none,
  });

  final DriverStats stats;
  final List<DriverListItem> drivers;
  final List<DriverRankItem> ranking;
  final String search;
  final String? statusFilter;
  final DriverLicenseAlertFilter licenseAlert;

  List<DriverListItem> get visible {
    var list = drivers;
    switch (licenseAlert) {
      case DriverLicenseAlertFilter.expired:
        list = list.where((d) => d.licenseExpired).toList();
      case DriverLicenseAlertFilter.expiring:
        list = list
            .where((d) => d.licenseExpiringSoon && !d.licenseExpired)
            .toList();
      case DriverLicenseAlertFilter.none:
        break;
    }
    final q = search.trim().toLowerCase();
    if (q.isNotEmpty) {
      list = list
          .where(
            (d) =>
                d.fullName.toLowerCase().contains(q) ||
                d.phone.contains(q) ||
                d.licenseNumber.toLowerCase().contains(q) ||
                (d.assignedVehicleRegistration?.toLowerCase().contains(q) ??
                    false),
          )
          .toList();
    }
    return list;
  }

  DriversHubState copyWith({
    DriverStats? stats,
    List<DriverListItem>? drivers,
    List<DriverRankItem>? ranking,
    String? search,
    String? statusFilter,
    bool clearStatus = false,
    DriverLicenseAlertFilter? licenseAlert,
  }) {
    return DriversHubState(
      stats: stats ?? this.stats,
      drivers: drivers ?? this.drivers,
      ranking: ranking ?? this.ranking,
      search: search ?? this.search,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      licenseAlert: licenseAlert ?? this.licenseAlert,
    );
  }
}

final driversHubProvider =
    AsyncNotifierProvider<DriversHubNotifier, DriversHubState>(
  DriversHubNotifier.new,
);

class DriversHubNotifier extends AsyncNotifier<DriversHubState> {
  @override
  Future<DriversHubState> build() => _load();

  Future<DriversHubState> _load({String? status}) async {
    final api = ref.read(driversApiProvider);
    DriverStats stats = DriverStats.empty;
    List<DriverListItem> drivers = const [];
    List<DriverRankItem> ranking = const [];
    await Future.wait([
      () async {
        try {
          stats = await api.stats();
        } catch (_) {}
      }(),
      () async {
        try {
          drivers = await api.list(status: status);
        } catch (_) {}
      }(),
      () async {
        try {
          ranking = await api.ranking();
        } catch (_) {}
      }(),
    ]);
    final prev = state.valueOrNull;
    return DriversHubState(
      stats: stats,
      drivers: drivers,
      ranking: ranking,
      search: prev?.search ?? '',
      statusFilter: status ?? prev?.statusFilter,
      licenseAlert: prev?.licenseAlert ?? DriverLicenseAlertFilter.none,
    );
  }

  Future<void> refresh() async {
    final filter = state.valueOrNull?.statusFilter;
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(status: filter));
  }

  void setSearch(String q) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: q));
  }

  void setLicenseAlert(DriverLicenseAlertFilter filter) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final next = cur.licenseAlert == filter
        ? DriverLicenseAlertFilter.none
        : filter;
    state = AsyncData(cur.copyWith(licenseAlert: next));
  }

  Future<void> setStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final next = (status == null || cur.statusFilter == status) ? null : status;
    state = AsyncData(cur.copyWith(statusFilter: next, clearStatus: next == null));
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(status: next));
  }
}

final driverDetailProvider =
    FutureProvider.family<DriverDetail, int>((ref, id) {
  return ref.read(driversApiProvider).getById(id);
});

final driverPerformanceProvider =
    FutureProvider.family<DriverPerformanceSummary?, int>((ref, id) async {
  try {
    return await ref.read(driversApiProvider).performance(id);
  } catch (_) {
    return null;
  }
});

final driverViolationsProvider =
    FutureProvider.family<List<DriverViolation>, int>((ref, id) async {
  try {
    return await ref.read(driversApiProvider).violations(id);
  } catch (_) {
    return const [];
  }
});

final driverAttendanceProvider =
    FutureProvider.family<List<DriverAttendanceRow>, int>((ref, id) async {
  try {
    return await ref.read(driversApiProvider).attendance(id);
  } catch (_) {
    return const [];
  }
});

final driverDocumentsProvider =
    FutureProvider.family<List<DriverDocumentItem>, int>((ref, id) async {
  try {
    return await ref.read(driversApiProvider).documents(id);
  } catch (_) {
    return const [];
  }
});
