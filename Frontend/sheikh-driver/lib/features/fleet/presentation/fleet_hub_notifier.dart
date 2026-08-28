import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import '../services/fleet_realtime_service.dart';
import 'fleet_vehicle_filters.dart';

class FleetHubState {
  const FleetHubState({
    this.kpis = GpsFleetStatusKpis.empty,
    this.ops = FleetOpsDashboard.empty,
    this.locations = const [],
    this.filters = FleetVehicleFilters.empty,
    this.search = '',
    this.isRefreshing = false,
    this.realtimeStatus = 'disconnected',
    this.lastFleetRefreshAt,
    this.lastGpsAt,
    this.loadError,
    this.liveFeedWarning,
  });

  final GpsFleetStatusKpis kpis;
  final FleetOpsDashboard ops;
  final List<FleetVehicleLocation> locations;
  final FleetVehicleFilters filters;
  final String search;
  final bool isRefreshing;
  /// SignalR status: connected | reconnecting | disconnected | no_token
  final String realtimeStatus;
  /// When vehicles/KPI HTTP load last succeeded (not GPS freshness).
  final DateTime? lastFleetRefreshAt;
  /// Freshest GPS timestamp across fleet (SignalR or live poll).
  final DateTime? lastGpsAt;
  /// Set when vehicles (or all fleet APIs) fail to load.
  final String? loadError;
  /// Soft warning when live positions failed but vehicle list still loaded.
  final String? liveFeedWarning;

  /// Back-compat for KPI strip / live map highlighting.
  FleetStatusFilterOption get statusFilter => filters.status;

  int get liveVehicleCount => locations
      .where(
        (v) =>
            v.status == FleetTrackStatus.moving ||
            v.status == FleetTrackStatus.idle ||
            v.status == FleetTrackStatus.parked ||
            v.status == FleetTrackStatus.sos,
      )
      .length;

  List<FleetVehicleLocation> get visible {
    var list = locations.where(filters.matches).toList();
    final q = search.trim().toLowerCase();
    if (q.isNotEmpty) {
      list = list
          .where(
            (v) =>
                v.vehicleName.toLowerCase().contains(q) ||
                v.registrationNumber.toLowerCase().contains(q) ||
                (v.driverName?.toLowerCase().contains(q) ?? false),
          )
          .toList();
    }
    return list;
  }

  FleetHubState copyWith({
    GpsFleetStatusKpis? kpis,
    FleetOpsDashboard? ops,
    List<FleetVehicleLocation>? locations,
    FleetVehicleFilters? filters,
    String? search,
    bool? isRefreshing,
    String? realtimeStatus,
    DateTime? lastFleetRefreshAt,
    DateTime? lastGpsAt,
    String? loadError,
    String? liveFeedWarning,
    bool clearLoadError = false,
    bool clearLiveFeedWarning = false,
  }) {
    return FleetHubState(
      kpis: kpis ?? this.kpis,
      ops: ops ?? this.ops,
      locations: locations ?? this.locations,
      filters: filters ?? this.filters,
      search: search ?? this.search,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      realtimeStatus: realtimeStatus ?? this.realtimeStatus,
      lastFleetRefreshAt: lastFleetRefreshAt ?? this.lastFleetRefreshAt,
      lastGpsAt: lastGpsAt ?? this.lastGpsAt,
      loadError: clearLoadError ? null : (loadError ?? this.loadError),
      liveFeedWarning: clearLiveFeedWarning
          ? null
          : (liveFeedWarning ?? this.liveFeedWarning),
    );
  }
}

final fleetHubProvider =
    AsyncNotifierProvider<FleetHubNotifier, FleetHubState>(FleetHubNotifier.new);

class FleetHubNotifier extends AsyncNotifier<FleetHubState> {
  StreamSubscription<GpsPosition>? _liveSub;
  StreamSubscription<String>? _statusSub;
  Timer? _pollTimer;

  static const _fallbackPoll = Duration(seconds: 10);

  @override
  Future<FleetHubState> build() async {
    ref.onDispose(() {
      _liveSub?.cancel();
      _statusSub?.cancel();
      _pollTimer?.cancel();
      unawaited(FleetRealtimeService.instance.disconnect());
    });
    final hub = await _load();
    _startRealtime();
    // SignalR-first: HTTP poll starts only if hub is not connected after connect attempt.
    _syncPollWithRealtime(FleetRealtimeService.instance.currentStatus);
    return hub.copyWith(
      realtimeStatus: FleetRealtimeService.instance.currentStatus,
    );
  }

  DateTime? _freshestGps(List<FleetVehicleLocation> locations) {
    DateTime? best;
    for (final v in locations) {
      final t = v.lastUpdated;
      if (t == null) continue;
      if (best == null || t.isAfter(best)) best = t;
    }
    return best;
  }

