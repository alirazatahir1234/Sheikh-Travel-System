import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../data/gps_alerts_api.dart';
import '../domain/alert_display.dart';
import '../domain/gps_alert_models.dart';

enum FleetAlertSort { newest, oldest }

enum FleetAlertStatusTab { active, resolved }

class AlertsState {
  const AlertsState({
    this.stats = GpsAlertStats.empty,
    this.events = const [],
    this.demoEvents = const [],
    this.statusTab = FleetAlertStatusTab.active,
    this.severityFilters = const {},
    this.chip = FleetAlertChip.all,
    this.unacknowledgedOnly = false,
    this.searchQuery = '',
    this.datePreset = 'today',
    this.sort = FleetAlertSort.newest,
    this.eventTypeFilter,
    this.vehicleIdFilter,
    this.selectedIds = const {},
    this.page = 1,
    this.pageSize = 50,
    this.hasMore = false,
    this.loadError,
  });

  final GpsAlertStats stats;
  final List<GpsAlertEvent> events;
  final List<GpsAlertEvent> demoEvents;
  final FleetAlertStatusTab statusTab;
  final Set<AlertDisplaySeverity> severityFilters;
  final FleetAlertChip chip;
  final bool unacknowledgedOnly;
  final String searchQuery;
  final String datePreset;
  final FleetAlertSort sort;
  final String? eventTypeFilter;
  final int? vehicleIdFilter;
  final Set<int> selectedIds;
  final int page;
  final int pageSize;
  final bool hasMore;
  final String? loadError;

  List<GpsAlertEvent> get mergedEvents {
    final byId = <int, GpsAlertEvent>{};
    for (final e in events) {
      byId[e.id] = e;
    }
    for (final e in demoEvents) {
      byId[e.id] = e;
    }
    final list = byId.values.toList();
    list.sort((a, b) => sort == FleetAlertSort.newest
        ? b.timestamp.compareTo(a.timestamp)
        : a.timestamp.compareTo(b.timestamp));
    return list;
  }

  List<AlertIncident> get incidents => groupAlertIncidents(mergedEvents);

  /// Incidents for the Active / Resolved tab (before severity filter).
  List<AlertIncident> get statusScopedIncidents {
    return incidents.where((inc) {
      final resolved = alertIncidentIsResolved(inc);
      return statusTab == FleetAlertStatusTab.resolved ? resolved : !resolved;
    }).toList();
  }

  List<AlertIncident> get visibleIncidents {
    return statusScopedIncidents
        .where(_matchesSeverityFilters)
        .where(_matchesUnacked)
        .where(_matchesSearch)
        .toList();
  }

  List<AlertIncident> get needsAttention =>
      visibleIncidents.where((i) => !i.isGrouped && i.primary.isOpen).toList();

  List<AlertIncident> get groupedIncidents =>
      visibleIncidents.where((i) => i.isGrouped).toList();

  /// Severity counts for the current status tab (ignores selected filters).
  AlertsFeedSummary get tabSeveritySummary {
    return AlertsFeedSummary.fromEvents(
      statusScopedIncidents.map((i) => i.primary).toList(),
    );
  }

  AlertsFeedSummary get feedSummary {
    if (stats.total > 0 ||
        stats.critical > 0 ||
        stats.warning > 0 ||
        stats.unacknowledged > 0) {
      return AlertsFeedSummary.fromStats(stats);
    }
    return AlertsFeedSummary.fromEvents(mergedEvents);
  }

  int get activeFilterCount {
    var n = 0;
    if (severityFilters.isNotEmpty) n++;
    if (datePreset != 'today') n++;
    if (sort != FleetAlertSort.newest) n++;
    if (searchQuery.trim().isNotEmpty) n++;
    if (eventTypeFilter != null) n++;
    if (vehicleIdFilter != null) n++;
    return n;
  }

  bool get hasSeverityFilters => severityFilters.isNotEmpty;

  bool isDemo(int id) => demoEvents.any((e) => e.id == id);

  bool _matchesSeverityFilters(AlertIncident inc) {
    if (severityFilters.isEmpty) return true;
    final bucket = alertSeverityBucket(inc.primary);
    return severityFilters.contains(bucket);
  }

  bool _matchesUnacked(AlertIncident inc) {
    if (!unacknowledgedOnly) return true;
    return !inc.isAcknowledged && inc.primary.isOpen;
  }

  bool _matchesSearch(AlertIncident inc) {
    final q = searchQuery.trim().toLowerCase();
    if (q.isEmpty) return true;
    return inc.allEvents.any((e) => alertMatchesSearch(e, q));
  }

  String? get apiStatus => switch (statusTab) {
        FleetAlertStatusTab.resolved => 'resolved',
        FleetAlertStatusTab.active => null,
      };

  AlertsState copyWith({
    GpsAlertStats? stats,
    List<GpsAlertEvent>? events,
    List<GpsAlertEvent>? demoEvents,
    FleetAlertStatusTab? statusTab,
    Set<AlertDisplaySeverity>? severityFilters,
    FleetAlertChip? chip,
    bool? unacknowledgedOnly,
    String? searchQuery,
    String? datePreset,
    FleetAlertSort? sort,
    String? eventTypeFilter,
    int? vehicleIdFilter,
    Set<int>? selectedIds,
    int? page,
    int? pageSize,
    bool? hasMore,
    String? loadError,
    bool clearEventType = false,
    bool clearVehicleId = false,
    bool clearLoadError = false,
    bool clearSelection = false,
    bool clearSeverityFilters = false,
  }) {
    return AlertsState(
      stats: stats ?? this.stats,
      events: events ?? this.events,
      demoEvents: demoEvents ?? this.demoEvents,
      statusTab: statusTab ?? this.statusTab,
      severityFilters: clearSeverityFilters
          ? const {}
          : (severityFilters ?? this.severityFilters),
      chip: chip ?? this.chip,
      unacknowledgedOnly: unacknowledgedOnly ?? this.unacknowledgedOnly,
      searchQuery: searchQuery ?? this.searchQuery,
      datePreset: datePreset ?? this.datePreset,
      sort: sort ?? this.sort,
      eventTypeFilter:
          clearEventType ? null : (eventTypeFilter ?? this.eventTypeFilter),
      vehicleIdFilter:
          clearVehicleId ? null : (vehicleIdFilter ?? this.vehicleIdFilter),
      selectedIds: clearSelection ? {} : (selectedIds ?? this.selectedIds),
      page: page ?? this.page,
      pageSize: pageSize ?? this.pageSize,
      hasMore: hasMore ?? this.hasMore,
      loadError: clearLoadError ? null : (loadError ?? this.loadError),
    );
  }
}

final alertsProvider =
    AsyncNotifierProvider<AlertsNotifier, AlertsState>(AlertsNotifier.new);

class AlertsNotifier extends AsyncNotifier<AlertsState> {
  Timer? _searchDebounce;

  @override
  Future<AlertsState> build() {
    ref.onDispose(() => _searchDebounce?.cancel());
    return _load();
  }

  Future<AlertsState> _load({
    FleetAlertStatusTab? statusTab,
    Set<AlertDisplaySeverity>? severityFilters,
    FleetAlertChip? chip,
    bool? unacknowledgedOnly,
    String? datePreset,
    FleetAlertSort? sort,
    String? searchQuery,
    String? eventTypeFilter,
    int? vehicleIdFilter,
    int page = 1,
    bool append = false,
    bool clearEventType = false,
    bool clearVehicleId = false,
    bool clearSeverityFilters = false,
  }) async {
    if (!ref.read(authRepositoryProvider).isLoggedIn) {
      return const AlertsState();
    }
    final api = ref.read(gpsAlertsApiProvider);
    final prev = state.valueOrNull;
    final selectedTab =
        statusTab ?? prev?.statusTab ?? FleetAlertStatusTab.active;
    final selectedFilters = clearSeverityFilters
        ? <AlertDisplaySeverity>{}
        : (severityFilters ?? prev?.severityFilters ?? const {});
    final selectedChip = chip ?? prev?.chip ?? FleetAlertChip.all;
    final selectedUnacked =
        unacknowledgedOnly ?? prev?.unacknowledgedOnly ?? false;
    // Resolved history often needs a wider window than "today".
    final selectedDate = datePreset ??
        prev?.datePreset ??
        (selectedTab == FleetAlertStatusTab.resolved ? 'last30' : 'today');
    final selectedSort = sort ?? prev?.sort ?? FleetAlertSort.newest;
    final selectedSearch = searchQuery ?? prev?.searchQuery ?? '';
    final selectedEventType =
        clearEventType ? null : (eventTypeFilter ?? prev?.eventTypeFilter);
    final selectedVehicleId =
        clearVehicleId ? null : (vehicleIdFilter ?? prev?.vehicleIdFilter);
    final pageSize = prev?.pageSize ?? 50;
    final apiDatePreset = selectedDate == 'all' ? null : selectedDate;

    final draft = AlertsState(
      statusTab: selectedTab,
      severityFilters: selectedFilters,
      chip: selectedChip,
      unacknowledgedOnly: selectedUnacked,
      datePreset: selectedDate,
      sort: selectedSort,
      searchQuery: selectedSearch,
      eventTypeFilter: selectedEventType,
      vehicleIdFilter: selectedVehicleId,
      page: page,
      pageSize: pageSize,
    );

    GpsAlertStats stats = prev?.stats ?? GpsAlertStats.empty;
    String? softError;
    try {
      stats = await api.stats();
    } catch (e) {
      softError = e.toString();
    }

    final events = await api.listEvents(
      status: draft.apiStatus,
      unacknowledgedOnly: selectedUnacked ? true : null,
      datePreset: apiDatePreset,
      search: selectedSearch.trim().isEmpty ? null : selectedSearch,
      sort: selectedSort == FleetAlertSort.oldest ? 'oldest' : 'newest',
      eventType: selectedEventType,
      vehicleId: selectedVehicleId,
      page: page,
      pageSize: pageSize,
    );

    final merged =
        append && prev != null ? [...prev.events, ...events] : events;

    return AlertsState(
      stats: stats,
      events: merged,
      demoEvents: kDebugMode ? (prev?.demoEvents ?? const []) : const [],
      statusTab: selectedTab,
      severityFilters: selectedFilters,
      chip: selectedChip,
      unacknowledgedOnly: selectedUnacked,
      searchQuery: selectedSearch,
      datePreset: selectedDate,
      sort: selectedSort,
      eventTypeFilter: selectedEventType,
      vehicleIdFilter: selectedVehicleId,
      selectedIds: append ? (prev?.selectedIds ?? {}) : {},
      page: page,
      pageSize: pageSize,
      hasMore: events.length >= pageSize,
      loadError: softError,
    );
  }

