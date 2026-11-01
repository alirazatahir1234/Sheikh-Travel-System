import 'package:flutter/material.dart';

import 'gps_alert_models.dart';

/// Canonical event keys after synonym collapse.
String normalizeAlertEventType(String raw) {
  final k = raw.trim().toLowerCase().replaceAll(' ', '_').replaceAll('-', '_');
  return switch (k) {
    'device_offline' || 'gps_offline' || 'offline' => 'vehicle_offline',
    'device_online' || 'vehicle_online' || 'gps_online' => 'online',
    'overspeed' || 'speed_alert' || 'over_speed' || 'speed' => 'speed_exceeded',
    'geofence_entered' || 'geofenceenter' => 'geofence_enter',
    'geofence_exited' || 'geofenceexit' => 'geofence_exit',
    'harsh_brake' || 'hard_braking' || 'harshbraking' => 'harsh_braking',
    'harsh_accel' || 'hard_acceleration' || 'harshacceleration' =>
      'harsh_acceleration',
    'battery' || 'battery_low' => 'low_battery',
    'maintenance' || 'maintenance_due' => 'maintenance_due',
    'gps_signal_lost' || 'no_gps' => 'gps_lost',
    'ignition' => 'ignition_on',
    _ => k,
  };
}

bool isOfflineAlertType(String eventType) =>
    normalizeAlertEventType(eventType) == 'vehicle_offline';

bool isOnlineAlertType(String eventType) =>
    normalizeAlertEventType(eventType) == 'online';

enum AlertDisplaySeverity { critical, warning, info, resolved }

class AlertTypeMeta {
  const AlertTypeMeta({
    required this.title,
    required this.description,
    required this.severity,
  });

  final String title;
  final String description;
  final AlertDisplaySeverity severity;
}

AlertTypeMeta alertTypeMeta(String eventType) {
  final key = normalizeAlertEventType(eventType);
  return switch (key) {
    'vehicle_offline' => const AlertTypeMeta(
        title: 'Vehicle Offline',
        description: 'Vehicle has stopped reporting GPS data.',
        severity: AlertDisplaySeverity.warning,
      ),
    'online' => const AlertTypeMeta(
        title: 'Vehicle Back Online',
        description: 'Vehicle has reconnected to the tracking server.',
        severity: AlertDisplaySeverity.info,
      ),
    'speed_exceeded' => const AlertTypeMeta(
        title: 'Overspeed',
        description: 'Vehicle exceeded the configured speed limit.',
        severity: AlertDisplaySeverity.warning,
      ),
    'harsh_braking' => const AlertTypeMeta(
        title: 'Harsh Braking',
        description: 'Sudden deceleration was detected.',
        severity: AlertDisplaySeverity.warning,
      ),
    'harsh_acceleration' => const AlertTypeMeta(
        title: 'Harsh Acceleration',
        description: 'Sudden acceleration was detected.',
        severity: AlertDisplaySeverity.warning,
      ),
    'geofence_enter' => const AlertTypeMeta(
        title: 'Geofence Entered',
        description: 'Vehicle entered a monitored geofence.',
        severity: AlertDisplaySeverity.warning,
      ),
    'geofence_exit' => const AlertTypeMeta(
        title: 'Geofence Exited',
        description: 'Vehicle left a monitored geofence.',
        severity: AlertDisplaySeverity.warning,
      ),
    'low_battery' => const AlertTypeMeta(
        title: 'Low Battery',
        description: 'GPS device battery is low.',
        severity: AlertDisplaySeverity.warning,
      ),
    'maintenance_due' => const AlertTypeMeta(
        title: 'Maintenance Due',
        description: 'Vehicle maintenance is due or overdue.',
        severity: AlertDisplaySeverity.warning,
      ),
    'gps_lost' => const AlertTypeMeta(
        title: 'GPS Signal Lost',
        description: 'GPS signal was lost or degraded.',
        severity: AlertDisplaySeverity.warning,
      ),
    'ignition_on' => const AlertTypeMeta(
        title: 'Ignition On',
        description: 'Ignition was turned on.',
        severity: AlertDisplaySeverity.info,
      ),
    'ignition_off' => const AlertTypeMeta(
        title: 'Ignition Off',
        description: 'Ignition was turned off.',
        severity: AlertDisplaySeverity.info,
      ),
    'sos' || 'alarm' => const AlertTypeMeta(
        title: 'SOS / Alarm',
        description: 'Emergency or alarm signal received.',
        severity: AlertDisplaySeverity.critical,
      ),
    'power_cut' => const AlertTypeMeta(
        title: 'Power Cut',
        description: 'Device power was disconnected.',
        severity: AlertDisplaySeverity.critical,
      ),
    'low_fuel' => const AlertTypeMeta(
        title: 'Low Fuel',
        description: 'Fuel level is below the configured threshold.',
        severity: AlertDisplaySeverity.warning,
      ),
    _ => AlertTypeMeta(
        title: _titleCase(key.replaceAll('_', ' ')),
        description: '',
        severity: _severityFromApiBucket(null),
      ),
  };
}

AlertDisplaySeverity _severityFromApiBucket(String? apiSeverity) {
  switch ((apiSeverity ?? '').toLowerCase()) {
    case 'critical':
      return AlertDisplaySeverity.critical;
    case 'high':
      return AlertDisplaySeverity.warning;
    case 'medium':
      return AlertDisplaySeverity.warning;
    case 'low':
      return AlertDisplaySeverity.info;
    default:
      return AlertDisplaySeverity.info;
  }
}

/// Display severity for list/detail — prefers type catalog, API critical, and recovery.
AlertDisplaySeverity alertDisplaySeverity(
  GpsAlertEvent event, {
  bool recovered = false,
}) {
  if (event.isResolved || recovered) {
    final key = normalizeAlertEventType(event.eventType);
    if (key == 'online') return AlertDisplaySeverity.info;
    if (key == 'vehicle_offline') return AlertDisplaySeverity.resolved;
  }
  final meta = alertTypeMeta(event.eventType);
  if (meta.severity == AlertDisplaySeverity.critical) {
    return AlertDisplaySeverity.critical;
  }
  final api = event.severity.toLowerCase();
  if (api == 'critical') return AlertDisplaySeverity.critical;
  if (meta.title == 'Vehicle Offline' && !recovered) {
    // Long open offline → critical feel when still active and old enough handled in UI.
    return AlertDisplaySeverity.warning;
  }
  return meta.severity != AlertDisplaySeverity.info
      ? meta.severity
      : _severityFromApiBucket(event.severity);
}

String alertTitle(GpsAlertEvent event) => alertTypeMeta(event.eventType).title;

String alertDescription(GpsAlertEvent event) {
  final meta = alertTypeMeta(event.eventType);
  final msg = event.message.trim();
  if (meta.description.isNotEmpty) return meta.description;
  if (msg.isNotEmpty) return msg;
  return 'Alert event recorded.';
}

String alertSeverityLabel(AlertDisplaySeverity s) => switch (s) {
      AlertDisplaySeverity.critical => 'CRITICAL',
      AlertDisplaySeverity.warning => 'WARNING',
      AlertDisplaySeverity.info => 'INFO',
      AlertDisplaySeverity.resolved => 'RESOLVED',
    };

/// Presentation grouping family (never discards underlying GPS events).
enum AlertIncidentKind {
  single,
  offline,
  ignition,
  overspeed,
  lowBattery,
  device,
}

AlertIncidentKind incidentKindForEventType(String eventType) {
  final key = normalizeAlertEventType(eventType);
  return switch (key) {
    'vehicle_offline' || 'online' => AlertIncidentKind.offline,
    'ignition_on' || 'ignition_off' => AlertIncidentKind.ignition,
    'speed_exceeded' => AlertIncidentKind.overspeed,
    'low_battery' => AlertIncidentKind.lowBattery,
    'power_cut' || 'gps_lost' => AlertIncidentKind.device,
    _ => AlertIncidentKind.single,
  };
}

/// List/detail row after pairing Offline → Online / clustering related events.
class AlertIncident {
  const AlertIncident({
    required this.primary,
    this.recovery,
    this.endedAt,
    this.kind = AlertIncidentKind.single,
    this.events = const [],
  });

  /// Alert opened for detail / acknowledge (offline row for incidents).
  final GpsAlertEvent primary;
  final GpsAlertEvent? recovery;
  final DateTime? endedAt;
  final AlertIncidentKind kind;

