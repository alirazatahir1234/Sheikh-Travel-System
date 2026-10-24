import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../shared/shared.module';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatDividerModule } from '@angular/material/divider';
import { ShellComponent } from './shell/shell.component';
import { AiChatComponent } from '../modules/ai/ai-chat/ai-chat.component';

@NgModule({
  declarations: [ShellComponent],
  imports: [SharedModule, RouterModule, MatToolbarModule, MatDividerModule, AiChatComponent],
  exports: [ShellComponent]
})
export class LayoutModule {}
