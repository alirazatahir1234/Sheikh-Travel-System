import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/dashboard/domain/dashboard_layout.dart';
import 'package:sheikh_go_driver/features/dashboard/domain/dashboard_role.dart';
import 'package:sheikh_go_driver/features/dashboard/domain/dashboard_visibility.dart';
import 'package:sheikh_go_driver/features/dashboard/presentation/dashboard_layout_registry.dart';

void main() {
  group('DashboardVisibility', () {
    test('owner-like roles see health and interactive live map', () {
      expect(DashboardVisibility.showFleetHealth(DashboardRole.owner), isTrue);
      expect(DashboardVisibility.showFleetHealth(DashboardRole.tenantAdmin), isTrue);
      expect(DashboardVisibility.showMapSummary(DashboardRole.superAdmin), isFalse);
      expect(
        DashboardVisibility.showInteractiveMap(DashboardRole.owner),
        isTrue,
      );
      expect(
        DashboardVisibility.showInteractiveMap(DashboardRole.tenantAdmin),
        isTrue,
      );
    });

    test('fleet manager sees interactive map and health', () {
      expect(DashboardVisibility.showFleetHealth(DashboardRole.fleetManager), isTrue);
      expect(
        DashboardVisibility.showInteractiveMap(DashboardRole.fleetManager),
        isTrue,
      );
      expect(DashboardVisibility.showMapSummary(DashboardRole.fleetManager), isFalse);
    });

    test('dispatcher hides health and AI attention role gate', () {
      expect(DashboardVisibility.showFleetHealth(DashboardRole.dispatcher), isFalse);
      expect(
        DashboardVisibility.roleAllows(
          DashboardRole.dispatcher,
          DashboardWidgetId.fleetHealthHeader,
        ),
        isFalse,
      );
      expect(
        DashboardVisibility.showAiAttention(DashboardRole.dispatcher),
        isFalse,
      );
    });
  });

  group('DashboardLayoutRegistry', () {
    test('fleet manager command order includes search health grid map', () {
      final ids = DashboardLayoutRegistry.widgetsFor(DashboardRole.fleetManager);
      expect(ids, contains(DashboardWidgetId.universalSearchBar));
      expect(ids, contains(DashboardWidgetId.fleetHealthHeader));
      expect(ids, contains(DashboardWidgetId.opsKpiGrid));
      expect(ids, contains(DashboardWidgetId.liveMapPreview));
      expect(ids, contains(DashboardWidgetId.attentionVehicles));
      expect(ids, isNot(contains(DashboardWidgetId.mapSummaryCard)));
    });

    test('owner layout uses interactive live map and fuel summary', () {
      final ids = DashboardLayoutRegistry.widgetsFor(DashboardRole.owner);
      expect(ids, contains(DashboardWidgetId.liveMapPreview));
      expect(ids, contains(DashboardWidgetId.fuelSummary));
      expect(ids, isNot(contains(DashboardWidgetId.mapSummaryCard)));
      final adminIds = DashboardLayoutRegistry.widgetsFor(DashboardRole.tenantAdmin);
      expect(adminIds, contains(DashboardWidgetId.liveMapPreview));
    });

    test('dispatcher has no health card', () {
      final ids = DashboardLayoutRegistry.widgetsFor(DashboardRole.dispatcher);
      expect(ids, isNot(contains(DashboardWidgetId.fleetHealthHeader)));
      expect(ids, contains(DashboardWidgetId.fleetStatsStrip));
      expect(ids, contains(DashboardWidgetId.liveMapPreview));
    });

    test('commandLabel maps PRD names', () {
      expect(DashboardRole.owner.commandLabel, 'Owner');
      expect(DashboardRole.tenantAdmin.commandLabel, 'Owner');
      expect(DashboardRole.fleetManager.commandLabel, 'Fleet Manager');
      expect(DashboardRole.driverManager.commandLabel, 'Supervisor');
    });
  });
}
