import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AiManagementCenterComponent } from './ai-management-center/ai-management-center.component';
import { AiChatComponent } from './ai-chat/ai-chat.component';

const routes: Routes = [
  { path: '', component: AiManagementCenterComponent },
  // Legacy /ai/chat → same premium workspace (tabs + conversations + composer)
  { path: 'chat', redirectTo: '', pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forChild(routes), AiManagementCenterComponent, AiChatComponent]
})
export class AiModule {}
