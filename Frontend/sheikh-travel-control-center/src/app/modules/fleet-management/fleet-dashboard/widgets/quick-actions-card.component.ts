import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { QuickAction } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-quick-actions-card',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-xl border border-fleet-border bg-white p-6">
      <h3 class="mb-4 font-label text-[13px] font-semibold uppercase tracking-widest text-fleet-text-muted">Quick Actions</h3>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        @for (action of actions(); track action.id) {
          @if (action.route) {
            <a
              [routerLink]="action.route"
              class="quick-action-card group">
              <mat-icon class="mb-2 text-fleet-primary">{{ action.icon }}</mat-icon>
              <span class="text-[13px] font-semibold">{{ action.label }}</span>
            </a>
          } @else {
            <button
              type="button"
              class="quick-action-card group"
              (click)="actionClick.emit(action.id)">
              <mat-icon class="mb-2 text-fleet-primary">{{ action.icon }}</mat-icon>
              <span class="text-[13px] font-semibold">{{ action.label }}</span>
            </button>
          }
        }
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
    .quick-action-card {
      display: flex;
      min-height: 104px;
      width: 100%;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      border-radius: 0.75rem;
      border: 1px solid var(--fleet-border, #e2e8f0);
      background: var(--fleet-surface-muted, #f8fafc);
      padding: 1rem;
      text-align: center;
      transition: background 0.15s, border-color 0.15s, color 0.15s;
      cursor: pointer;
      color: var(--fleet-text, #0f172a);
    }
    .quick-action-card:hover {
      background: color-mix(in srgb, var(--fleet-primary, #3b82f6) 10%, white);
      border-color: var(--fleet-primary, #3b82f6);
      color: var(--fleet-primary, #3b82f6);
    }
  `]
})
export class QuickActionsCardComponent {
  readonly actions = input<QuickAction[]>([]);
  readonly actionClick = output<string>();
}
