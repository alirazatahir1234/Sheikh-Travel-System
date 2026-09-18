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
    this.datePreset = 'last7',
    this.loadError,
  });

  final GpsAlertStats stats;
  final List<GpsAlertEvent> events;
  final String? statusFilter;
  final String? readStateFilter;
  final String? severityFilter;

  /// `all` = no date filter; otherwise API datePreset (`today`, `last7`, …).
  final String datePreset;
  final String? loadError;

  List<GpsAlertEvent> get visible => events;

  AlertsState copyWith({
    GpsAlertStats? stats,
    List<GpsAlertEvent>? events,
    String? statusFilter,
    String? readStateFilter,
    String? severityFilter,
    String? datePreset,
    String? loadError,
    bool clearStatus = false,
    bool clearReadState = false,
    bool clearSeverity = false,
    bool clearLoadError = false,
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
      loadError: clearLoadError ? null : (loadError ?? this.loadError),
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
    bool clearStatus = false,
    bool clearReadState = false,
    bool clearSeverity = false,
  }) async {
    final api = ref.read(gpsAlertsApiProvider);
    final prev = state.valueOrNull;
    final selectedStatus =
        clearStatus ? null : (statusFilter ?? prev?.statusFilter);
    final selectedReadState =
        clearReadState ? null : (readStateFilter ?? prev?.readStateFilter);
    final selectedSeverity =
        clearSeverity ? null : (severityFilter ?? prev?.severityFilter);
    final selectedDatePreset = datePreset ?? prev?.datePreset ?? 'last7';
    final apiDatePreset =
        selectedDatePreset == 'all' ? null : selectedDatePreset;

    GpsAlertStats stats = prev?.stats ?? GpsAlertStats.empty;
    String? softError;
    try {
      stats = await api.stats();
    } catch (e) {
      softError = e.toString();
    }

    final events = await api.listEvents(
      status: selectedStatus,
      readState: selectedReadState,
      severity: selectedSeverity,
      datePreset: apiDatePreset,
    );

    return AlertsState(
      stats: stats,
      events: events,
      statusFilter: selectedStatus,
      readStateFilter: selectedReadState,
      severityFilter: selectedSeverity,
      datePreset: selectedDatePreset,
      loadError: softError,
    );
  }

  Future<void> _run(Future<AlertsState> Function() loader) async {
    final previous = state.valueOrNull;
    state = previous != null
        ? AsyncLoading<AlertsState>().copyWithPrevious(AsyncData(previous))
        : const AsyncLoading();
    state = await AsyncValue.guard(loader);
  }

  Future<void> refresh() => _run(_load);

  Future<void> setLifecycle(String? lifecycle) {
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
    final clearing = lifecycle == null;
    return _run(
      () => _load(
        statusFilter: status,
        readStateFilter: readState,
        clearStatus: clearing,
        clearReadState: clearing,
      ),
    );
  }

  Future<void> setSeverity(String? severity) {
    return _run(
      () => _load(
        severityFilter: severity,
        clearSeverity: severity == null,
      ),
    );
  }

  Future<void> setDatePreset(String datePreset) =>
      _run(() => _load(datePreset: datePreset));

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
