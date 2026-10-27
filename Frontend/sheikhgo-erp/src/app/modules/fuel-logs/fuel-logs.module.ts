import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FuelLogListComponent } from './fuel-log-list/fuel-log-list.component';
import { FuelLogFormComponent } from './fuel-log-form/fuel-log-form.component';
import { FuelLogAnalyticsComponent } from './fuel-log-analytics/fuel-log-analytics.component';
import { FuelEfficiencyReportComponent } from './fuel-efficiency-report/fuel-efficiency-report.component';
import { FuelTelemetryPanelComponent } from './fuel-telemetry-panel/fuel-telemetry-panel.component';
import { FuelFraudEventsPageComponent } from './fuel-fraud-events/fuel-fraud-events-page.component';

const routes: Routes = [
  { path: '', component: FuelLogListComponent },
  { path: 'fraud-events', component: FuelFraudEventsPageComponent },
  { path: 'efficiency', component: FuelEfficiencyReportComponent },
  { path: 'telemetry', component: FuelTelemetryPanelComponent },
  { path: 'new', component: FuelLogFormComponent },
  { path: ':id/edit', component: FuelLogFormComponent }
];

@NgModule({
  declarations: [
    FuelLogListComponent,
    FuelLogFormComponent,
    FuelLogAnalyticsComponent,
    FuelEfficiencyReportComponent,
    FuelTelemetryPanelComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class FuelLogsModule {}
