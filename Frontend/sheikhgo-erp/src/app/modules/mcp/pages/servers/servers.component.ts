import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import {
  McpCatalog,
  McpCatalogService,
  McpGroup,
  McpGroupStatus
} from '../../shared/mcp-catalog.service';
import { McpServerCardComponent } from '../../shared/server-card/server-card.component';

@Component({
  selector: 'app-mcp-servers',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, McpServerCardComponent],
  template: `
    <div class="page" *ngIf="catalog as c">
      <header>
        <div>
          <h2>MCP Servers</h2>
          <p>
            {{ c.groups.length }} servers · {{ c.summary.runningGroups }} running ·
            {{ c.summary.plannedGroups }} planned · {{ c.summary.stoppedGroups }} stopped
          </p>
        </div>
      </header>

      <div class="controls">
        <label class="search-field">
          <span class="sr-only">Search servers</span>
          <mat-icon aria-hidden="true">search</mat-icon>
          <input type="search" [(ngModel)]="search" placeholder="Search servers..." autocomplete="off" />
        </label>
        <div class="filters" role="group" aria-label="Filter by status">
          <button type="button" [class.active]="filter === 'all'" (click)="filter = 'all'">All</button>
          <button type="button" [class.active]="filter === 'running'" (click)="filter = 'running'">Running</button>
          <button type="button" [class.active]="filter === 'planned'" (click)="filter = 'planned'">Planned</button>
          <button type="button" [class.active]="filter === 'stopped'" (click)="filter = 'stopped'">Stopped</button>
        </div>
      </div>

      <div class="grid">
        @for (g of visible(); track g.id) {
          <app-mcp-server-card [group]="g" (open)="open($event)"></app-mcp-server-card>
        } @empty {
          <p class="empty">No servers match your filters.</p>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .page {
        --mcp-green: var(--fleet-primary, #005f49);
        --mcp-teal: var(--stb-primary, #0f766e);
        --mcp-border: var(--stb-border, #e2e8f0);
        --mcp-text: var(--stb-text, #0f172a);
        --mcp-muted: var(--stb-text-muted, #64748b);
      }
      .sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border: 0;
      }
      header h2 {
        margin: 0;
        font-size: 18px;
        font-weight: 700;
        color: var(--mcp-text);
      }
      header p {
        margin: 4px 0 16px;
        color: var(--mcp-muted);
        font-size: 13px;
      }
      .controls {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        margin-bottom: 14px;
        align-items: center;
      }
      .search-field {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        flex: 1 1 180px;
        max-width: 280px;
        padding: 0 12px;
        height: 40px;
        border: 1px solid var(--mcp-border);
        border-radius: 10px;
        background: #fff;
      }
      .search-field mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        color: var(--mcp-muted);
      }
      .search-field input {
        flex: 1;
        border: none;
        outline: none;
        background: transparent;
        font-size: 13px;
        min-width: 0;
      }
      .filters {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
      }
      .filters button {
        border: 1px solid var(--mcp-border);
        background: #fff;
        border-radius: 999px;
        padding: 6px 14px;
        font-size: 12px;
        font-weight: 700;
        color: var(--mcp-muted);
        cursor: pointer;
      }
      .filters button.active {
        background: var(--mcp-green);
        border-color: var(--mcp-green);
        color: #fff;
      }
      .filters button:focus-visible {
        outline: 2px solid var(--mcp-teal);
        outline-offset: 2px;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
        gap: 12px;
      }
      .empty {
        grid-column: 1 / -1;
        margin: 0;
        padding: 24px;
        text-align: center;
        color: var(--mcp-muted);
        border: 1px dashed var(--mcp-border);
        border-radius: 12px;
        background: #fff;
      }
    `
  ]
})
export class McpServersComponent implements OnInit {
  catalog: McpCatalog | null = null;
  filter: 'all' | McpGroupStatus = 'all';
  search = '';

  constructor(
    private mcp: McpCatalogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  visible(): McpGroup[] {
    let groups = this.catalog?.groups || [];
    const q = this.search.trim().toLowerCase();
    if (q) {
      groups = groups.filter(
        g =>
          g.name.toLowerCase().includes(q) ||
          g.description.toLowerCase().includes(q) ||
          g.id.toLowerCase().includes(q)
      );
    }
    if (this.filter === 'all') return groups;
    return groups.filter(g => g.status === this.filter);
  }

  open(group: McpGroup): void {
    this.mcp.pushActivity('Opened server', group.name);
    void this.router.navigate(['/mcp/tools'], { queryParams: { group: group.id } });
  }
}
