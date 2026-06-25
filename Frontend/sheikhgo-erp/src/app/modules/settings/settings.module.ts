import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { SettingsLayoutComponent } from './settings-layout/settings-layout.component';
import { SettingsPageComponent } from './settings-page/settings-page.component';
import { DynamicSettingsFormComponent } from './components/dynamic-settings-form/dynamic-settings-form.component';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';

const routes: Routes = [
  {
    path: '',
    component: SettingsLayoutComponent,
    children: [
      { path: '', redirectTo: 'general', pathMatch: 'full' },
      { path: ':category', component: SettingsPageComponent }
    ]
  }
];

@NgModule({
  declarations: [
    SettingsLayoutComponent,
    SettingsPageComponent,
    DynamicSettingsFormComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes), UiButtonComponent, UiPageHeaderComponent]
})
export class SettingsModule {}
