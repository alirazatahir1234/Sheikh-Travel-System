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
