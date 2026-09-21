import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { McpCatalog, McpCatalogService, McpGroup } from '../../shared/mcp-catalog.service';
import { McpServerCardComponent } from '../../shared/server-card/server-card.component';

@Component({
  selector: 'app-mcp-servers',
  standalone: true,
  imports: [CommonModule, McpServerCardComponent],
  template: `
    <div class="page" *ngIf="catalog as c">
      <header>
        <h2>MCP servers</h2>
        <p>Twelve tool-group cards from the exported catalog. Running groups map to live SheikhGo-MCP tools.</p>
      </header>
      <div class="filters">
        <button type="button" [class.active]="filter === 'all'" (click)="filter = 'all'">All</button>
        <button type="button" [class.active]="filter === 'running'" (click)="filter = 'running'">Running</button>
        <button type="button" [class.active]="filter === 'planned'" (click)="filter = 'planned'">Planned</button>
        <button type="button" [class.active]="filter === 'stopped'" (click)="filter = 'stopped'">Stopped</button>
      </div>
      <div class="grid">
        @for (g of visible(); track g.id) {
          <app-mcp-server-card [group]="g" (open)="open($event)"></app-mcp-server-card>
        }
      </div>
    </div>
  `,
  styles: [
    `
      header h2 {
        margin: 0;
        font-size: 1.15rem;
        color: #0f172a;
      }
      header p {
        margin: 0.35rem 0 0.9rem;
        color: #64748b;
        font-size: 0.85rem;
      }
      .filters {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
        margin-bottom: 0.85rem;
      }
      .filters button {
        border: 1px solid #d6e0ef;
        background: #fff;
        border-radius: 999px;
        padding: 0.3rem 0.75rem;
        font-size: 0.75rem;
        font-weight: 700;
        color: #475569;
        cursor: pointer;
      }
      .filters button.active {
        background: #1e3a5f;
        border-color: #1e3a5f;
        color: #fff;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(15.5rem, 1fr));
        gap: 0.75rem;
      }
    `
  ]
})
export class McpServersComponent implements OnInit {
  catalog: McpCatalog | null = null;
  filter: 'all' | 'running' | 'planned' | 'stopped' = 'all';

  constructor(
    private mcp: McpCatalogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  visible(): McpGroup[] {
    const groups = this.catalog?.groups || [];
    if (this.filter === 'all') return groups;
    return groups.filter(g => g.status === this.filter);
  }

  open(group: McpGroup): void {
    this.mcp.pushActivity('Opened server', group.name);
    void this.router.navigate(['/mcp/tools'], { queryParams: { group: group.id } });
  }
}
