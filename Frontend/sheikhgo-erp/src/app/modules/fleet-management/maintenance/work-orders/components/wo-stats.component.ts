import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { WorkOrderStats } from '../../../../../core/models/maintenance.model';

export type WoStatKey = '' | 'open' | 'inProgress' | 'completed' | 'cancelled';

@Component({
  selector: 'wo-stats',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats">
      @for (card of cards; track card.key) {
        <button type="button" class="stat-card stat-card--{{ card.tone }}"
                [class.stat-card--active]="activeKey() === card.key"
                (click)="select.emit(card.key)">
          <span class="stat-value">{{ valueFor(card.key) }}</span>
          <span class="stat-label">{{ card.label }}</span>
        </button>
      }
    </div>
  `,
  styles: [`
    .stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; }
    .stat-card {
      display: flex; flex-direction: column; align-items: flex-start; gap: 0.2rem;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.125rem;
      cursor: pointer; text-align: left; transition: border-color 0.15s, box-shadow 0.15s;
    }
    .stat-card:hover { border-color: #cbd5e1; box-shadow: 0 2px 8px rgba(15,23,42,.06); }
    .stat-card--active { border-color: #0b6b50 !important; background: #f0fdf8 !important; }
    .stat-card--info.stat-card--active { border-color: #1d4ed8 !important; background: #eff6ff !important; }
    .stat-card--warning.stat-card--active { border-color: #b45309 !important; background: #fffbeb !important; }
    .stat-card--done.stat-card--active { border-color: #0b6b50 !important; }
    .stat-card--danger.stat-card--active { border-color: #dc2626 !important; background: #fef2f2 !important; }
    .stat-value { font-size: 1.5rem; font-weight: 800; color: #0f172a; line-height: 1; }
    .stat-label { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; letter-spacing: 0.03em; }
    @media (max-width: 900px) { .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 480px) { .stats { grid-template-columns: 1fr; } }
  `]
})
export class WoStatsComponent {
  readonly stats = input<WorkOrderStats | null>(null);
  readonly activeKey = input<WoStatKey>('');
  readonly select = output<WoStatKey>();

  readonly cards = [
    { key: 'open' as const, label: 'Open', tone: 'info' },
    { key: 'inProgress' as const, label: 'In Progress', tone: 'warning' },
    { key: 'completed' as const, label: 'Completed', tone: 'done' },
    { key: 'cancelled' as const, label: 'Cancelled', tone: 'danger' }
  ];

  valueFor(key: WoStatKey): number {
    const s = this.stats();
    if (!s || !key) return 0;
    return s[key];
  }
}
