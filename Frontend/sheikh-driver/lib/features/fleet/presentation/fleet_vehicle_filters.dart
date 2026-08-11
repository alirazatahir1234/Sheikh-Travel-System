import '../domain/fleet_models.dart';

enum FleetStatusFilterOption {
  all,
  online,
  moving,
  idle,
  parked,
  offline,
}

enum FleetIgnitionFilter { all, on, off, unknown }

enum FleetSignalFilter { all, good, weak, offline }

enum FleetDriverFilter { all, assigned, unassigned }

enum FleetAlertFilter { all, withAlerts, noAlerts }

/// Multi-dimensional vehicle list filters for Fleet hub / live map.
class FleetVehicleFilters {
  const FleetVehicleFilters({
    this.status = FleetStatusFilterOption.all,
    this.ignition = FleetIgnitionFilter.all,
    this.signal = FleetSignalFilter.all,
    this.driver = FleetDriverFilter.all,
    this.alerts = FleetAlertFilter.all,
  });

  static const empty = FleetVehicleFilters();

  final FleetStatusFilterOption status;
  final FleetIgnitionFilter ignition;
  final FleetSignalFilter signal;
  final FleetDriverFilter driver;
  final FleetAlertFilter alerts;

  bool get isDefault =>
      status == FleetStatusFilterOption.all &&
      ignition == FleetIgnitionFilter.all &&
      signal == FleetSignalFilter.all &&
      driver == FleetDriverFilter.all &&
      alerts == FleetAlertFilter.all;

  int get activeCount {
    var n = 0;
    if (status != FleetStatusFilterOption.all) n++;
    if (ignition != FleetIgnitionFilter.all) n++;
    if (signal != FleetSignalFilter.all) n++;
    if (driver != FleetDriverFilter.all) n++;
    if (alerts != FleetAlertFilter.all) n++;
    return n;
  }

  FleetVehicleFilters copyWith({
    FleetStatusFilterOption? status,
    FleetIgnitionFilter? ignition,
    FleetSignalFilter? signal,
    FleetDriverFilter? driver,
    FleetAlertFilter? alerts,
  }) {
    return FleetVehicleFilters(
      status: status ?? this.status,
      ignition: ignition ?? this.ignition,
      signal: signal ?? this.signal,
      driver: driver ?? this.driver,
      alerts: alerts ?? this.alerts,
    );
  }

  bool matches(FleetVehicleLocation v) {
    if (!_matchesStatus(v)) return false;
    if (!_matchesIgnition(v)) return false;
    if (!_matchesSignal(v)) return false;
    if (!_matchesDriver(v)) return false;
    if (!_matchesAlerts(v)) return false;
    return true;
  }

  bool _matchesStatus(FleetVehicleLocation v) {
    switch (status) {
      case FleetStatusFilterOption.all:
        return true;
      case FleetStatusFilterOption.online:
        return v.status == FleetTrackStatus.moving ||
            v.status == FleetTrackStatus.idle ||
            v.status == FleetTrackStatus.parked ||
            v.status == FleetTrackStatus.sos;
      case FleetStatusFilterOption.moving:
        return v.status == FleetTrackStatus.moving;
      case FleetStatusFilterOption.idle:
        return v.status == FleetTrackStatus.idle;
      case FleetStatusFilterOption.parked:
        return v.status == FleetTrackStatus.parked;
      case FleetStatusFilterOption.offline:
        return v.status == FleetTrackStatus.offline ||
            v.status == FleetTrackStatus.neverSeen;
    }
  }

  bool _matchesIgnition(FleetVehicleLocation v) {
    switch (ignition) {
      case FleetIgnitionFilter.all:
        return true;
      case FleetIgnitionFilter.on:
        return v.ignition == true;
      case FleetIgnitionFilter.off:
        return v.ignition == false;
      case FleetIgnitionFilter.unknown:
        return v.ignition == null;
    }
  }

  bool _matchesSignal(FleetVehicleLocation v) {
    switch (signal) {
      case FleetSignalFilter.all:
        return true;
      case FleetSignalFilter.good:
        return (v.gsmSignal ?? 0) >= 2;
      case FleetSignalFilter.weak:
        return v.gsmSignal == 1;
      case FleetSignalFilter.offline:
        return v.gsmSignal == null || v.gsmSignal == 0;
    }
  }

  bool _matchesDriver(FleetVehicleLocation v) {
    final assigned =
        v.driverName != null && v.driverName!.trim().isNotEmpty;
    switch (driver) {
      case FleetDriverFilter.all:
        return true;
      case FleetDriverFilter.assigned:
        return assigned;
      case FleetDriverFilter.unassigned:
        return !assigned;
    }
  }

  bool _matchesAlerts(FleetVehicleLocation v) {
    final has = _hasAlert(v);
    switch (alerts) {
      case FleetAlertFilter.all:
        return true;
      case FleetAlertFilter.withAlerts:
        return has;
      case FleetAlertFilter.noAlerts:
        return !has;
    }
  }

  static bool _hasAlert(FleetVehicleLocation v) {
    if (v.status == FleetTrackStatus.sos) return true;
    if (v.alarmType != null && v.alarmType!.trim().isNotEmpty) return true;
    if (v.serviceAlert != null && v.serviceAlert!.trim().isNotEmpty) {
      return true;
    }
    return false;
  }

  /// Maps a KPI chip tap to a status filter option.
  static FleetStatusFilterOption fromKpiStatus(FleetTrackStatus? status) {
    if (status == null) return FleetStatusFilterOption.online;
    return switch (status) {
      FleetTrackStatus.moving => FleetStatusFilterOption.moving,
      FleetTrackStatus.idle => FleetStatusFilterOption.idle,
      FleetTrackStatus.parked => FleetStatusFilterOption.parked,
      FleetTrackStatus.offline ||
      FleetTrackStatus.neverSeen =>
        FleetStatusFilterOption.offline,
      FleetTrackStatus.sos => FleetStatusFilterOption.online,
    };
  }
}
