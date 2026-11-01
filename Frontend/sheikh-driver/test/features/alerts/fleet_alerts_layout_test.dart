import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/features/alerts/domain/alert_display.dart';
import 'package:sheikh_go_driver/features/alerts/domain/gps_alert_models.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/alerts_notifier.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/alert_empty_state.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/alert_error_state.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/alert_loading_state.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/alert_severity_filters.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/alert_status_tabs.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/fleet_alert_card.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/fleet_alerts_header.dart';

GpsAlertEvent ev({
  required int id,
  required String type,
  String severity = 'medium',
  String status = 'active',
  String? plate,
  String? geofence,
}) =>
    GpsAlertEvent(
      id: id,
      vehicleId: 2,
      vehicleName: 'Toyota Corolla',
      registrationNumber: plate ?? 'LEB-8825',
      eventType: type,
      latitude: 32.26,
      longitude: 74.66,
      speed: 0,
      message: 'test $type',
      timestamp: DateTime.now().toUtc().subtract(const Duration(minutes: 2)),
      isAcknowledged: false,
      severity: severity,
      status: status,
      geofenceName: geofence ?? 'Lahore Ring Road',
      driverName: 'Imran',
      canAcknowledge: true,
      canAssign: true,
    );

void main() {
  testWidgets('status tabs and severity chips render', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Column(
            children: [
              AlertStatusTabs(
                selected: FleetAlertStatusTab.active,
                activeCount: 4,
                resolvedCount: null,
                onChanged: (_) {},
              ),
              AlertSeverityFilters(
                selected: const {},
                criticalCount: 1,
                warningCount: 2,
                infoCount: 1,
                onToggle: (_) {},
              ),
            ],
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.text('Active (4)'), findsOneWidget);
    expect(find.text('Resolved'), findsOneWidget);
    expect(find.text('Critical (1)'), findsOneWidget);
    expect(find.text('Warning (2)'), findsOneWidget);
    expect(find.text('Info (1)'), findsOneWidget);
  });

  testWidgets('alert card shows mockup fields', (tester) async {
    final incidents = groupAlertIncidents([
      ev(
          id: 1,
          type: 'speed_exceeded',
          severity: 'critical',
          plate: 'LHR-4587'),
    ]);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: FleetAlertCard(
            incident: incidents.first,
            onTap: () {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.textContaining('Over Speed'), findsOneWidget);
    expect(find.text('LHR-4587'), findsOneWidget);
    expect(find.text('Lahore Ring Road'), findsOneWidget);
    expect(find.textContaining('ago'), findsOneWidget);
  });

  testWidgets('empty / error / loading states render', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: AlertEmptyState(kind: AlertEmptyKind.allClear),
        ),
      ),
    );
    expect(find.text('All Clear!'), findsOneWidget);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: AlertErrorState(onRetry: () {}),
        ),
      ),
    );
    expect(find.text("Couldn't Load Alerts"), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: AlertLoadingState(count: 2)),
      ),
    );
    await tester.pump(const Duration(milliseconds: 200));
    expect(tester.takeException(), isNull);
  });

  testWidgets('full alerts body with cards', (tester) async {
    final incidents = groupAlertIncidents([
      ev(id: 1, type: 'low_battery'),
      ev(id: 2, type: 'ignition_off'),
      ev(id: 4, type: 'speed_exceeded', severity: 'high'),
    ]);

    final router = GoRouter(routes: [
      GoRoute(
        path: '/',
        builder: (_, __) => Scaffold(
          backgroundColor: const Color(0xFFF3F7F9),
          body: SafeArea(
            bottom: false,
            child: Column(
              children: [
                FleetAlertsHeader(
                  companyName: 'Sheikh Travel',
                  vehicleCount: 4,
                  unreadNotifications: 3,
                  onBell: () {},
                ),
                AlertStatusTabs(
                  selected: FleetAlertStatusTab.active,
                  activeCount: incidents.length,
                  resolvedCount: null,
                  onChanged: (_) {},
                ),
                AlertSeverityFilters(
                  selected: const {},
                  criticalCount: 0,
                  warningCount: 2,
                  infoCount: 1,
                  onToggle: (_) {},
                ),
                Expanded(
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: incidents.length,
                    itemBuilder: (_, i) => FleetAlertCard(
                      incident: incidents[i],
                      onTap: () {},
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    ]);

    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.text('Fleet Alerts'), findsOneWidget);
    expect(find.textContaining('LEB-8825'), findsWidgets);
  });
}
