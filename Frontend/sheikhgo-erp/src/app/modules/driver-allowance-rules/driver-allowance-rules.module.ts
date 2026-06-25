import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverAllowanceRuleListComponent } from './rule-list/rule-list.component';
import { DriverAllowanceRuleFormComponent } from './rule-form/rule-form.component';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';

const routes: Routes = [
  { path: '', component: DriverAllowanceRuleListComponent },
  { path: 'new', component: DriverAllowanceRuleFormComponent },
  { path: ':id/edit', component: DriverAllowanceRuleFormComponent }
];

@NgModule({
  declarations: [DriverAllowanceRuleListComponent, DriverAllowanceRuleFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes), UiButtonComponent, UiPageHeaderComponent]
})
export class DriverAllowanceRulesModule {}