  /// All original GPS events in this presentation row (never discarded).
  final List<GpsAlertEvent> events;

  List<GpsAlertEvent> get allEvents {
    if (events.isNotEmpty) return events;
    if (recovery != null) return [primary, recovery!];
    return [primary];
  }

  /// Multi-event clusters shown under "Grouped incidents" (not offline pairing).
  bool get isGrouped =>
      allEvents.length > 1 &&
      kind != AlertIncidentKind.single &&
      kind != AlertIncidentKind.offline;

  bool get isOfflineIncident =>
      kind == AlertIncidentKind.offline || isOfflineAlertType(primary.eventType);

  Duration? get duration {
    final end = endedAt ?? primary.resolvedAt ?? recovery?.timestamp;
    if (end == null) return null;
    final d = end.difference(primary.timestamp);
    return d.isNegative ? null : d;
  }

  bool get recovered =>
      endedAt != null ||
      recovery != null ||
      primary.resolvedAt != null ||
      (isOfflineIncident && primary.isResolved);

  bool get needsAcknowledge =>
      allEvents.any((e) => e.canAcknowledge);

  bool get isAcknowledged => allEvents.every((e) => e.isAcknowledged || e.isResolved);

  bool get isAssigned => allEvents.any((e) => e.isAssigned);

  String? get assignedToName {
    for (final e in allEvents) {
      final n = e.assignedToName?.trim();
      if (n != null && n.isNotEmpty) return n;
    }
    return null;
  }

  DateTime? get acknowledgedAt =>
      primary.acknowledgedAt ?? recovery?.acknowledgedAt;

  List<int> get acknowledgeIds {
    final ids = <int>[];
    for (final e in allEvents) {
      if (e.canAcknowledge) ids.add(e.id);
    }
    return ids;
  }

  List<int> get allIds => allEvents.map((e) => e.id).toList();

  String get groupTitle {
    final plate = primary.vehicleLabel;
    return switch (kind) {
      AlertIncidentKind.ignition => 'Ignition activity • $plate',
      AlertIncidentKind.overspeed => 'Overspeed burst • $plate',
      AlertIncidentKind.lowBattery => 'Low battery • $plate',
      AlertIncidentKind.device => 'Device health • $plate',
      AlertIncidentKind.offline => alertTitle(primary),
      AlertIncidentKind.single => alertTitle(primary),
    };
  }

  String get groupSubtitle {
    if (kind == AlertIncidentKind.offline) {
      final dur = formatAlertDuration(duration);
      if (recovered) {
        return dur.isEmpty
            ? 'GPS reporting resumed'
            : 'Offline for $dur then recovered';
      }
      return dur.isEmpty
          ? 'GPS stopped reporting'
          : 'GPS stopped reporting • $dur';
    }
    if (isGrouped) {
      final sorted = [...allEvents]..sort((a, b) => a.timestamp.compareTo(b.timestamp));
      final start = sorted.first.timestamp.toLocal();
      final end = sorted.last.timestamp.toLocal();
      final sh =
          '${start.hour.toString().padLeft(2, '0')}:${start.minute.toString().padLeft(2, '0')}';
      final eh =
          '${end.hour.toString().padLeft(2, '0')}:${end.minute.toString().padLeft(2, '0')}';
      return '${allEvents.length} events • $sh - $eh';
    }
    final msg = primary.message.trim();
    if (msg.isNotEmpty) return msg;
    return alertDescription(primary);
  }
}

