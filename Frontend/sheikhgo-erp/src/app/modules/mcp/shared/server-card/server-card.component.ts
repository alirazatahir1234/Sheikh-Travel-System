import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { McpGroup, McpCatalogService } from '../mcp-catalog.service';
import { McpStatusPillComponent } from '../status-pill/status-pill.component';

@Component({
  selector: 'app-mcp-server-card',
  standalone: true,
  imports: [CommonModule, MatIconModule, McpStatusPillComponent],
  template: `
    <article class="card" [class.muted]="group.status !== 'running'">
      <div class="top">
        <span class="icon-wrap" [attr.data-status]="group.status" aria-hidden="true">
          <mat-icon>{{ group.icon || 'extension' }}</mat-icon>
        </span>
        <app-mcp-status-pill [status]="group.status"></app-mcp-status-pill>
      </div>
      <h3>{{ group.name }}</h3>
      <p>{{ group.description }}</p>
      <div class="meta">
        <span>{{ catalog.toolCount(group) }} tools</span>
        <span class="meta-status">{{ statusHint() }}</span>
      </div>
      <button type="button" class="action" (click)="open.emit(group)">
        {{ actionLabel() }}
        <mat-icon>arrow_forward</mat-icon>
      </button>
    </article>
  `,
  styles: [
    `
      .card {
        display: flex;
        flex-direction: column;
        gap: 10px;
        height: 100%;
        padding: 16px;
        border-radius: 12px;
        border: 1px solid var(--stb-border, #e2e8f0);
        background: var(--stb-surface, #fff);
        box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04), 0 4px 12px rgba(15, 23, 42, 0.04);
        transition: border-color 0.15s ease, box-shadow 0.15s ease;
      }
      .card:hover {
        border-color: rgba(15, 118, 110, 0.35);
        box-shadow: 0 4px 16px rgba(15, 23, 42, 0.08);
      }
      .card.muted {
        opacity: 0.96;
      }
      .top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
      }
      .icon-wrap {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 40px;
        height: 40px;
        border-radius: 10px;
        background: var(--stb-primary-50, #ecfdf5);
        color: var(--stb-primary, #0f766e);
      }
      .icon-wrap[data-status='planned'] {
        background: #dbeafe;
        color: var(--stb-info, #3b82f6);
      }
      .icon-wrap[data-status='stopped'] {
        background: #fee2e2;
        color: var(--stb-danger, #ef4444);
      }
      .icon-wrap mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
      }
      h3 {
        margin: 0;
        font-size: 15px;
        font-weight: 700;
        color: var(--stb-text, #0f172a);
      }
      p {
        margin: 0;
        flex: 1;
        font-size: 13px;
        line-height: 1.45;
        color: var(--stb-text-muted, #64748b);
        min-height: 2.4em;
      }
      .meta {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 8px;
        font-size: 12px;
        color: var(--stb-text-muted, #64748b);
        font-weight: 600;
      }
      .meta-status {
        color: var(--stb-text-soft, #94a3b8);
        font-weight: 500;
      }
      .action {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 4px;
        width: 100%;
        margin-top: 4px;
        padding: 8px 12px;
        border-radius: 10px;
        border: 1px solid var(--stb-border, #e2e8f0);
        background: var(--stb-surface-alt, #f8fafc);
        color: var(--fleet-primary, #005f49);
        font-size: 13px;
        font-weight: 700;
        cursor: pointer;
        transition: background 0.15s ease, border-color 0.15s ease;
      }
      .action mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }
      .action:hover {
        background: var(--stb-primary-50, #ecfdf5);
        border-color: rgba(15, 118, 110, 0.35);
      }
      .action:focus-visible {
        outline: 2px solid var(--stb-primary, #0f766e);
        outline-offset: 2px;
      }
    `
  ]
})
export class McpServerCardComponent {
  @Input({ required: true }) group!: McpGroup;
  @Output() open = new EventEmitter<McpGroup>();

  constructor(readonly catalog: McpCatalogService) {}

  actionLabel(): string {
    if (this.group.status === 'running') return 'Open Console';
    if (this.group.status === 'planned') return 'Configure';
    return 'View Tools';
  }

  statusHint(): string {
    if (this.group.status === 'running') return 'Active in catalog';
    if (this.group.status === 'planned') return 'Not started';
    return 'Stopped in catalog';
  }
}
