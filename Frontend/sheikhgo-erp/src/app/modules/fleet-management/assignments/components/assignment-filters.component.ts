import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import {
  ASSIGNMENT_STATUSES,
  ASSIGNMENT_TYPES,
  FleetAssignmentFilters
} from '../../../../core/models/fleet-assignment.model';

@Component({
  selector: 'assignment-filters',
  standalone: true,
  imports: [FormsModule, MatIconModule, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters">
      <div class="search-wrap">
        <mat-icon class="search-icon">search</mat-icon>
        <input class="search-input" type="text" placeholder="Search assignment no., vehicle, driver…"
          [ngModel]="filters().search" (ngModelChange)="patch('search', $event)" (keyup.enter)="apply.emit()" />
      </div>

      <ui-select label="" placeholder="All vehicles" [searchable]="true" [options]="vehicleOptions()"
        [ngModel]="filters().vehicleId || null" (ngModelChange)="patch('vehicleId', $event ?? '')" />

      <ui-select label="" placeholder="All drivers" [searchable]="true" [options]="driverOptions()"
        [ngModel]="filters().driverId || null" (ngModelChange)="patch('driverId', $event ?? '')" />

      <select class="filter-select" [ngModel]="filters().status" (ngModelChange)="patch('status', $event); apply.emit()">
        <option value="">All Status</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>

      <select class="filter-select" [ngModel]="filters().assignmentType" (ngModelChange)="patch('assignmentType', $event); apply.emit()">
        <option value="">All Types</option>
        @for (t of types; track t) { <option [value]="t">{{ t }}</option> }
      </select>

      <ui-select label="" placeholder="All branches" [searchable]="true" [options]="branchOptions()"
        [ngModel]="filters().branchId || null" (ngModelChange)="patch('branchId', $event ?? '')" />

      <input class="filter-date" type="date" [ngModel]="filters().dateFrom" (ngModelChange)="patch('dateFrom', $event); apply.emit()" />
      <input class="filter-date" type="date" [ngModel]="filters().dateTo" (ngModelChange)="patch('dateTo', $event); apply.emit()" />

      <div class="filter-actions">
        <button type="button" class="btn-ghost" (click)="apply.emit()"><mat-icon>filter_list</mat-icon></button>
        <button type="button" class="btn-ghost" (click)="clear.emit()"><mat-icon>clear</mat-icon></button>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
      margin-bottom: 1rem;
    }

    .search-wrap {
      position: relative;
      flex: 1 1 280px;
      min-width: 0;
    }

    .search-icon {
      position: absolute;
      left: 0.625rem;
      top: 50%;
      transform: translateY(-50%);
      font-size: 18px;
      color: #94a3b8;
    }

    .search-input {
      width: 100%;
      min-height: 44px;
      padding: 0.5rem 0.75rem 0.5rem 2.25rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.875rem;
      box-sizing: border-box;
    }

    .filter-select,
    .filter-date {
      min-height: 44px;
      padding: 0.5rem 0.625rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.875rem;
      background: #fff;
      box-sizing: border-box;
    }

    .filter-actions {
      display: flex;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .btn-ghost {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 44px;
      min-height: 44px;
      padding: 0.45rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      background: #fff;
      cursor: pointer;
    }

    ui-select {
      flex: 1 1 160px;
      min-width: 0;
    }

    @media (min-width: 768px) and (max-width: 1023px) {
      .filters {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        align-items: end;
      }
      .search-wrap { grid-column: 1 / -1; }
      .filter-actions { grid-column: 1 / -1; justify-content: flex-end; }
      ui-select, .filter-select, .filter-date { width: 100%; }
    }

    @media (max-width: 767px) {
      .filters {
        flex-direction: column;
        align-items: stretch;
      }
      .search-wrap { flex: 1 1 auto; width: 100%; }
      ui-select, .filter-select, .filter-date { width: 100%; flex: 1 1 auto; }
      .filter-actions { width: 100%; }
      .btn-ghost { flex: 1; }
    }
  `]
})
export class AssignmentFiltersComponent {
  readonly filters = input.required<FleetAssignmentFilters>();
  readonly vehicleOptions = input<UiSelectOption[]>([]);
  readonly driverOptions = input<UiSelectOption[]>([]);
  readonly branchOptions = input<UiSelectOption[]>([]);

  readonly filtersChange = output<FleetAssignmentFilters>();
  readonly apply = output<void>();
  readonly clear = output<void>();

  readonly statuses = ASSIGNMENT_STATUSES;
  readonly types = ASSIGNMENT_TYPES;

  patch(key: keyof FleetAssignmentFilters, value: string): void {
    this.filtersChange.emit({ ...this.filters(), [key]: value });
  }
}