/// Pair Offline → next Online; cluster ignition/overspeed/battery/device bursts.
/// Original GPS events are retained on each [AlertIncident.events] list.
List<AlertIncident> groupAlertIncidents(List<GpsAlertEvent> events) {
  if (events.isEmpty) return const [];

  final chronological = [...events]
    ..sort((a, b) => a.timestamp.compareTo(b.timestamp));

  final pairedOnlineIds = <int>{};
  final offlineToRecovery = <int, GpsAlertEvent>{};
  final consumed = <int>{};

  for (var i = 0; i < chronological.length; i++) {
    final e = chronological[i];
    if (!isOfflineAlertType(e.eventType)) continue;

    GpsAlertEvent? recovery;
    for (var j = i + 1; j < chronological.length; j++) {
      final n = chronological[j];
      if (n.vehicleId != e.vehicleId) continue;
      if (isOnlineAlertType(n.eventType) && !pairedOnlineIds.contains(n.id)) {
        recovery = n;
        pairedOnlineIds.add(n.id);
        break;
      }
      if (isOfflineAlertType(n.eventType) && n.vehicleId == e.vehicleId) break;
    }

    if (recovery != null) {
      offlineToRecovery[e.id] = recovery;
    }
  }

  const window = Duration(hours: 2);
  final clusterKinds = {
    AlertIncidentKind.ignition,
    AlertIncidentKind.overspeed,
    AlertIncidentKind.lowBattery,
    AlertIncidentKind.device,
  };

  final clusters = <AlertIncident>[];
  for (var i = 0; i < chronological.length; i++) {
    final seed = chronological[i];
    if (consumed.contains(seed.id)) continue;
    if (pairedOnlineIds.contains(seed.id)) continue;
    if (isOfflineAlertType(seed.eventType) || isOnlineAlertType(seed.eventType)) {
      continue;
    }

    final kind = incidentKindForEventType(seed.eventType);
    if (!clusterKinds.contains(kind)) continue;

    final group = <GpsAlertEvent>[seed];
    consumed.add(seed.id);
    var lastTs = seed.timestamp;

    for (var j = i + 1; j < chronological.length; j++) {
      final n = chronological[j];
      if (consumed.contains(n.id) || pairedOnlineIds.contains(n.id)) continue;
      if (n.vehicleId != seed.vehicleId) continue;
      if (incidentKindForEventType(n.eventType) != kind) continue;
      if (n.timestamp.difference(lastTs) > window) break;
      group.add(n);
      consumed.add(n.id);
      lastTs = n.timestamp;
    }

    if (group.length >= 2) {
      group.sort((a, b) => b.timestamp.compareTo(a.timestamp));
      clusters.add(AlertIncident(
        primary: group.first,
        kind: kind,
        events: group,
        endedAt: group.map((e) => e.timestamp).reduce(
              (a, b) => a.isAfter(b) ? a : b,
            ),
      ));
    } else {
      // Singleton — release so it becomes a normal row below.
      consumed.remove(seed.id);
    }
  }

  final newestFirst = [...events]
    ..sort((a, b) => b.timestamp.compareTo(a.timestamp));

  final out = <AlertIncident>[...clusters];
  for (final e in newestFirst) {
    if (pairedOnlineIds.contains(e.id)) continue;
    if (consumed.contains(e.id)) continue;

    if (isOfflineAlertType(e.eventType)) {
      final recovery = offlineToRecovery[e.id];
      final ended = recovery?.timestamp ?? e.resolvedAt;
      final members = <GpsAlertEvent>[e];
      if (recovery != null) members.add(recovery);
      out.add(AlertIncident(
        primary: e,
        recovery: recovery,
        endedAt: ended,
        kind: AlertIncidentKind.offline,
        events: members,
      ));
      if (recovery != null) consumed.add(recovery.id);
    } else if (!isOnlineAlertType(e.eventType)) {
      out.add(AlertIncident(
        primary: e,
        kind: incidentKindForEventType(e.eventType),
        events: [e],
      ));
    }
  }

  out.sort((a, b) => b.primary.timestamp.compareTo(a.primary.timestamp));
  return out;
}

String formatAlertDuration(Duration? d) {
  if (d == null) return '';
  final totalMinutes = d.inMinutes;
  if (totalMinutes <= 0) {
    final sec = d.inSeconds;
    if (sec <= 0) return '0m';
    return '${sec}s';
  }
  if (totalMinutes < 60) return '${totalMinutes}m';
  final h = totalMinutes ~/ 60;
  final m = totalMinutes % 60;
  if (m == 0) return '${h}h';
  return '${h}h ${m}m';
}

String _titleCase(String input) {
  if (input.trim().isEmpty) return input;
  return input
      .split(RegExp(r'\s+'))
      .map((w) => w.isEmpty
          ? w
          : '${w[0].toUpperCase()}${w.length > 1 ? w.substring(1) : ''}')
      .join(' ');
}

/// Telematics feed category buckets (filter pills).
enum AlertCategory {
  all,
  critical,
  speeding,
  geofence,
  engineDtc,
  harsh,
  safetySos,
  maintenance,
  other,
}

