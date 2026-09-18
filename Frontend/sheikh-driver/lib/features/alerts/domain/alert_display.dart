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

/// List/detail row after pairing Offline → Online into one incident.
class AlertIncident {
  const AlertIncident({
    required this.primary,
    this.recovery,
    this.endedAt,
  });

  /// Alert opened for detail / acknowledge (offline row for incidents).
  final GpsAlertEvent primary;
  final GpsAlertEvent? recovery;
  final DateTime? endedAt;

  bool get isOfflineIncident => isOfflineAlertType(primary.eventType);

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
      primary.canAcknowledge || (recovery?.canAcknowledge ?? false);

  bool get isAcknowledged =>
      primary.isAcknowledged &&
      (recovery == null || recovery!.isAcknowledged || !recovery!.canAcknowledge);

  DateTime? get acknowledgedAt =>
      primary.acknowledgedAt ?? recovery?.acknowledgedAt;

  List<int> get acknowledgeIds {
    final ids = <int>[];
    if (primary.canAcknowledge) ids.add(primary.id);
    final r = recovery;
    if (r != null && r.canAcknowledge) ids.add(r.id);
    return ids;
  }
}

/// Pair Offline → next Online; hide paired Online rows from the main list.
List<AlertIncident> groupAlertIncidents(List<GpsAlertEvent> events) {
  if (events.isEmpty) return const [];

  final chronological = [...events]
    ..sort((a, b) => a.timestamp.compareTo(b.timestamp));

  final pairedOnlineIds = <int>{};
  final offlineToRecovery = <int, GpsAlertEvent>{};

  for (var i = 0; i < chronological.length; i++) {
    final e = chronological[i];
    if (!isOfflineAlertType(e.eventType)) continue;

    GpsAlertEvent? recovery;
    for (var j = i + 1; j < chronological.length; j++) {
      final n = chronological[j];
      if (isOnlineAlertType(n.eventType) && !pairedOnlineIds.contains(n.id)) {
        recovery = n;
        pairedOnlineIds.add(n.id);
        break;
      }
      if (isOfflineAlertType(n.eventType)) break;
    }

    if (recovery != null) {
      offlineToRecovery[e.id] = recovery;
    }
  }

  final newestFirst = [...events]
    ..sort((a, b) => b.timestamp.compareTo(a.timestamp));

  final out = <AlertIncident>[];
  for (final e in newestFirst) {
    if (pairedOnlineIds.contains(e.id)) continue;

    if (isOfflineAlertType(e.eventType)) {
      final recovery = offlineToRecovery[e.id];
      final ended = recovery?.timestamp ?? e.resolvedAt;
      out.add(AlertIncident(
        primary: e,
        recovery: recovery,
        endedAt: ended,
      ));
    } else {
      out.add(AlertIncident(primary: e));
    }
  }
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
    required this.closed,
  });

  final int total;
  final int critical;
  final int warning;
  final int closed;

  static AlertsFeedSummary fromEvents(List<GpsAlertEvent> events) {
    var critical = 0;
    var warning = 0;
    var closed = 0;
    for (final e in events) {
      if (e.isResolved || e.isArchived) {
        closed++;
        continue;
      }
      final d = alertDisplaySeverity(e);
      if (d == AlertDisplaySeverity.critical ||
          e.severity.toLowerCase() == 'critical') {
        critical++;
      } else if (d == AlertDisplaySeverity.warning ||
          e.severity.toLowerCase() == 'high' ||
          e.severity.toLowerCase() == 'medium') {
        warning++;
      }
    }
    return AlertsFeedSummary(
      total: events.length,
      critical: critical,
      warning: warning,
      closed: closed,
    );
  }
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
