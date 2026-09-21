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
    <button type="button" class="card" [class.muted]="group.status !== 'running'" (click)="open.emit(group)">
      <div class="top">
        <span class="icon-wrap">
          <mat-icon>{{ group.icon || 'extension' }}</mat-icon>
        </span>
        <app-mcp-status-pill [status]="group.status"></app-mcp-status-pill>
      </div>
      <h3>{{ group.name }}</h3>
      <p>{{ group.description }}</p>
      <div class="meta">
        <span>{{ catalog.toolCount(group) }} tools</span>
        <span class="arrow">Open →</span>
      </div>
    </button>
  `,
  styles: [
    `
      .card {
        display: flex;
        flex-direction: column;
        gap: 0.55rem;
        width: 100%;
        text-align: left;
        padding: 1rem 1.1rem;
        border-radius: 0.85rem;
        border: 1px solid #dbe3f0;
        background: linear-gradient(165deg, #ffffff 0%, #f5f8fc 100%);
        box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
        cursor: pointer;
        transition: border-color 0.15s ease, box-shadow 0.15s ease, transform 0.15s ease;
      }
      .card:hover {
        border-color: #93c5fd;
        box-shadow: 0 8px 20px rgba(30, 64, 175, 0.08);
        transform: translateY(-1px);
      }
      .card.muted {
        opacity: 0.92;
      }
      .top {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }
      .icon-wrap {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.25rem;
        height: 2.25rem;
        border-radius: 0.65rem;
        background: #1e3a5f;
        color: #e0f2fe;
      }
      .icon-wrap mat-icon {
        font-size: 1.15rem;
        width: 1.15rem;
        height: 1.15rem;
      }
      h3 {
        margin: 0;
        font-size: 0.95rem;
        font-weight: 700;
        color: #0f172a;
      }
      p {
        margin: 0;
        font-size: 0.8rem;
        line-height: 1.4;
        color: #64748b;
        min-height: 2.2rem;
      }
      .meta {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-top: 0.25rem;
        font-size: 0.75rem;
        color: #475569;
        font-weight: 600;
      }
      .arrow {
        color: #2563eb;
      }
    `
  ]
})
export class McpServerCardComponent {
  @Input({ required: true }) group!: McpGroup;
  @Output() open = new EventEmitter<McpGroup>();

  constructor(readonly catalog: McpCatalogService) {}
}
