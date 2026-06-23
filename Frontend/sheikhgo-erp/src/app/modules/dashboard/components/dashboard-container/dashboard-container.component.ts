import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

import { DashboardService } from '../../services/dashboard.service';
import { DashboardRegistryEntry, DashboardType } from '../../models/dashboard-type.model';
import { DashboardSelectorComponent } from '../dashboard-selector/dashboard-selector.component';
import { DefaultDashboardComponent } from '../../dashboards/default-dashboard/default-dashboard.component';
import { FleetDashboardComponent } from '../../dashboards/fleet-dashboard/fleet-dashboard.component';

/**
 * Route component for `/dashboard`. Renders the selected dashboard via NgComponentOutlet
 * inside the main STCC shell — one sidebar, selector always in the header.
 */
@Component({
  selector: 'app-dashboard-container',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DashboardSelectorComponent],
  template: `
    <div class="dashboard-container">
      <header class="dashboard-container__header">
        <app-dashboard-selector></app-dashboard-selector>
      </header>
      <ng-container *ngComponentOutlet="activeComponent()"></ng-container>
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100%; }

    .dashboard-container {
      display: flex;
      flex-direction: column;
      min-height: 100%;
    }

    .dashboard-container__header {
      display: flex;
      justify-content: flex-end;
      padding: 16px 24px 0;
    }
  `]
})
export class DashboardContainerComponent {
  private readonly dashboardService = inject(DashboardService);

  private readonly registry: DashboardRegistryEntry[] = [
    { type: DashboardType.DEFAULT, label: 'Default Dashboard', icon: 'dashboard', component: DefaultDashboardComponent },
    { type: DashboardType.FLEET, label: 'Fleet Dashboard', icon: 'local_shipping', component: FleetDashboardComponent }
  ];

  readonly activeComponent = computed(() => {
    const type = this.dashboardService.selectedDashboard();
    return (this.registry.find((entry) => entry.type === type) ?? this.registry[0]).component;
  });
}
