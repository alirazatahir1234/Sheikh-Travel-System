import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FleetUiModule } from '../../shared/fleet-ui';
import { UiModule } from '../../shared/components/ui';

import { FleetLayoutComponent } from './fleet-layout/fleet-layout.component';
import { FleetDashboardComponent } from './fleet-dashboard/fleet-dashboard.component';
import { FleetDashboardContentComponent } from './fleet-dashboard/fleet-dashboard-content.component';
import { ComplianceDashboardComponent } from './compliance/compliance-dashboard/compliance-dashboard.component';
import { InspectionListComponent } from './inspections/inspection-list/inspection-list.component';
import { AssignmentBoardComponent } from './assignments/assignment-board/assignment-board.component';
import { AssignmentCalendarComponent } from './assignments/components/assignment-calendar.component';
import { MaintenanceDashboardComponent } from './maintenance/maintenance-dashboard/maintenance-dashboard.component';
import { MaintenanceShellComponent } from './maintenance/maintenance-shell/maintenance-shell.component';
import { MaintenanceRequestsPageComponent } from './maintenance/requests/maintenance-requests-page.component';
import { WorkOrdersPageComponent } from './maintenance/work-orders/work-orders-page.component';
import { WorkshopsVendorsPageComponent } from './maintenance/workshops/workshops-vendors-page.component';
import { ServiceSchedulerPageComponent } from './maintenance/schedules/service-scheduler-page.component';
import { ServiceHistoryPageComponent } from './maintenance/history/service-history-page.component';
import { PartsInventoryPageComponent } from './maintenance/parts/parts-inventory-page.component';

import { MaintenanceReportsPageComponent } from './maintenance/reports/maintenance-reports-page.component';

const routes: Routes = [
  {
    path: '',
    component: FleetLayoutComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: FleetDashboardComponent },
      { path: 'compliance', component: ComplianceDashboardComponent },
      { path: 'inspections', component: InspectionListComponent },
      { path: 'assignments/calendar', component: AssignmentCalendarComponent },
      { path: 'assignments', component: AssignmentBoardComponent },
      {
        path: 'maintenance',
        component: MaintenanceShellComponent,
        children: [
          { path: '', component: MaintenanceDashboardComponent },
          { path: 'requests', component: MaintenanceRequestsPageComponent },
          { path: 'work-orders', component: WorkOrdersPageComponent },
          { path: 'workshops', component: WorkshopsVendorsPageComponent },
          { path: 'schedules', component: ServiceSchedulerPageComponent },
          { path: 'history', component: ServiceHistoryPageComponent },
          { path: 'parts', component: PartsInventoryPageComponent },
          { path: 'reports', component: MaintenanceReportsPageComponent }
        ]
      }
    ]
  }
];

@NgModule({
  declarations: [
    FleetLayoutComponent,
    FleetDashboardComponent,
    ComplianceDashboardComponent,
    InspectionListComponent
  ],
  imports: [
    SharedModule,
    FleetUiModule,
    UiModule,
    FleetDashboardContentComponent,
    AssignmentBoardComponent,
    AssignmentCalendarComponent,
    MaintenanceShellComponent,
    MaintenanceReportsPageComponent,
    ServiceSchedulerPageComponent,
    ServiceHistoryPageComponent,
    WorkshopsVendorsPageComponent,
    RouterModule.forChild(routes)
  ]
})
export class FleetManagementModule {}
