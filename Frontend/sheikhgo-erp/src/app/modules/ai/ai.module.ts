import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AiManagementCenterComponent } from './ai-management-center/ai-management-center.component';

const routes: Routes = [
  { path: '', component: AiManagementCenterComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes), AiManagementCenterComponent]
})
export class AiModule {}
