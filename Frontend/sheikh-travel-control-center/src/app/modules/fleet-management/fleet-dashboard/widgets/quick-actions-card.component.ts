import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { QuickAction } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-quick-actions-card',
  standalone: true,
  imports: [NgClass, RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-xl border border-fleet-border bg-white p-6">
      <h3 class="mb-4 font-label text-[13px] font-semibold uppercase tracking-widest text-fleet-text-muted">Quick Actions</h3>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        @for (action of actions(); track action.id) {
          @if (action.route) {
            <a
              [routerLink]="action.route"
              class="quick-action-card group"
              [ngClass]="toneClass(action.tone)">
              <span class="quick-action-icon" [ngClass]="toneClass(action.tone)">
                <mat-icon>{{ action.icon }}</mat-icon>
              </span>
              <span class="text-[13px] font-semibold">{{ action.label }}</span>
            </a>
          } @else {
            <button
              type="button"
              class="quick-action-card group"
              [ngClass]="toneClass(action.tone)"
              (click)="actionClick.emit(action.id)">
              <span class="quick-action-icon" [ngClass]="toneClass(action.tone)">
                <mat-icon>{{ action.icon }}</mat-icon>
              </span>
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
      gap: 0.5rem;
    }
    .quick-action-icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: rgba(100, 116, 139, 0.12);
      color: #64748b;
    }
    .quick-action-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .tone-green .quick-action-icon, .quick-action-icon.tone-green { background: #ecfdf5; color: #059669; }
    .tone-blue .quick-action-icon, .quick-action-icon.tone-blue { background: #eff6ff; color: #2563eb; }
    .tone-orange .quick-action-icon, .quick-action-icon.tone-orange { background: #fff7ed; color: #ea580c; }
    .tone-red .quick-action-icon, .quick-action-icon.tone-red { background: #fef2f2; color: #dc2626; }
    .quick-action-card:hover {
      border-color: var(--fleet-primary, #3b82f6);
      background: color-mix(in srgb, var(--fleet-primary, #3b82f6) 6%, white);
    }
  `]
})
export class QuickActionsCardComponent {
  readonly actions = input<QuickAction[]>([]);
  readonly actionClick = output<string>();

  toneClass(tone?: QuickAction['tone']): string {
    return tone ? `tone-${tone}` : 'tone-neutral';
  }
}
