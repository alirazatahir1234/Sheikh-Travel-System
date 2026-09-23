import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import {
  McpCatalog,
  McpCatalogService,
  McpGroup,
  McpGroupStatus
} from '../../shared/mcp-catalog.service';
import { McpServerCardComponent } from '../../shared/server-card/server-card.component';
import { McpChatDockComponent } from '../../shared/chat-dock/chat-dock.component';
import { APP_LOGO_PATH, APP_PRODUCT_NAME } from '../../../../core/constants/app-brand';

@Component({
  selector: 'app-mcp-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatIconModule,
    McpServerCardComponent,
    McpChatDockComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class McpDashboardComponent implements OnInit {
  catalog: McpCatalog | null = null;
  readonly logoPath = APP_LOGO_PATH;
  readonly productName = APP_PRODUCT_NAME;

  search = '';
  statusFilter: 'all' | McpGroupStatus = 'all';
  sortBy: 'name' | 'status' | 'tools' = 'name';

  constructor(
    private mcp: McpCatalogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  askMcp(): void {
    void this.router.navigateByUrl('/mcp/chat');
  }

  openGroup(group: McpGroup): void {
    this.mcp.pushActivity('Opened server', group.name);
    void this.router.navigate(['/mcp/tools'], { queryParams: { group: group.id } });
  }

  serverSummary(): string {
    const c = this.catalog;
    if (!c) return '';
    const total = c.groups.length;
    return `${total} servers · ${c.summary.runningGroups} running · ${c.summary.plannedGroups} planned · ${c.summary.stoppedGroups} stopped`;
  }

  visibleGroups(): McpGroup[] {
    let groups = [...(this.catalog?.groups || [])];
    const q = this.search.trim().toLowerCase();
    if (q) {
      groups = groups.filter(
        g =>
          g.name.toLowerCase().includes(q) ||
          g.description.toLowerCase().includes(q) ||
          g.id.toLowerCase().includes(q)
      );
    }
    if (this.statusFilter !== 'all') {
      groups = groups.filter(g => g.status === this.statusFilter);
    }
    if (this.sortBy === 'name') {
      groups.sort((a, b) => a.name.localeCompare(b.name));
    } else if (this.sortBy === 'status') {
      const order: Record<string, number> = { running: 0, planned: 1, stopped: 2 };
      groups.sort(
        (a, b) => (order[a.status] ?? 9) - (order[b.status] ?? 9) || a.name.localeCompare(b.name)
      );
    } else if (this.sortBy === 'tools') {
      groups.sort(
        (a, b) => this.mcp.toolCount(b) - this.mcp.toolCount(a) || a.name.localeCompare(b.name)
      );
    }
    return groups;
  }
}
