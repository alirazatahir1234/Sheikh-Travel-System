import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import '../services/fleet_realtime_service.dart';

class FleetHubState {
  const FleetHubState({
    this.kpis = GpsFleetStatusKpis.empty,
    this.ops = FleetOpsDashboard.empty,
    this.locations = const [],
    this.filter,
    this.search = '',
    this.isRefreshing = false,
    this.realtimeStatus = 'disconnected',
    this.lastLiveAt,
  });

  final GpsFleetStatusKpis kpis;
  final FleetOpsDashboard ops;
  final List<FleetVehicleLocation> locations;
  final FleetTrackStatus? filter;
  final String search;
  final bool isRefreshing;
  /// SignalR status: connected | reconnecting | disconnected | no_token
  final String realtimeStatus;
  /// Last SignalR location event or successful HTTP poll.
  final DateTime? lastLiveAt;

  List<FleetVehicleLocation> get visible {
    var list = locations;
    if (filter != null) {
      list = list.where((v) => v.status == filter).toList();
    }
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
    FleetTrackStatus? filter,
    bool clearFilter = false,
    String? search,
    bool? isRefreshing,
    String? realtimeStatus,
    DateTime? lastLiveAt,
  }) {
    return FleetHubState(
      kpis: kpis ?? this.kpis,
      ops: ops ?? this.ops,
      locations: locations ?? this.locations,
      filter: clearFilter ? null : (filter ?? this.filter),
      search: search ?? this.search,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      realtimeStatus: realtimeStatus ?? this.realtimeStatus,
      lastLiveAt: lastLiveAt ?? this.lastLiveAt,
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
      lastLiveAt: DateTime.now(),
    );
  }

  Future<FleetHubState> _load() async {
    if (!ref.read(authRepositoryProvider).isLoggedIn) {
      return const FleetHubState();
    }
    final api = ref.read(fleetApiProvider);
    GpsFleetStatusKpis kpis = GpsFleetStatusKpis.empty;
    FleetOpsDashboard ops = FleetOpsDashboard.empty;
    List<VehicleListItem> vehicles = const [];
    List<GpsPosition> live = const [];

    await Future.wait([
      () async {
        try {
          kpis = await api.getFleetStatusLocal();
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
        } catch (_) {}
      }(),
      () async {
        try {
          live = await api.getLivePositions();
        } catch (_) {}
      }(),
    ]);

    final locations = mergeVehiclesWithLive(vehicles: vehicles, live: live);
    return FleetHubState(kpis: kpis, ops: ops, locations: locations);
  }

  void _startRealtime() {
    if (!ref.read(authRepositoryProvider).isLoggedIn) return;
    unawaited(FleetRealtimeService.instance.connect());
    _liveSub?.cancel();
    _liveSub = FleetRealtimeService.instance.locationUpdates.listen((pos) {
      final current = state.valueOrNull;
      if (current == null) return;
      state = AsyncData(
        current.copyWith(
          locations: applyLiveUpdate(current.locations, pos),
          lastLiveAt: DateTime.now(),
          realtimeStatus: 'connected',
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
          filter: current?.filter,
          search: current?.search,
          isRefreshing: false,
          realtimeStatus: FleetRealtimeService.instance.currentStatus,
          lastLiveAt: DateTime.now(),
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
          filter: current?.filter,
          search: current?.search,
          isRefreshing: false,
          realtimeStatus: FleetRealtimeService.instance.currentStatus,
          lastLiveAt: DateTime.now(),
        ),
      );
    } catch (_) {}
  }

  void setFilter(FleetTrackStatus? status) {
    final current = state.valueOrNull;
    if (current == null) return;
    if (status == null || current.filter == status) {
      state = AsyncData(current.copyWith(clearFilter: true));
    } else {
      state = AsyncData(current.copyWith(filter: status));
    }
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
