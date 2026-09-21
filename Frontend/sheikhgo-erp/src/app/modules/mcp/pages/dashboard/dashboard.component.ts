import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import {
  McpCatalog,
  McpCatalogService,
  McpGroup,
  McpQuickAction
} from '../../shared/mcp-catalog.service';
import { McpServerCardComponent } from '../../shared/server-card/server-card.component';
import { McpChatDockComponent } from '../../shared/chat-dock/chat-dock.component';

@Component({
  selector: 'app-mcp-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    McpServerCardComponent,
    McpChatDockComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class McpDashboardComponent implements OnInit {
  catalog: McpCatalog | null = null;

  constructor(
    private mcp: McpCatalogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  onQuickAction(action: McpQuickAction): void {
    if (action.route) {
      void this.router.navigateByUrl(action.route);
      return;
    }
    if (action.prompt) {
      this.mcp.setPendingPrompt(action.prompt, action.groupId);
      void this.router.navigateByUrl('/mcp/chat');
    }
  }

  openGroup(group: McpGroup): void {
    this.mcp.pushActivity('Opened server', group.name);
    void this.router.navigate(['/mcp/tools'], { queryParams: { group: group.id } });
  }
}
