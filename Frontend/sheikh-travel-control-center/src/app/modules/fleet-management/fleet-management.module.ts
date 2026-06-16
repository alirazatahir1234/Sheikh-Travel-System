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

const routes: Routes = [
  {
    path: '',
    component: FleetLayoutComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: FleetDashboardComponent },
      { path: 'compliance', component: ComplianceDashboardComponent },
      { path: 'inspections', component: InspectionListComponent },
      { path: 'assignments', component: AssignmentBoardComponent }
    ]
  }
];

@NgModule({
  declarations: [
    FleetLayoutComponent,
    FleetDashboardComponent,
    ComplianceDashboardComponent,
    InspectionListComponent,
    AssignmentBoardComponent
  ],
  imports: [
    SharedModule,
    FleetUiModule,
    UiModule,
    FleetDashboardContentComponent,
    RouterModule.forChild(routes)
  ]
})
export class FleetManagementModule {}
