import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ActivityEvent } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-recent-activities-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex h-full flex-col rounded-xl border border-fleet-border bg-white p-6">
      <h3 class="mb-6 font-label text-[13px] font-semibold uppercase tracking-widest text-fleet-text-muted">Recent Activities</h3>

      <ol class="fleet-timeline relative flex-1 space-y-6">
        @for (event of events(); track event.id) {
          <li class="relative pl-8">
            <span
              class="absolute left-0 top-1.5 h-4 w-4 rounded-full ring-4 ring-white"
              [class.bg-fleet-primary]="event.tone === 'primary'"
              [class.bg-fleet-secondary]="event.tone === 'secondary'"
              [class.bg-fleet-border]="event.tone === 'muted'"></span>
            <h4 class="text-[13px] font-semibold text-fleet-text">{{ event.title }}</h4>
            <p class="text-[11px] text-fleet-text-muted">{{ event.timeAgo }}</p>
          </li>
        } @empty {
          <li class="text-sm text-fleet-text-muted">No recent activity.</li>
        }
      </ol>

      <button type="button" class="mt-8 text-center text-[13px] font-semibold text-fleet-primary hover:underline">
        View Historical Logs
      </button>
    </section>
  `,
  styles: [`
    :host { display: block; height: 100%; }
    .fleet-timeline::before {
      content: '';
      position: absolute;
      left: 7px;
      top: 8px;
      bottom: 8px;
      width: 2px;
      background: var(--fleet-border);
    }
  `]
})
export class RecentActivitiesCardComponent {
  readonly events = input<ActivityEvent[]>([]);
}
