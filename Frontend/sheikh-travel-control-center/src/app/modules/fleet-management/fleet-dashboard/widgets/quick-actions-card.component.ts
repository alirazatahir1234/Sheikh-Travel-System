import { ChangeDetectionStrategy, Component, input } from '@angular/core';
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
      <div class="grid grid-cols-2 gap-4">
        @for (action of actions(); track action.id) {
          <a
            [routerLink]="action.route"
            class="group flex flex-col items-center justify-center rounded-xl bg-fleet-surface-muted p-4 text-center transition-all hover:bg-fleet-primary/10 hover:text-fleet-primary">
            <mat-icon class="mb-2 text-fleet-primary">{{ action.icon }}</mat-icon>
            <span class="text-[13px] font-semibold">{{ action.label }}</span>
          </a>
        }
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class QuickActionsCardComponent {
  readonly actions = input<QuickAction[]>([]);
}
