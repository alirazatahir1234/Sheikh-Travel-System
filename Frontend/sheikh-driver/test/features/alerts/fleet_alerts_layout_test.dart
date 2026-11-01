import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/features/alerts/domain/alert_display.dart';
import 'package:sheikh_go_driver/features/alerts/domain/gps_alert_models.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/alerts_notifier.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/fleet_alert_row.dart';
import 'package:sheikh_go_driver/features/alerts/presentation/widgets/fleet_alerts_header.dart';

GpsAlertEvent ev({
  required int id,
  required String type,
  String severity = 'medium',
}) =>
    GpsAlertEvent(
      id: id,
      vehicleId: 2,
      vehicleName: 'Toyota Corolla',
      registrationNumber: 'LEB-8825',
      eventType: type,
      latitude: 32.26,
      longitude: 74.66,
      speed: 0,
      message: 'test $type',
      timestamp: DateTime.now().toUtc(),
      isAcknowledged: false,
      severity: severity,
      status: 'active',
      driverName: 'Imran',
      canAcknowledge: true,
      canAssign: true,
    );

void main() {
  testWidgets('KPI row does not overflow', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: FleetAlertKpiRow(
            summary: AlertsFeedSummary(
              total: 23,
              critical: 0,
              warning: 10,
              info: 13,
              unacknowledged: 20,
              closed: 0,
            ),
            selected: FleetAlertChip.all,
            unacknowledgedSelected: false,
            onSelect: _noopChip,
            onUnacknowledged: _noop,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.text('Critical'), findsOneWidget);
    expect(find.text('20'), findsOneWidget);
  });

  testWidgets('full alerts body with rows', (tester) async {
    final incidents = groupAlertIncidents([
      ev(id: 1, type: 'low_battery'),
      ev(id: 2, type: 'ignition_off'),
      ev(id: 3, type: 'ignition_on'),
      ev(id: 4, type: 'speed_exceeded', severity: 'high'),
      ev(id: 5, type: 'speed_exceeded', severity: 'high'),
    ]);

    final router = GoRouter(routes: [
      GoRoute(
        path: '/',
        builder: (_, __) => Scaffold(
          backgroundColor: const Color(0xFFF3F7F8),
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
                Expanded(
                  child: CustomScrollView(
                    slivers: [
                      const SliverToBoxAdapter(
                        child: FleetAlertKpiRow(
                          summary: AlertsFeedSummary(
                            total: 23,
                            critical: 0,
                            warning: 10,
                            info: 13,
                            unacknowledged: 20,
                            closed: 0,
                          ),
                          selected: FleetAlertChip.all,
                          unacknowledgedSelected: false,
                          onSelect: _noopChip,
                          onUnacknowledged: _noop,
                        ),
                      ),
                      SliverList.builder(
                        itemCount: incidents.length,
                        itemBuilder: (_, i) => FleetAlertRow(
                          incident: incidents[i],
                          selected: false,
                          selectionMode: false,
                          onTap: _noop,
                          onToggleSelect: _noop,
                        ),
                      ),
                    ],
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

void _noop() {}
void _noopChip(FleetAlertChip c) {}