  Future<FleetHubState> _load() async {
    if (!ref.read(authRepositoryProvider).isLoggedIn) {
      return const FleetHubState(loadError: 'Not signed in');
    }
    final api = ref.read(fleetApiProvider);
    GpsFleetStatusKpis apiKpis = GpsFleetStatusKpis.empty;
    FleetOpsDashboard ops = FleetOpsDashboard.empty;
    List<VehicleListItem> vehicles = const [];
    List<GpsPosition> live = const [];
    String? vehiclesError;
    String? liveFeedWarning;

    await Future.wait([
      () async {
        try {
          apiKpis = await api.getFleetStatusLocal();
        } catch (_) {}
      }(),
      () async {
        try {
          ops = await api.getOpsDashboard();
        } catch (_) {}
      }(),
      () async {
        try {
          vehicles = await api.getVehicles();
        } catch (e) {
          vehiclesError = e.toString();
        }
      }(),
      () async {
        try {
          live = await api.getLivePositions();
        } catch (e) {
          liveFeedWarning =
              'Live GPS feed unavailable. Showing last known vehicle data. ($e)';
        }
      }(),
    ]);

    final locations = mergeVehiclesWithLive(vehicles: vehicles, live: live);
    // Always derive strip counts from the same rows the list renders.
    final kpis = deriveFleetKpisFromLocations(
      locations,
      alertsToday: apiKpis.alertsToday,
    );
    final loadError = vehiclesError == null
        ? null
        : 'Vehicles API failed. Pull to refresh. ($vehiclesError)';
    final now = DateTime.now();
    return FleetHubState(
      kpis: kpis,
      ops: ops,
      locations: locations,
      loadError: loadError,
      liveFeedWarning: liveFeedWarning,
      lastFleetRefreshAt: now,
      lastGpsAt: _freshestGps(locations),
    );
  }

  void _startRealtime() {
    if (!ref.read(authRepositoryProvider).isLoggedIn) return;
    unawaited(FleetRealtimeService.instance.connect());
    _liveSub?.cancel();
    _liveSub = FleetRealtimeService.instance.locationUpdates.listen((pos) {
      final current = state.valueOrNull;
      if (current == null) return;
      final locations = applyLiveUpdate(current.locations, pos);
      state = AsyncData(
        current.copyWith(
          locations: locations,
          kpis: deriveFleetKpisFromLocations(
            locations,
            alertsToday: current.kpis.alertsToday,
          ),
          lastGpsAt: pos.timestamp,
          realtimeStatus: 'connected',
          clearLiveFeedWarning: true,
        ),
      );
    });
    _statusSub?.cancel();
    _statusSub = FleetRealtimeService.instance.connectionStatus.listen((status) {
      final current = state.valueOrNull;
      if (current != null) {
        state = AsyncData(current.copyWith(realtimeStatus: status));
      }
      _syncPollWithRealtime(status);
      if (status == 'connected') {
        // One quiet resync after reconnect, then SignalR-only.
        unawaited(refreshQuiet());
      }
    });
  }

  void _syncPollWithRealtime(String status) {
    final needPoll = status != 'connected';
    if (!needPoll) {
      _pollTimer?.cancel();
      _pollTimer = null;
      return;
    }
    if (_pollTimer != null) return;
    _pollTimer = Timer.periodic(_fallbackPoll, (_) {
      unawaited(refreshQuiet());
    });
  }

  Future<void> refresh() async {
    final previous = state.valueOrNull;
    if (previous != null) {
      state = AsyncData(previous.copyWith(isRefreshing: true));
    }
    try {
      final hub = await _load();
      final current = state.valueOrNull;
      state = AsyncData(
        hub.copyWith(
          filters: current?.filters,
          search: current?.search,
          isRefreshing: false,
          realtimeStatus: FleetRealtimeService.instance.currentStatus,
          clearLoadError: hub.loadError == null,
          loadError: hub.loadError,
          clearLiveFeedWarning: hub.liveFeedWarning == null,
          liveFeedWarning: hub.liveFeedWarning,
        ),
      );
      _startRealtime();
    } catch (e, st) {
      if (previous != null) {
        state = AsyncData(previous.copyWith(isRefreshing: false));
      } else {
        state = AsyncError(e, st);
      }
    }
  }

  Future<void> refreshQuiet() async {
    try {
      final hub = await _load();
      final current = state.valueOrNull;
      state = AsyncData(
        hub.copyWith(
          filters: current?.filters,
          search: current?.search,
          isRefreshing: false,
          realtimeStatus: FleetRealtimeService.instance.currentStatus,
          clearLoadError: hub.loadError == null,
          loadError: hub.loadError,
          clearLiveFeedWarning: hub.liveFeedWarning == null,
          liveFeedWarning: hub.liveFeedWarning,
        ),
      );
    } catch (_) {}
  }

  /// KPI strip shortcut — tap again on the same status clears status filter.
  void setStatusFilter(FleetStatusFilterOption option) {
    final current = state.valueOrNull;
    if (current == null) return;
    final next = current.filters.status == option
        ? FleetStatusFilterOption.all
        : option;
    state = AsyncData(
      current.copyWith(filters: current.filters.copyWith(status: next)),
    );
  }

  void setFilters(FleetVehicleFilters filters) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(filters: filters));
  }

  void clearFilters() {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(filters: FleetVehicleFilters.empty));
  }

  void setSearch(String q) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(search: q));
  }
}

final vehicleDetailProvider =
    FutureProvider.family<VehicleDetail, int>((ref, id) {
  return ref.read(fleetApiProvider).getVehicle(id);
});
