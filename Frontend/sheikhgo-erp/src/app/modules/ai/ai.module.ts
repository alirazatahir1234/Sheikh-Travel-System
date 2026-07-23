import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AiManagementCenterComponent } from './ai-management-center/ai-management-center.component';
import { AiChatComponent } from './ai-chat/ai-chat.component';

const routes: Routes = [
  { path: '', component: AiManagementCenterComponent },
  { path: 'chat', component: AiChatComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes), AiManagementCenterComponent, AiChatComponent]
})
export class AiModule {}
