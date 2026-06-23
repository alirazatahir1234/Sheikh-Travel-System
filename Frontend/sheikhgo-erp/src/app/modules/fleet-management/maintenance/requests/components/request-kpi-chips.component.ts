import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MaintenanceRequestStats } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'request-kpi-chips',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="chips">
      <button type="button" class="chip" [class.chip--active]="activeFilter() === 'Open'" (click)="filterChange.emit('Open')">
        <span class="chip-value">{{ stats()?.open ?? 0 }}</span>
        <span class="chip-label">Open</span>
      </button>
      <button type="button" class="chip chip--approved" [class.chip--active]="activeFilter() === 'Approved'" (click)="filterChange.emit('Approved')">
        <span class="chip-value">{{ stats()?.approved ?? 0 }}</span>
        <span class="chip-label">Approved</span>
      </button>
      <button type="button" class="chip chip--progress" [class.chip--active]="activeFilter() === 'InProgress'" (click)="filterChange.emit('InProgress')">
        <span class="chip-value">{{ stats()?.inProgress ?? 0 }}</span>
        <span class="chip-label">In Progress</span>
      </button>
      <button type="button" class="chip chip--all" [class.chip--active]="!activeFilter()" (click)="filterChange.emit('')">
        <span class="chip-label">All Requests</span>
      </button>
    </div>
  `,
  styles: [`
    .chips { display: flex; gap: 0.625rem; flex-wrap: wrap; margin-bottom: 1rem; }
    .chip {
      display: flex; flex-direction: column; align-items: flex-start; gap: 0.125rem;
      padding: 0.75rem 1.125rem; border-radius: 12px; border: 1px solid #fde68a;
      background: #fffbeb; cursor: pointer; min-width: 100px;
    }
    .chip--approved { border-color: #b8e6d4; background: #e8f5f0; }
    .chip--progress { border-color: #bfdbfe; background: #eff6ff; }
    .chip--all { border-color: #e2e8f0; background: #fff; flex-direction: row; align-items: center; }
    .chip--active { outline: 2px solid #0b6b50; outline-offset: 1px; }
    .chip-value { font-size: 1.375rem; font-weight: 800; color: #0b6b50; }
    .chip--approved .chip-value { color: #0b6b50; }
    .chip-label { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
  `]
})
export class RequestKpiChipsComponent {
  readonly stats = input<MaintenanceRequestStats | null>(null);
  readonly activeFilter = input('');
  readonly filterChange = output<string>();
}