extension AlertCategoryX on AlertCategory {
  String get id => switch (this) {
        AlertCategory.all => 'all',
        AlertCategory.critical => 'critical',
        AlertCategory.speeding => 'speeding',
        AlertCategory.geofence => 'geofence',
        AlertCategory.engineDtc => 'engine_dtc',
        AlertCategory.harsh => 'harsh',
        AlertCategory.safetySos => 'safety_sos',
        AlertCategory.maintenance => 'maintenance',
        AlertCategory.other => 'other',
      };

  String get label => switch (this) {
        AlertCategory.all => 'All Alerts',
        AlertCategory.critical => 'Critical',
        AlertCategory.speeding => 'Speeding',
        AlertCategory.geofence => 'Geofence',
        AlertCategory.engineDtc => 'Engine & DTC',
        AlertCategory.harsh => 'Harsh Driving',
        AlertCategory.safetySos => 'Safety/SOS',
        AlertCategory.maintenance => 'Maintenance',
        AlertCategory.other => 'Other',
      };

  static const pillOrder = [
    AlertCategory.all,
    AlertCategory.critical,
    AlertCategory.speeding,
    AlertCategory.geofence,
    AlertCategory.engineDtc,
    AlertCategory.harsh,
    AlertCategory.safetySos,
    AlertCategory.maintenance,
  ];
}

AlertCategory alertCategoryFor(GpsAlertEvent event) {
  final key = normalizeAlertEventType(event.eventType);
  return switch (key) {
    'speed_exceeded' => AlertCategory.speeding,
    'geofence_enter' || 'geofence_exit' => AlertCategory.geofence,
    'harsh_braking' || 'harsh_acceleration' => AlertCategory.harsh,
    'sos' || 'alarm' || 'power_cut' || 'seatbelt' => AlertCategory.safetySos,
    'maintenance_due' || 'engine_fault' || 'low_fuel' || 'low_battery' =>
      AlertCategory.maintenance,
    'fuel_theft' ||
    'tow' ||
    'gps_lost' ||
    'vehicle_offline' ||
    'online' ||
    'idle_vehicle' ||
    'ignition_on' ||
    'ignition_off' =>
      AlertCategory.other,
    _ => event.message.toLowerCase().contains('dtc') ||
            event.message.toLowerCase().contains('engine overheat') ||
            event.message.toLowerCase().contains('overheating')
        ? AlertCategory.engineDtc
        : AlertCategory.other,
  };
}

bool matchesAlertCategory(GpsAlertEvent event, AlertCategory? filter) {
  if (filter == null || filter == AlertCategory.all) return true;
  if (filter == AlertCategory.critical) {
    return alertDisplaySeverity(event) == AlertDisplaySeverity.critical ||
        event.severity.toLowerCase() == 'critical';
  }
  if (filter == AlertCategory.engineDtc) {
    final cat = alertCategoryFor(event);
    final msg = event.message.toLowerCase();
    return cat == AlertCategory.engineDtc ||
        (cat == AlertCategory.maintenance &&
            (msg.contains('dtc') || msg.contains('overheat')));
  }
  return alertCategoryFor(event) == filter;
}

bool alertMatchesSearch(GpsAlertEvent event, String query) {
  final q = query.trim().toLowerCase();
  if (q.isEmpty) return true;
  final hay = [
    event.vehicleName ?? '',
    'vehicle #${event.vehicleId}',
    event.driverName ?? '',
    event.message,
    event.geofenceName ?? '',
    event.eventType,
    alertTitle(event),
    '${event.latitude},${event.longitude}',
  ].join(' ').toLowerCase();
  return hay.contains(q);
}

class AlertsFeedSummary {
  const AlertsFeedSummary({
    required this.total,
    required this.critical,
    required this.warning,
    required this.info,
    required this.unacknowledged,
    required this.closed,
  });

  final int total;
  final int critical;
  final int warning;
  final int info;
  final int unacknowledged;
  final int closed;

  factory AlertsFeedSummary.fromStats(GpsAlertStats stats) {
    return AlertsFeedSummary(
      total: stats.total,
      critical: stats.critical,
      warning: stats.warning,
      info: stats.info,
      unacknowledged: stats.unacknowledged,
      closed: stats.resolved + stats.archived,
    );
  }

