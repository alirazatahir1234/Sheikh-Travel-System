import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverAllowanceRuleListComponent } from './rule-list/rule-list.component';
import { DriverAllowanceRuleFormComponent } from './rule-form/rule-form.component';

const routes: Routes = [
  { path: '', component: DriverAllowanceRuleListComponent },
  { path: 'new', component: DriverAllowanceRuleFormComponent },
  { path: ':id/edit', component: DriverAllowanceRuleFormComponent }
];

@NgModule({
  declarations: [DriverAllowanceRuleListComponent, DriverAllowanceRuleFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriverAllowanceRulesModule {}
