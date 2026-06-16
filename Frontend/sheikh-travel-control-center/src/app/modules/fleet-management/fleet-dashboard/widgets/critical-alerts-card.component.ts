import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CriticalAlert } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-critical-alerts-card',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-xl border border-fleet-border bg-white p-6">
      <div class="mb-4 flex items-center justify-between">
        <h3 class="font-label text-[13px] font-semibold uppercase tracking-widest text-fleet-text-muted">Critical Alerts</h3>
        @if (highCount() > 0) {
          <span class="rounded-full bg-fleet-error px-2 py-0.5 text-[10px] font-bold uppercase text-white">
            {{ highCount() }} High
          </span>
        }
      </div>

      <div class="space-y-4">
        @for (alert of alerts(); track alert.id) {
          <div
            class="flex gap-3 rounded-lg border p-3"
            [class.border-fleet-error]="alert.tone === 'error'"
            [class.bg-fleet-error-soft]="alert.tone === 'error'"
            [class.border-fleet-secondary]="alert.tone === 'warning'"
            [class.bg-fleet-secondary-soft]="alert.tone === 'warning'">
            <mat-icon
              class="shrink-0"
              [class.text-fleet-error]="alert.tone === 'error'"
              [class.text-fleet-secondary]="alert.tone === 'warning'"
              style="font-variation-settings: 'FILL' 1;">{{ alert.icon }}</mat-icon>
            <div class="min-w-0">
              <h4 class="text-[13px] font-semibold text-fleet-text">{{ alert.title }}</h4>
              <p class="mt-0.5 text-[11px] text-fleet-text-muted">{{ alert.detail }}</p>
              @if (alert.actionLabel) {
                <button type="button" class="mt-1 text-[11px] font-bold text-fleet-error underline">{{ alert.actionLabel }}</button>
              }
            </div>
          </div>
        } @empty {
          <p class="py-6 text-center text-sm text-fleet-text-muted">No active alerts.</p>
        }
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class CriticalAlertsCardComponent {
  readonly alerts = input<CriticalAlert[]>([]);

  protected readonly highCount = computed(() => this.alerts().filter((a) => a.tone === 'error').length);
}
