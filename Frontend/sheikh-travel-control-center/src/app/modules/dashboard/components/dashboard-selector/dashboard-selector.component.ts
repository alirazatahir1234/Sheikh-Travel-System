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
    <div class="flex items-center gap-2 sm:gap-3">
      <span class="hidden whitespace-nowrap text-sm font-semibold text-fleet-text-muted sm:inline">Dashboard</span>
      <div class="w-48 sm:w-56">
        <ui-select
          [options]="selectOptions"
          [ngModel]="selected()"
          (ngModelChange)="onSelect($event)"
          [searchable]="false"
          placeholder="Select dashboard">
        </ui-select>
      </div>
    </div>
  `
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