  static AlertsFeedSummary fromEvents(List<GpsAlertEvent> events) {
    var critical = 0;
    var warning = 0;
    var info = 0;
    var unacked = 0;
    var closed = 0;
    for (final e in events) {
      if (!e.isAcknowledged && e.isOpen) unacked++;
      if (e.isResolved || e.isArchived) closed++;
      // Always bucket by underlying severity so Resolved-tab chips stay useful.
      final d = alertSeverityBucket(e);
      if (d == AlertDisplaySeverity.critical) {
        critical++;
      } else if (d == AlertDisplaySeverity.warning) {
        warning++;
      } else {
        info++;
      }
    }
    return AlertsFeedSummary(
      total: events.length,
      critical: critical,
      warning: warning,
      info: info,
      unacknowledged: unacked,
      closed: closed,
    );
  }
}

/// Fleet feed chip filters (mockup).
enum FleetAlertChip {
  all,
  critical,
  warning,
  info,
  acknowledged,
  resolved,
}

extension FleetAlertChipX on FleetAlertChip {
  String get label => switch (this) {
        FleetAlertChip.all => 'All',
        FleetAlertChip.critical => 'Critical',
        FleetAlertChip.warning => 'Warning',
        FleetAlertChip.info => 'Info',
        FleetAlertChip.acknowledged => 'Acknowledged',
        FleetAlertChip.resolved => 'Resolved',
      };
}

IconData alertTypeSquareIcon(String eventType) {
  final key = normalizeAlertEventType(eventType);
  return switch (key) {
    'vehicle_offline' || 'gps_lost' => Icons.power_settings_new_rounded,
    'online' => Icons.wifi_rounded,
    'speed_exceeded' => Icons.speed_rounded,
    'low_battery' => Icons.battery_alert_rounded,
    'power_cut' => Icons.power_off_rounded,
    'ignition_on' || 'ignition_off' => Icons.vpn_key_rounded,
    'sos' || 'alarm' => Icons.sos_outlined,
    'geofence_enter' || 'geofence_exit' => Icons.gps_fixed_rounded,
    _ => Icons.notifications_active_outlined,
  };
}

