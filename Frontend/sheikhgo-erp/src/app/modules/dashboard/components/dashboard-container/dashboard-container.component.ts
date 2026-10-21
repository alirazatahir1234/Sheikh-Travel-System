import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

import { DashboardService } from '../../services/dashboard.service';
import { DashboardRegistryEntry, DashboardType } from '../../models/dashboard-type.model';
import { DashboardSelectorComponent } from '../dashboard-selector/dashboard-selector.component';
import { DefaultDashboardComponent } from '../../dashboards/default-dashboard/default-dashboard.component';

/**
 * Route component for `/dashboard` (ops overview).
 * Fleet asset-health hub is only at `/fleet/dashboard` — not duplicated via selector.
 */
@Component({
  selector: 'app-dashboard-container',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DashboardSelectorComponent],
  template: `
    <div class="dashboard-container">
      @if (showSelector()) {
        <header class="dashboard-container__header">
          <app-dashboard-selector></app-dashboard-selector>
        </header>
      }
      <ng-container *ngComponentOutlet="activeComponent()"></ng-container>
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100%; min-width: 0; max-width: 100%; }

    .dashboard-container {
      display: flex;
      flex-direction: column;
      min-height: 100%;
      min-width: 0;
      max-width: 100%;
    }

    .dashboard-container__header {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      padding: 16px 24px 0;
      min-width: 0;
    }

    @media (max-width: 767px) {
      .dashboard-container__header {
        justify-content: stretch;
        padding: 8px 0 0;
      }

      .dashboard-container__header > * {
        width: 100%;
      }
    }

    @media (max-width: 575px) {
      .dashboard-container__header {
        padding-top: 4px;
      }
    }
  `]
})
export class DashboardContainerComponent {
  private readonly dashboardService = inject(DashboardService);

  private readonly registry: DashboardRegistryEntry[] = [
    { type: DashboardType.DEFAULT, label: 'Operations Dashboard', icon: 'dashboard', component: DefaultDashboardComponent }
  ];

  /** Hide the selector when only one dashboard is registered (avoids dual-nav confusion). */
  readonly showSelector = computed(() => this.registry.length > 1);

  readonly activeComponent = computed(() => {
    const type = this.dashboardService.selectedDashboard();
    return (this.registry.find((entry) => entry.type === type) ?? this.registry[0]).component;
  });
}
