import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { UiSelectComponent, UiSelectOption } from '../../../../shared/components/ui';
import { DashboardService } from '../../services/dashboard.service';
import { DASHBOARD_OPTIONS, DashboardType } from '../../models/dashboard-type.model';

/** Dropdown that switches the active dashboard in the Admin Portal `/dashboard` route. */
@Component({
  selector: 'app-dashboard-selector',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, UiSelectComponent],
  template: `
    <div class="dash-selector flex items-center gap-2 sm:gap-3">
      <span class="hidden whitespace-nowrap text-sm font-semibold text-fleet-text-muted sm:inline">Dashboard</span>
      <div class="dash-selector__control w-full min-w-0 sm:w-56">
        <ui-select
          [options]="selectOptions"
          [ngModel]="selected()"
          (ngModelChange)="onSelect($event)"
          [searchable]="false"
          placeholder="Select dashboard">
        </ui-select>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .dash-selector { min-width: 0; }

    @media (max-width: 767px) {
      :host { width: 100%; }
      .dash-selector { width: 100%; }
      .dash-selector__control { min-height: 44px; }
    }
  `]
})
export class DashboardSelectorComponent {
  private readonly dashboardService = inject(DashboardService);

  readonly selected = computed<string>(() => this.dashboardService.selectedDashboard());

  readonly selectOptions: UiSelectOption[] = DASHBOARD_OPTIONS.map((option) => ({
    value: option.type,
    label: option.label
  }));

  onSelect(value: string | string[] | null): void {
    if (typeof value === 'string') {
      this.dashboardService.setDashboard(value as DashboardType);
    }
  }
}
