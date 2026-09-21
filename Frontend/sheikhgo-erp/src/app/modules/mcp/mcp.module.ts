import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { McpShellComponent } from './mcp-shell/mcp-shell.component';
import { McpDashboardComponent } from './pages/dashboard/dashboard.component';
import { McpServersComponent } from './pages/servers/servers.component';
import { McpToolsComponent } from './pages/tools/tools.component';
import { McpChatComponent } from './pages/chat/chat.component';
import { McpConnectionsComponent } from './pages/connections/connections.component';
import { McpSettingsComponent } from './pages/settings/settings.component';

const routes: Routes = [
  {
    path: '',
    component: McpShellComponent,
    children: [
      { path: '', component: McpDashboardComponent },
      { path: 'servers', component: McpServersComponent },
      { path: 'tools', component: McpToolsComponent },
      { path: 'chat', component: McpChatComponent },
      { path: 'connections', component: McpConnectionsComponent },
      { path: 'settings', component: McpSettingsComponent }
    ]
  }
];

@NgModule({
  imports: [
    RouterModule.forChild(routes),
    McpShellComponent,
    McpDashboardComponent,
    McpServersComponent,
    McpToolsComponent,
    McpChatComponent,
    McpConnectionsComponent,
    McpSettingsComponent
  ]
})
export class McpModule {}
