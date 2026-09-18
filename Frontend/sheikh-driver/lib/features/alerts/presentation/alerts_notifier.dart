import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../../notifications/domain/notification_models.dart';
import '../../notifications/presentation/notifications_notifier.dart';
import '../data/gps_alerts_api.dart';
import '../domain/alert_display.dart';
import '../domain/gps_alert_models.dart';
import 'alerts_simulator.dart';

class AlertsState {
  const AlertsState({
    this.stats = GpsAlertStats.empty,
    this.events = const [],
    this.demoEvents = const [],
    this.statusFilter,
    this.readStateFilter,
    this.severityFilter,
    this.eventTypeFilter,
    this.categoryFilter = AlertCategory.all,
    this.searchQuery = '',
    this.datePreset = 'last7',
    this.loadError,
  });

  final GpsAlertStats stats;
  final List<GpsAlertEvent> events;
  final List<GpsAlertEvent> demoEvents;
  final String? statusFilter;
  final String? readStateFilter;
  final String? severityFilter;
  final String? eventTypeFilter;
  final AlertCategory categoryFilter;
  final String searchQuery;

  /// `all` = no date filter; otherwise API datePreset (`today`, `last7`, …).
  final String datePreset;

  /// Soft error (e.g. stats failed) while events may still be present.
  final String? loadError;

  List<GpsAlertEvent> get mergedEvents {
    final byId = <int, GpsAlertEvent>{};
    for (final e in events) {
      byId[e.id] = e;
    }
    for (final e in demoEvents) {
      byId[e.id] = e;
    }
    final list = byId.values.toList()
      ..sort((a, b) => b.timestamp.compareTo(a.timestamp));
    return list;
  }

  List<GpsAlertEvent> get visible {
    return mergedEvents
        .where((e) => matchesAlertCategory(e, categoryFilter))
        .where((e) => alertMatchesSearch(e, searchQuery))
        .toList();
  }

  AlertsFeedSummary get feedSummary =>
      AlertsFeedSummary.fromEvents(mergedEvents);

  Map<AlertCategory, int> get categoryCounts {
    final counts = {for (final c in AlertCategoryX.pillOrder) c: 0};
    for (final e in mergedEvents) {
      counts[AlertCategory.all] = (counts[AlertCategory.all] ?? 0) + 1;
      if (matchesAlertCategory(e, AlertCategory.critical)) {
        counts[AlertCategory.critical] =
            (counts[AlertCategory.critical] ?? 0) + 1;
      }
      final cat = alertCategoryFor(e);
      if (cat != AlertCategory.other && counts.containsKey(cat)) {
        counts[cat] = (counts[cat] ?? 0) + 1;
      }
    }
    return counts;
  }

  int get activeFilterCount {
    var n = 0;
    if (statusFilter != null || readStateFilter != null) n++;
    if (severityFilter != null) n++;
    if (categoryFilter != AlertCategory.all) n++;
    if (datePreset != 'last7') n++;
    if (searchQuery.trim().isNotEmpty) n++;
    return n;
  }

  bool isDemo(int id) => demoEvents.any((e) => e.id == id);

  AlertsState copyWith({
    GpsAlertStats? stats,
    List<GpsAlertEvent>? events,
    List<GpsAlertEvent>? demoEvents,
    String? statusFilter,
    String? readStateFilter,
    String? severityFilter,
    String? eventTypeFilter,
    AlertCategory? categoryFilter,
    String? searchQuery,
    String? datePreset,
    String? loadError,
    bool clearStatus = false,
    bool clearReadState = false,
    bool clearSeverity = false,
    bool clearEventType = false,
    bool clearLoadError = false,
  }) {
    return AlertsState(
      stats: stats ?? this.stats,
      events: events ?? this.events,
      demoEvents: demoEvents ?? this.demoEvents,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      readStateFilter:
          clearReadState ? null : (readStateFilter ?? this.readStateFilter),
      severityFilter:
          clearSeverity ? null : (severityFilter ?? this.severityFilter),
      eventTypeFilter:
          clearEventType ? null : (eventTypeFilter ?? this.eventTypeFilter),
      categoryFilter: categoryFilter ?? this.categoryFilter,
      searchQuery: searchQuery ?? this.searchQuery,
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
    String? eventTypeFilter,
    String? datePreset,
    bool clearStatus = false,
    bool clearReadState = false,
    bool clearSeverity = false,
    bool clearEventType = false,
  }) async {
    if (!ref.read(authRepositoryProvider).isLoggedIn) {
      return const AlertsState();
    }
    final api = ref.read(gpsAlertsApiProvider);
    final prev = state.valueOrNull;
    final selectedStatus =
        clearStatus ? null : (statusFilter ?? prev?.statusFilter);
    final selectedReadState =
        clearReadState ? null : (readStateFilter ?? prev?.readStateFilter);
    final selectedSeverity =
        clearSeverity ? null : (severityFilter ?? prev?.severityFilter);
    final selectedEventType =
        clearEventType ? null : (eventTypeFilter ?? prev?.eventTypeFilter);
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
      eventType: selectedEventType,
      datePreset: apiDatePreset,
    );

