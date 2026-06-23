import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';

export interface PartsFilterState {
  search: string;
}

@Component({
  selector: 'parts-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters-card">
      <label class="filter-group">
        <span class="filter-label">Part Name / Number</span>
        <input class="filter-control" type="search" placeholder="Search parts…"
          [ngModel]="filters().search" (ngModelChange)="patch({ search: $event })" />
      </label>
      <div class="filter-actions">
        <button type="button" class="btn btn--primary" (click)="apply.emit()">
          <mat-icon>search</mat-icon> Search
        </button>
        <button type="button" class="btn btn--outline" (click)="reset.emit()">
          <mat-icon>restart_alt</mat-icon> Reset
        </button>
      </div>
    </div>
  `,
  styles: [`
    .filters-card {
      display: flex; gap: 0.75rem; flex-wrap: wrap; align-items: flex-end;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px;
      box-shadow: 0 1px 3px rgba(0,0,0,.08); padding: 1rem 1.25rem; margin-bottom: 1rem;
    }
    .filter-group { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; min-width: 200px; }
    .filter-label { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    .filter-control { padding: 0.5rem 0.75rem; border: 1.5px solid #e2e8f0; border-radius: 8px; font-size: 0.8125rem; }
    .filter-control:focus { outline: none; border-color: #0B6B50; }
    .filter-actions { display: flex; gap: 0.5rem; }
    .btn { display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.8125rem; font-weight: 600; cursor: pointer; border: none; }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--outline { background: #fff; color: #475569; border: 1.5px solid #cbd5e1; }
  `]
})
export class PartsFiltersComponent {
  readonly filters = input.required<PartsFilterState>();
  readonly filtersChange = output<PartsFilterState>();
  readonly apply = output<void>();
  readonly reset = output<void>();

  patch(partial: Partial<PartsFilterState>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }
}
