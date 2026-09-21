import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { McpGroupStatus } from '../mcp-catalog.service';

@Component({
  selector: 'app-mcp-status-pill',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="pill" [ngClass]="status">
      <span class="dot" aria-hidden="true"></span>
      {{ label || status }}
    </span>
  `,
  styles: [
    `
      .pill {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.15rem 0.55rem;
        border-radius: 999px;
        font-size: 0.7rem;
        font-weight: 600;
        letter-spacing: 0.02em;
        text-transform: capitalize;
        border: 1px solid transparent;
      }
      .dot {
        width: 0.45rem;
        height: 0.45rem;
        border-radius: 50%;
        background: currentColor;
      }
      .running {
        color: #0f766e;
        background: #ccfbf1;
        border-color: #99f6e4;
      }
      .planned {
        color: #1d4ed8;
        background: #dbeafe;
        border-color: #bfdbfe;
      }
      .stopped {
        color: #64748b;
        background: #f1f5f9;
        border-color: #e2e8f0;
      }
    `
  ]
})
export class McpStatusPillComponent {
  @Input() status: McpGroupStatus | string = 'planned';
  @Input() label?: string;
}
