import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { McpGroupStatus } from '../mcp-catalog.service';

@Component({
  selector: 'app-mcp-status-pill',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="pill" [ngClass]="statusClass" [attr.aria-label]="'Status: ' + displayLabel">
      <span class="dot" aria-hidden="true"></span>
      {{ displayLabel }}
    </span>
  `,
  styles: [
    `
      .pill {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 3px 10px;
        border-radius: 999px;
        font-size: 12px;
        font-weight: 600;
        letter-spacing: 0.01em;
        border: 1px solid transparent;
        white-space: nowrap;
      }
      .dot {
        width: 7px;
        height: 7px;
        border-radius: 50%;
        background: currentColor;
        flex-shrink: 0;
      }
      .running {
        color: #047857;
        background: #d1fae5;
        border-color: #a7f3d0;
      }
      .planned {
        color: #1d4ed8;
        background: #dbeafe;
        border-color: #bfdbfe;
      }
      .stopped {
        color: #b91c1c;
        background: #fee2e2;
        border-color: #fecaca;
      }
      .unknown,
      .loading {
        color: #475569;
        background: #f1f5f9;
        border-color: #e2e8f0;
      }
    `
  ]
})
export class McpStatusPillComponent {
  @Input() status: McpGroupStatus | string = 'planned';
  @Input() label?: string;

  get statusClass(): string {
    const s = (this.status || 'planned').toLowerCase();
    if (s === 'running' || s === 'connected' || s === 'ready') return 'running';
    if (s === 'stopped' || s === 'error' || s === 'disconnected' || s === 'attention') return 'stopped';
    if (s === 'loading' || s === 'unknown') return 'unknown';
    return 'planned';
  }

  get displayLabel(): string {
    if (this.label) return this.label;
    const s = (this.status || 'planned').toString();
    return s.charAt(0).toUpperCase() + s.slice(1);
  }
}
