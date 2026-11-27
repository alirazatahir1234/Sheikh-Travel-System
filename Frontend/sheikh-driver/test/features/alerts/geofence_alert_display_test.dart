import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/alerts/domain/alert_display.dart';
import 'package:sheikh_go_driver/features/alerts/domain/gps_alert_models.dart';

GpsAlertEvent _event(String type) => GpsAlertEvent(
      id: 1,
      vehicleId: 2,
      eventType: type,
      latitude: 32.26,
      longitude: 74.66,
      speed: 0,
      message: 'test $type',
      timestamp: DateTime.utc(2026, 11, 27, 9),
      isAcknowledged: false,
      severity: 'high',
      status: 'new',
      geofenceName: 'Pasrur Head Office',
    );

void main() {
  // The geofence rule alerts the server can now raise (GeofenceRuleEvents.cs).
  const ruleTypes = {
    'geofence_dwell': ('Stayed Too Long', AlertDisplaySeverity.warning),
    'geofence_overspeed': (
      'Speeding Inside Geofence',
      AlertDisplaySeverity.warning
    ),
    'geofence_idle': ('Idling Inside Geofence', AlertDisplaySeverity.warning),
    'geofence_not_arrived': (
      'No Vehicle Arrived',
      AlertDisplaySeverity.critical
    ),
    'geofence_after_hours_exit': (
      'Left Outside Hours',
      AlertDisplaySeverity.critical
    ),
  };

  test('geofence rule alerts have their own titles and severities', () {
    for (final entry in ruleTypes.entries) {
      final meta = alertTypeMeta(entry.key);
      expect(meta.title, entry.value.$1, reason: entry.key);
      expect(meta.severity, entry.value.$2, reason: entry.key);
      expect(meta.description, isNotEmpty);
    }
  });

  test('geofence rule alerts sit under the Geofence filter', () {
    for (final type in ruleTypes.keys) {
      expect(alertCategoryFor(_event(type)), AlertCategory.geofence,
          reason: type);
      expect(matchesAlertCategory(_event(type), AlertCategory.geofence), isTrue,
          reason: type);
    }
  });

  test('geofence rule alerts get their own icons, not the generic bell', () {
    for (final type in ruleTypes.keys) {
      expect(alertCategoryIcon(_event(type)),
          isNot(Icons.notifications_active_outlined),
          reason: type);
      expect(
          alertTypeSquareIcon(type), isNot(Icons.notifications_active_outlined),
          reason: type);
    }
  });

  test('entry and exit alerts keep their existing labels', () {
    expect(alertTypeMeta('geofence_enter').title, 'Geofence Entered');
    expect(alertTypeMeta('geofence_exited').title, 'Geofence Exited');
    expect(alertCategoryFor(_event('geofence_enter')), AlertCategory.geofence);
  });
}
