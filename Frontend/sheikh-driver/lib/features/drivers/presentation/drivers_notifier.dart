import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/drivers_api.dart';
import '../domain/driver_models.dart';

enum DriverLicenseAlertFilter { none, expiring, expired }

String? licenseExpiryQuery(DriverLicenseAlertFilter filter) => switch (filter) {
      DriverLicenseAlertFilter.expired => 'EXPIRED',
      DriverLicenseAlertFilter.expiring => 'EXPIRING',
      DriverLicenseAlertFilter.none => null,
    };

class DriversHubState {
  const DriversHubState({
    this.stats = DriverStats.empty,
    this.drivers = const [],
    this.ranking = const [],
    this.rankingUnavailable = false,
    this.search = '',
    this.statusFilter,
    this.licenseAlert = DriverLicenseAlertFilter.none,
  });

  final DriverStats stats;
  final List<DriverListItem> drivers;
  final List<DriverRankItem> ranking;

  /// True when the ranking pool could not be loaded (show empty state, not silence).
  final bool rankingUnavailable;
  final String search;
  final String? statusFilter;
  final DriverLicenseAlertFilter licenseAlert;

  bool get hasActiveFilters =>
      search.trim().isNotEmpty ||
      statusFilter != null ||
      licenseAlert != DriverLicenseAlertFilter.none;

  List<DriverListItem> get visible {
    var list = drivers;
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
    bool? rankingUnavailable,
    String? search,
    String? statusFilter,
    bool clearStatus = false,
    DriverLicenseAlertFilter? licenseAlert,
  }) {
    return DriversHubState(
      stats: stats ?? this.stats,
      drivers: drivers ?? this.drivers,
      ranking: ranking ?? this.ranking,
      rankingUnavailable: rankingUnavailable ?? this.rankingUnavailable,
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

  Future<DriversHubState> _load({
    String? status,
    bool clearStatus = false,
    DriverLicenseAlertFilter? licenseAlert,
    String? search,
  }) async {
    final api = ref.read(driversApiProvider);
    final prev = state.valueOrNull;
    final effectiveStatus =
        clearStatus ? null : (status ?? prev?.statusFilter);
    final effectiveLicense =
        licenseAlert ?? prev?.licenseAlert ?? DriverLicenseAlertFilter.none;
    final licenseQuery = licenseExpiryQuery(effectiveLicense);

    DriverStats stats = DriverStats.empty;
    List<DriverListItem> drivers = const [];
    List<DriverListItem> rankPool = const [];
    var rankingUnavailable = false;

    await Future.wait([
      () async {
        try {
          stats = await api.stats();
        } catch (_) {}
      }(),
      () async {
        try {
          drivers = await api.list(
            status: effectiveStatus,
            licenseExpiry: licenseQuery,
          );
        } catch (_) {}
      }(),
      () async {
        // Unfiltered pool for Top Drivers (ERP-aligned client score).
        try {
          rankPool = await api.list(pageSize: 100);
        } catch (_) {
          rankingUnavailable = true;
        }
      }(),
    ]);

    final ranking = rankingUnavailable
        ? const <DriverRankItem>[]
        : buildTopDriversFromList(rankPool);

    return DriversHubState(
      stats: stats,
      drivers: drivers,
      ranking: ranking,
      rankingUnavailable: rankingUnavailable || ranking.isEmpty,
      search: search ?? prev?.search ?? '',
      statusFilter: effectiveStatus,
      licenseAlert: effectiveLicense,
    );
  }

  /// Soft reload: keep prior data visible (no full-page skeleton flash).
  Future<void> _softReload(Future<DriversHubState> Function() loader) async {
    final previous = state.valueOrNull;
    if (previous != null) {
      state = const AsyncLoading<DriversHubState>().copyWithPrevious(
        AsyncData(previous),
      );
    } else {
      state = const AsyncLoading();
    }
    state = await AsyncValue.guard(loader);
  }

  Future<void> refresh() async {
    final cur = state.valueOrNull;
    await _softReload(
      () => _load(
        status: cur?.statusFilter,
        licenseAlert: cur?.licenseAlert,
      ),
    );
  }

  void setSearch(String q) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: q));
  }

  Future<void> setLicenseAlert(DriverLicenseAlertFilter filter) async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final next = cur.licenseAlert == filter
        ? DriverLicenseAlertFilter.none
        : filter;
    await _softReload(
      () => _load(status: cur.statusFilter, licenseAlert: next),
    );
  }

  Future<void> setStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final next = (status == null || cur.statusFilter == status) ? null : status;
    await _softReload(
      () => _load(
        status: next,
        clearStatus: next == null,
        licenseAlert: cur.licenseAlert,
      ),
    );
  }

  /// Clears status, license-alert, and search (state only — UI clears its controller).
  Future<void> clearFilters() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final needsReload = cur.statusFilter != null ||
        cur.licenseAlert != DriverLicenseAlertFilter.none;
    if (!needsReload) {
      state = AsyncData(cur.copyWith(search: ''));
      return;
    }
    await _softReload(
      () => _load(
        clearStatus: true,
        licenseAlert: DriverLicenseAlertFilter.none,
        search: '',
      ),
    );
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