  Future<void> _run(Future<AlertsState> Function() loader) async {
    final previous = state.valueOrNull;
    state = previous != null
        ? const AsyncLoading<AlertsState>()
            .copyWithPrevious(AsyncData(previous))
        : const AsyncLoading();
    state = await AsyncValue.guard(loader);
  }

  Future<void> refresh() => _run(() => _load(page: 1));

  Future<void> loadMore() async {
    final cur = state.valueOrNull;
    if (cur == null || !cur.hasMore || state.isLoading) return;
    await _run(() => _load(page: cur.page + 1, append: true));
  }

  void setSearchQuery(String query) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(searchQuery: query));
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 400), () {
      _run(() => _load(searchQuery: query, page: 1));
    });
  }

  /// Switch Active / Resolved — reloads from API; keeps severity filters.
  Future<void> setStatusTab(FleetAlertStatusTab tab) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.statusTab == tab) return;
    final datePreset = tab == FleetAlertStatusTab.resolved &&
            (cur?.datePreset == 'today' || cur == null)
        ? 'last30'
        : null;
    await _run(() => _load(
          statusTab: tab,
          datePreset: datePreset,
          page: 1,
        ));
  }

  /// Local-only severity filter toggle (no API round-trip).
  void toggleSeverityFilter(AlertDisplaySeverity severity) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    if (severity == AlertDisplaySeverity.resolved) return;
    final next = {...cur.severityFilters};
    if (!next.add(severity)) next.remove(severity);
    state = AsyncData(cur.copyWith(severityFilters: next));
  }

  void clearSeverityFilters() {
    final cur = state.valueOrNull;
    if (cur == null || cur.severityFilters.isEmpty) return;
    state = AsyncData(cur.copyWith(clearSeverityFilters: true));
  }

  Future<void> setChip(FleetAlertChip chip) async {
    // Legacy path — map chip to status / severity filters.
    if (chip == FleetAlertChip.resolved) {
      await setStatusTab(FleetAlertStatusTab.resolved);
      return;
    }
    final cur = state.valueOrNull;
    if (cur != null && cur.statusTab != FleetAlertStatusTab.active) {
      await setStatusTab(FleetAlertStatusTab.active);
    }
    final mapped = switch (chip) {
      FleetAlertChip.critical => {AlertDisplaySeverity.critical},
      FleetAlertChip.warning => {AlertDisplaySeverity.warning},
      FleetAlertChip.info => {AlertDisplaySeverity.info},
      _ => <AlertDisplaySeverity>{},
    };
    final latest = state.valueOrNull;
    if (latest == null) return;
    state = AsyncData(
      latest.copyWith(
        chip: chip,
        severityFilters: mapped,
        unacknowledgedOnly: false,
      ),
    );
  }

  Future<void> setUnacknowledgedOnly(bool value) => _run(() => _load(
        unacknowledgedOnly: value,
        page: 1,
      ));

  Future<void> setDatePreset(String datePreset) =>
      _run(() => _load(datePreset: datePreset, page: 1));

  Future<void> setSort(FleetAlertSort sort) =>
      _run(() => _load(sort: sort, page: 1));

  Future<void> setEventType(String? eventType) => _run(
        () => _load(
          eventTypeFilter: eventType,
          clearEventType: eventType == null,
          page: 1,
        ),
      );

  Future<void> setVehicleFilter(int? vehicleId) => _run(
        () => _load(
          vehicleIdFilter: vehicleId,
          clearVehicleId: vehicleId == null,
          page: 1,
        ),
      );

  Future<void> clearAdvancedFilters() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(
      cur.copyWith(
        chip: FleetAlertChip.all,
        unacknowledgedOnly: false,
        searchQuery: '',
        datePreset:
            cur.statusTab == FleetAlertStatusTab.resolved ? 'last30' : 'today',
        sort: FleetAlertSort.newest,
        clearEventType: true,
        clearVehicleId: true,
        clearSelection: true,
        clearSeverityFilters: true,
      ),
    );
    await _run(() => _load(
          unacknowledgedOnly: false,
          datePreset: cur.statusTab == FleetAlertStatusTab.resolved
              ? 'last30'
              : 'today',
          sort: FleetAlertSort.newest,
          searchQuery: '',
          clearEventType: true,
          clearVehicleId: true,
          clearSeverityFilters: true,
          page: 1,
        ));
  }

  void toggleSelect(int id) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final next = {...cur.selectedIds};
    if (!next.add(id)) next.remove(id);
    state = AsyncData(cur.copyWith(selectedIds: next));
  }

  void selectAllVisible() {
    final cur = state.valueOrNull;
    if (cur == null) return;
    final ids = <int>{};
    for (final inc in cur.visibleIncidents) {
      ids.addAll(inc.allIds);
    }
    state = AsyncData(cur.copyWith(selectedIds: ids));
  }

  void clearSelection() {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(clearSelection: true));
  }

  Future<void> acknowledge(int id) async {
    final cur = state.valueOrNull;
    if (cur != null && cur.isDemo(id)) {
      state = AsyncData(
        cur.copyWith(
          demoEvents: cur.demoEvents
              .map((e) => e.id == id
                  ? e.copyWith(
                      isAcknowledged: true,
                      status: 'acknowledged',
                      acknowledgedAt: DateTime.now().toUtc(),
                      canAcknowledge: false,
                      canResolve: true,
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

  Future<void> acknowledgeIds(List<int> ids) async {
    final cur = state.valueOrNull;
    if (cur == null || ids.isEmpty) return;
    final real = ids.where((id) => id > 0 && !cur.isDemo(id)).toList();
    final demo = ids.where(cur.isDemo).toList();
    for (final id in demo) {
      await acknowledge(id);
    }
    if (real.isNotEmpty) {
      try {
        await ref.read(gpsAlertsApiProvider).bulkAcknowledge(real);
      } catch (_) {
        for (final id in real) {
          try {
            await ref.read(gpsAlertsApiProvider).acknowledge(id);
          } catch (_) {}
        }
      }
    }
    await refresh();
  }

  Future<void> acknowledgeSelected() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    await acknowledgeIds(cur.selectedIds.toList());
  }

  Future<void> assign({
    required List<int> ids,
    int? assignedToUserId,
    required String assignedToName,
  }) async {
    final cur = state.valueOrNull;
    if (cur == null || ids.isEmpty) return;
    final name = assignedToName.trim();
    if (name.isEmpty && assignedToUserId == null) return;

    final real = ids.where((id) => id > 0 && !cur.isDemo(id)).toList();
    final demo = ids.where(cur.isDemo).toList();
    if (demo.isNotEmpty) {
      state = AsyncData(
        cur.copyWith(
          demoEvents: cur.demoEvents
              .map((e) => demo.contains(e.id)
                  ? e.copyWith(
                      assignedToUserId: assignedToUserId,
                      assignedToName: name,
                      assignedAt: DateTime.now().toUtc(),
                    )
                  : e)
              .toList(),
        ),
      );
    }
    if (real.isNotEmpty) {
      try {
        await ref.read(gpsAlertsApiProvider).bulkAssign(
              real,
              assignedToUserId: assignedToUserId,
              assignedToName: name,
            );
      } catch (_) {
        for (final id in real) {
          try {
            await ref.read(gpsAlertsApiProvider).assign(
                  id,
                  assignedToUserId: assignedToUserId,
                  assignedToName: name,
                );
          } catch (_) {}
        }
      }
    }
    await refresh();
  }

  Future<void> assignSelected({
    int? assignedToUserId,
    required String assignedToName,
  }) async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    await assign(
      ids: cur.selectedIds.toList(),
      assignedToUserId: assignedToUserId,
      assignedToName: assignedToName,
    );
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
          demoEvents: cur.demoEvents.where((e) => e.id != id).toList(),
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
