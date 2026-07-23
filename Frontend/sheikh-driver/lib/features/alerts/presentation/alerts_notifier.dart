import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/gps_alerts_api.dart';
import '../domain/gps_alert_models.dart';

class AlertsState {
  const AlertsState({
    this.stats = GpsAlertStats.empty,
    this.events = const [],
    this.statusFilter,
    this.readStateFilter,
    this.severityFilter,
    this.datePreset = 'today',
  });

  final GpsAlertStats stats;
  final List<GpsAlertEvent> events;
  final String? statusFilter;
  final String? readStateFilter;
  final String? severityFilter;
  final String datePreset;

  List<GpsAlertEvent> get visible => events;

  AlertsState copyWith({
    GpsAlertStats? stats,
    List<GpsAlertEvent>? events,
    String? statusFilter,
    String? readStateFilter,
    String? severityFilter,
    String? datePreset,
    bool clearStatus = false,
    bool clearReadState = false,
    bool clearSeverity = false,
  }) {
    return AlertsState(
      stats: stats ?? this.stats,
      events: events ?? this.events,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      readStateFilter:
          clearReadState ? null : (readStateFilter ?? this.readStateFilter),
      severityFilter:
          clearSeverity ? null : (severityFilter ?? this.severityFilter),
      datePreset: datePreset ?? this.datePreset,
    );
  }
}

final alertsProvider =
    AsyncNotifierProvider<AlertsNotifier, AlertsState>(AlertsNotifier.new);

class AlertsNotifier extends AsyncNotifier<AlertsState> {
  @override
  Future<AlertsState> build() => _load();

  Future<AlertsState> _load({
    String? statusFilter,
    String? readStateFilter,
    String? severityFilter,
    String? datePreset,
  }) async {
    final api = ref.read(gpsAlertsApiProvider);
    final prev = state.valueOrNull;
    final selectedStatus = statusFilter ?? prev?.statusFilter;
    final selectedReadState = readStateFilter ?? prev?.readStateFilter;
    final selectedSeverity = severityFilter ?? prev?.severityFilter;
    final selectedDatePreset = datePreset ?? prev?.datePreset ?? 'today';

    GpsAlertStats stats = GpsAlertStats.empty;
    List<GpsAlertEvent> events = const [];
    await Future.wait([
      () async {
        try {
          stats = await api.stats();
        } catch (_) {}
      }(),
      () async {
        try {
          events = await api.listEvents(
            status: selectedStatus,
            readState: selectedReadState,
            severity: selectedSeverity,
            datePreset: selectedDatePreset,
          );
        } catch (_) {}
      }(),
    ]);

    return AlertsState(
      stats: stats,
      events: events,
      statusFilter: selectedStatus,
      readStateFilter: selectedReadState,
      severityFilter: selectedSeverity,
      datePreset: selectedDatePreset,
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_load);
  }

  Future<void> setLifecycle(String? lifecycle) async {
    state = const AsyncLoading();
    final readState = switch (lifecycle) {
      'unread' => 'unread',
      'read' => 'read',
      _ => null,
    };
    final status = switch (lifecycle) {
      'acknowledged' => 'acknowledged',
      'resolved' => 'resolved',
      'archived' => 'archived',
      _ => null,
    };
    state = await AsyncValue.guard(
      () => _load(
        statusFilter: status,
        readStateFilter: readState,
      ),
    );
  }

  Future<void> setSeverity(String? severity) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => _load(severityFilter: severity),
    );
  }

  Future<void> setDatePreset(String datePreset) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(datePreset: datePreset));
  }

  Future<void> acknowledge(int id) async {
    await ref.read(gpsAlertsApiProvider).acknowledge(id);
    await refresh();
  }

  Future<void> markRead(int id) async {
    await ref.read(gpsAlertsApiProvider).markRead(id);
    await refresh();
  }

  Future<void> resolve(int id, {String? notes}) async {
    await ref.read(gpsAlertsApiProvider).resolve(id, notes: notes);
    await refresh();
  }

  Future<void> archive(int id, {String? reason}) async {
    await ref.read(gpsAlertsApiProvider).archive(id, reason: reason);
    await refresh();
  }
}

final alertDetailProvider =
    FutureProvider.family<GpsAlertEvent, int>((ref, id) {
  return ref.read(gpsAlertsApiProvider).getById(id);
});
