import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuditLogListComponent } from './audit-log-list/audit-log-list.component';

const routes: Routes = [
  { path: '', component: AuditLogListComponent }
];

@NgModule({
  declarations: [AuditLogListComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class AuditLogsModule {}
