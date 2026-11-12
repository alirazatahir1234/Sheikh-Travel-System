import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../shared/shared.module';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatDividerModule } from '@angular/material/divider';
import { ShellComponent } from './shell/shell.component';
import { AiChatComponent } from '../modules/ai/ai-chat/ai-chat.component';
import { GlobalCompanyScopeSelectComponent } from '../shared/components/company-scope/global-company-scope-select.component';

@NgModule({
  declarations: [ShellComponent],
  imports: [
    SharedModule,
    RouterModule,
    MatToolbarModule,
    MatDividerModule,
    AiChatComponent,
    GlobalCompanyScopeSelectComponent
  ],
  exports: [ShellComponent]
})
export class LayoutModule {}
