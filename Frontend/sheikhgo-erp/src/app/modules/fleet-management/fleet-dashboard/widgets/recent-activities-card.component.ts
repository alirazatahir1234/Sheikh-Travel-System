import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivityEvent } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-recent-activities-card',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex h-full flex-col rounded-xl border border-fleet-border bg-white p-5">
      <h3 class="mb-4 text-[12px] font-semibold uppercase tracking-wider text-fleet-text-muted">Recent Activity</h3>

      <ol class="fleet-timeline relative flex-1 space-y-5">
        @for (event of events(); track event.id) {
          <li class="relative pl-8">
            <span
              class="absolute left-0 top-1.5 h-3.5 w-3.5 rounded-full ring-4 ring-white"
              [class.bg-fleet-primary]="event.tone === 'primary'"
              [class.bg-fleet-secondary]="event.tone === 'secondary'"
              [class.bg-fleet-border]="event.tone === 'muted'"></span>
            <h4 class="text-[13px] font-semibold text-fleet-text">{{ event.title }}</h4>
            <p class="text-[11px] text-fleet-text-muted">{{ event.timeAgo }}</p>
          </li>
        } @empty {
          <li class="py-8 text-center text-sm text-fleet-text-muted">No recent activity.</li>
        }
      </ol>

      @if (events().length) {
        <a routerLink="/fleet/maintenance" class="mt-6 text-center text-[13px] font-semibold text-fleet-primary hover:underline">
          View maintenance
        </a>
      }
    </section>
  `,
  styles: [`
    :host { display: block; height: 100%; }
    .fleet-timeline:not(:has(li:only-child))::before {
      content: '';
      position: absolute;
      left: 6px;
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