    return AlertsState(
      stats: stats,
      events: events,
      demoEvents: prev?.demoEvents ?? const [],
      statusFilter: selectedStatus,
      readStateFilter: selectedReadState,
      severityFilter: selectedSeverity,
      eventTypeFilter: selectedEventType,
      categoryFilter: prev?.categoryFilter ?? AlertCategory.all,
      searchQuery: prev?.searchQuery ?? '',
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

  void setSearchQuery(String query) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(searchQuery: query));
  }

  void setCategoryFilter(AlertCategory category) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(categoryFilter: category));
  }

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

  Future<void> setEventType(String? eventType) {
    return _run(
      () => _load(
        eventTypeFilter: eventType,
        clearEventType: eventType == null,
      ),
    );
  }

  Future<void> clearAdvancedFilters() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(
      cur.copyWith(
        categoryFilter: AlertCategory.all,
        searchQuery: '',
        clearStatus: true,
        clearReadState: true,
        clearSeverity: true,
        clearEventType: true,
      ),
    );
    await _run(() => _load(
          clearStatus: true,
          clearReadState: true,
          clearSeverity: true,
          clearEventType: true,
          datePreset: 'last7',
        ));
  }

  void simulateIncident() {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final created = AlertsSimulator.createBatch(seed: cur.demoEvents.length);
    final next = cur.copyWith(demoEvents: [...created, ...cur.demoEvents]);
    state = AsyncData(next);
    final first = created.first;
    ref.read(foregroundBannerProvider.notifier).show(
          ForegroundBannerEvent(
            title: alertTitle(first),
            body: alertDescription(first),
            route: '/alerts',
          ),
        );
  }

  Future<void> acknowledge(int id) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.isDemo(id)) {
      state = AsyncData(
        cur.copyWith(
          demoEvents: cur.demoEvents
              .map((e) => e.id == id
                  ? GpsAlertEvent(
                      id: e.id,
                      vehicleId: e.vehicleId,
                      eventType: e.eventType,
                      latitude: e.latitude,
                      longitude: e.longitude,
                      speed: e.speed,
                      message: e.message,
                      timestamp: e.timestamp,
                      isAcknowledged: true,
                      severity: e.severity,
                      status: 'acknowledged',
                      vehicleName: e.vehicleName,
                      geofenceId: e.geofenceId,
                      geofenceName: e.geofenceName,
                      driverId: e.driverId,
                      driverName: e.driverName,
                      acknowledgedAt: DateTime.now().toUtc(),
                      canAcknowledge: false,
                      canResolve: true,
                      canArchive: false,
                    )
                  : e)
              .toList(),
        ),
      );
      return;
    }
    await ref.read(gpsAlertsApiProvider).acknowledge(id);
    await refresh();
  }

  Future<void> acknowledgeAllVisible() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final targets =
        cur.visible.where((e) => e.canAcknowledge || cur.isDemo(e.id)).toList();
    for (final e in targets) {
      if (cur.isDemo(e.id) || e.id < 0) {
        await acknowledge(e.id);
      } else if (e.canAcknowledge) {
        try {
          await ref.read(gpsAlertsApiProvider).acknowledge(e.id);
        } catch (_) {}
      }
    }
    await refresh();
  }

  Future<void> markRead(int id) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.isDemo(id)) return;
    await ref.read(gpsAlertsApiProvider).markRead(id);
    await refresh();
  }

  Future<void> resolve(int id, {String? notes}) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.isDemo(id)) {
      state = AsyncData(
        cur.copyWith(
          demoEvents: cur.demoEvents
              .where((e) => e.id != id)
              .toList(),
        ),
      );
      return;
    }
    await ref.read(gpsAlertsApiProvider).resolve(id, notes: notes);
    await refresh();
  }

  Future<void> archive(int id, {String? reason}) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.isDemo(id)) {
      state = AsyncData(
        cur.copyWith(
          demoEvents: cur.demoEvents.where((e) => e.id != id).toList(),
        ),
      );
      return;
    }
    await ref.read(gpsAlertsApiProvider).archive(id, reason: reason);
    await refresh();
  }
}

final alertDetailProvider =
    FutureProvider.family<GpsAlertEvent, int>((ref, id) {
  final local = ref.watch(alertsProvider).valueOrNull;
  if (local != null) {
    for (final e in local.demoEvents) {
      if (e.id == id) return Future.value(e);
    }
    for (final e in local.events) {
      if (e.id == id) return Future.value(e);
    }
  }
  if (id < 0) {
    return Future.error(Exception('Demo alert expired'));
  }
  return ref.read(gpsAlertsApiProvider).getById(id);
});