String formatAlertClock(DateTime utcOrLocal) {
  final local = utcOrLocal.toLocal();
  return '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}

String alertAckStatusLabel(GpsAlertEvent e) {
  if (e.isResolved) return 'Resolved';
  if (e.isAssigned && !e.isAcknowledged) {
    final n = e.assignedToName?.trim();
    return n == null || n.isEmpty ? 'Assigned' : 'Assigned • $n';
  }
  if (e.isAcknowledged) return 'Acknowledged';
  return 'Unacknowledged';
}

Color alertAckStatusColor(GpsAlertEvent e) {
  if (e.isResolved) return const Color(0xFF10B981);
  if (e.isAssigned && !e.isAcknowledged) return const Color(0xFFF59E0B);
  if (e.isAcknowledged) return const Color(0xFF10B981);
  return const Color(0xFFEF4444);
}

IconData alertCategoryIcon(GpsAlertEvent event) {
  final key = normalizeAlertEventType(event.eventType);
  return switch (key) {
    'speed_exceeded' => Icons.speed_rounded,
    'geofence_enter' || 'geofence_exit' => Icons.gps_fixed_rounded,
    'harsh_braking' || 'harsh_acceleration' => Icons.warning_amber_rounded,
    'sos' || 'alarm' => Icons.sos_outlined,
    'vehicle_offline' || 'gps_lost' => Icons.signal_wifi_off_rounded,
    'online' => Icons.wifi_rounded,
    'maintenance_due' || 'engine_fault' => Icons.build_outlined,
    'low_battery' || 'power_cut' => Icons.battery_alert_rounded,
    'low_fuel' || 'fuel_theft' => Icons.local_gas_station_outlined,
    _ => Icons.notifications_active_outlined,
  };
}

/// Best-effort speed limit from free-text message (e.g. "limit 80").
double? parseSpeedLimitFromMessage(String message) {
  final m = RegExp(
    r'(?:limit|max(?:imum)?|posted)\s*[:=]?\s*(\d{2,3})\s*(?:km/?h|kph)?',
    caseSensitive: false,
  ).firstMatch(message);
  if (m != null) return double.tryParse(m.group(1)!);
  final m2 = RegExp(r'(\d{2,3})\s*km/?h', caseSensitive: false).allMatches(message);
  if (m2.length >= 2) {
    // Often "114 km/h ... 80 km/h" — take the smaller as limit.
    final vals = m2.map((x) => double.tryParse(x.group(1)!)).whereType<double>().toList();
    if (vals.length >= 2) {
      vals.sort();
      return vals.first;
    }
  }
  return null;
}

String formatAlertRelativeTime(DateTime utcOrLocal) {
  final local = utcOrLocal.toLocal();
  final d = DateTime.now().difference(local);
  if (d.inSeconds < 60) return 'Just now';
  if (d.inMinutes < 60) return '${d.inMinutes}m ago';
  if (d.inHours < 24) return '${d.inHours}h ago';
  if (d.inDays < 7) return '${d.inDays}d ago';
  return '${local.day}/${local.month}';
}

/// Verbose relative time for Fleet Alerts cards (mockup copy).
String formatAlertRelativeTimeVerbose(DateTime utcOrLocal) {
  final local = utcOrLocal.toLocal();
  final d = DateTime.now().difference(local);
  if (d.inSeconds < 60) return 'Just now';
  if (d.inMinutes == 1) return '1 minute ago';
  if (d.inMinutes < 60) return '${d.inMinutes} minutes ago';
  if (d.inHours == 1) return '1 hour ago';
  if (d.inHours < 24) return '${d.inHours} hours ago';
  if (d.inDays == 1) return '1 day ago';
  if (d.inDays < 7) return '${d.inDays} days ago';
  return '${local.day}/${local.month}/${local.year}';
}

/// Severity bucket for Critical / Warning / Info filter chips.
AlertDisplaySeverity alertSeverityBucket(GpsAlertEvent event) {
  final meta = alertTypeMeta(event.eventType);
  if (meta.severity == AlertDisplaySeverity.critical) {
    return AlertDisplaySeverity.critical;
  }
  final api = event.severity.toLowerCase();
  if (api == 'critical') return AlertDisplaySeverity.critical;
  if (api == 'high' ||
      api == 'medium' ||
      meta.severity == AlertDisplaySeverity.warning) {
    return AlertDisplaySeverity.warning;
  }
  if (api == 'low' || meta.severity == AlertDisplaySeverity.info) {
    return AlertDisplaySeverity.info;
  }
  return _severityFromApiBucket(event.severity);
}

String alertLocationLabel(GpsAlertEvent event) {
  final geofence = event.geofenceName?.trim();
  if (geofence != null && geofence.isNotEmpty) return geofence;
  if (event.latitude != 0 || event.longitude != 0) {
    return '${event.latitude.toStringAsFixed(3)}, '
        '${event.longitude.toStringAsFixed(3)}';
  }
  return 'Location unavailable';
}

/// True when the incident belongs on the Resolved tab.
bool alertIncidentIsResolved(AlertIncident incident) {
  final e = incident.primary;
  return e.isResolved || e.isArchived || incident.recovered;
}

List<({String label, DateTime at, AlertDisplaySeverity severity})>
    alertTimelineNodes(GpsAlertEvent e) {
  final nodes = <({String label, DateTime at, AlertDisplaySeverity severity})>[
    (label: 'Detected', at: e.timestamp, severity: alertDisplaySeverity(e)),
  ];
  if (e.readAt != null) {
    nodes.add((
      label: 'Read',
      at: e.readAt!,
      severity: AlertDisplaySeverity.info,
    ));
  }
  if (e.acknowledgedAt != null) {
    nodes.add((
      label: 'Acknowledged',
      at: e.acknowledgedAt!,
      severity: AlertDisplaySeverity.warning,
    ));
  }
  if (e.resolvedAt != null) {
    nodes.add((
      label: 'Resolved',
      at: e.resolvedAt!,
      severity: AlertDisplaySeverity.resolved,
    ));
  }
  if (e.archivedAt != null) {
    nodes.add((
      label: 'Archived',
      at: e.archivedAt!,
      severity: AlertDisplaySeverity.info,
    ));
  }
  nodes.sort((a, b) => a.at.compareTo(b.at));
  return nodes;
}
