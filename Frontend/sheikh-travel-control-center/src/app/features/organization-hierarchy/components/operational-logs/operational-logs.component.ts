import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditLog } from '../../models/organization.models';

@Component({
  selector: 'app-operational-logs',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-xl border border-border overflow-hidden">
      <!-- Header -->
      <div class="px-4 py-3 border-b border-border flex items-center justify-between">
        <h3 class="text-sm font-semibold text-text">Operational Logs</h3>
        <button
          type="button"
          (click)="refresh.emit()"
          class="w-6 h-6 rounded-lg hover:bg-surface-alt flex items-center justify-center text-text-muted hover:text-text transition-colors"
          title="Refresh"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
        </button>
      </div>

      <!-- Content -->
      <div class="p-4">
        <div class="relative">
          <!-- Timeline Line -->
          <div class="absolute left-[7px] top-2 bottom-2 w-0.5 bg-border"></div>

          <!-- Log Items -->
          <div class="space-y-4">
            @for (log of logs(); track log.id) {
              <div class="relative flex gap-3">
                <!-- Timeline Dot -->
                <div [class]="getDotClasses(log.severity)"></div>

                <!-- Content -->
                <div class="flex-1 min-w-0">
                  <div class="text-xs text-text-muted mb-1">
                    {{ formatTime(log.timestamp) }}
                  </div>
                  <p class="text-sm text-text leading-snug">
                    <span class="font-semibold">{{ log.userName }}</span>
                    {{ getActionText(log) }}
                  </p>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- View Full Audit Trail -->
        <button
          type="button"
          (click)="viewFullAuditTrail.emit()"
          class="mt-4 w-full py-2.5 px-4 bg-primary-50 text-primary-700 text-sm font-medium rounded-lg hover:bg-primary-100 transition-colors"
        >
          VIEW FULL AUDIT TRAIL
        </button>
      </div>
    </div>
  `,
})
export class OperationalLogsComponent {
  readonly logs = input.required<AuditLog[]>();

  readonly refresh = output<void>();
  readonly viewFullAuditTrail = output<void>();

  getDotClasses(severity: 'info' | 'warning' | 'error'): string {
    const base = 'relative z-10 w-4 h-4 rounded-full border-2 border-white flex-shrink-0';
    switch (severity) {
      case 'info':
        return `${base} bg-emerald-500`;
      case 'warning':
        return `${base} bg-blue-500`;
      case 'error':
        return `${base} bg-red-500`;
      default:
        return `${base} bg-gray-400`;
    }
  }

  formatTime(timestamp: string): string {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 60) {
      return `${diffMins} min ago`;
    } else if (diffHours < 24) {
      return `${diffHours} hours ago`;
    } else if (diffDays === 1) {
      return `Yesterday, ${date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}`;
    } else {
      return date.toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
      });
    }
  }

  getActionText(log: AuditLog): string {
    const parts = log.description.split(log.userName);
    return parts.length > 1 ? parts[1] : log.description;
  }
}
