import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { McpCatalog, McpCatalogService } from '../../shared/mcp-catalog.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  selector: 'app-mcp-settings',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  template: `
    <div class="page" *ngIf="catalog as c">
      <header>
        <h2>Settings</h2>
        <p>Console preferences for this browser. Catalog is loaded from ERP assets.</p>
      </header>

      <section class="panel">
        <h3>Catalog</h3>
        <ul>
          <li><span>Package</span><strong>{{ c.package }}</strong></li>
          <li><span>Version</span><strong>{{ c.version }}</strong></li>
          <li><span>Exported</span><strong>{{ c.exportedAt | date: 'medium' }}</strong></li>
          <li><span>Protocol</span><strong>{{ c.protocol }}</strong></li>
          <li><span>Asset path</span><strong>assets/mcp/mcp-catalog.json</strong></li>
        </ul>
        <p class="hint">
          Refresh the catalog from SheikhGo-MCP with
          <code>npm run export:catalog</code>
          (writes <code>public/assets/mcp/mcp-catalog.json</code>).
        </p>
      </section>

      <section class="panel">
        <h3>Local activity</h3>
        <p class="hint">{{ mcp.activity().length }} event(s) stored in localStorage.</p>
        <button mat-stroked-button type="button" color="warn" (click)="clear()">
          <mat-icon>delete</mat-icon>
          Clear activity history
        </button>
      </section>

      <section class="panel">
        <h3>Permissions</h3>
        <p class="hint">
          This module reuses <code>Ai.View</code>. No separate Backend RBAC permission in v1.
        </p>
      </section>
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
        margin: 0.35rem 0 1rem;
        color: #64748b;
        font-size: 0.85rem;
      }
      .panel {
        border: 1px solid #d6e0ef;
        border-radius: 0.85rem;
        background: #fff;
        padding: 1rem;
        margin-bottom: 0.75rem;
      }
      .panel h3 {
        margin: 0 0 0.65rem;
        font-size: 0.9rem;
        color: #0f172a;
      }
      ul {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.45rem;
      }
      li {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        font-size: 0.8rem;
        color: #64748b;
      }
      li strong {
        color: #0f172a;
        text-align: right;
      }
      .hint {
        margin: 0.75rem 0 0;
        font-size: 0.78rem;
        color: #64748b;
        line-height: 1.45;
      }
      code {
        font-size: 0.72rem;
        background: #f1f5f9;
        padding: 0.05rem 0.3rem;
        border-radius: 0.25rem;
      }
    `
  ]
})
export class McpSettingsComponent implements OnInit {
  catalog: McpCatalog | null = null;

  constructor(
    readonly mcp: McpCatalogService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  clear(): void {
    this.mcp.clearActivity();
    this.toast.success('Activity cleared');
  }
}
