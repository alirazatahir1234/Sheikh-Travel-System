import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { MaintenanceKpis } from '../../../../core/models/maintenance.model';

export interface MaintKpiCard {
  key: string;
  label: string;
  value: string | number;
  icon: string;
  tone: string;
  alert?: boolean;
}

@Component({
  selector: 'maintenance-kpi-grid',
  standalone: true,
  imports: [MatIconModule, CurrencyPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kpi-grid">
      @for (card of cards(); track card.key) {
        <div class="kpi-card" [class.kpi-card--alert]="card.alert" [class.kpi-card--primary]="card.tone === 'brand'">
          <div class="kpi-icon kpi-icon--{{ card.tone }}"><mat-icon>{{ card.icon }}</mat-icon></div>
          <div class="kpi-body">
            <p class="kpi-label">{{ card.label }}</p>
            <p class="kpi-value">@if (card.key === 'cost') { {{ card.value | currency }} } @else { {{ card.value }} }</p>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 0.875rem; }
    .kpi-card {
      display: flex; gap: 0.875rem; align-items: center; padding: 1.125rem 1.25rem;
      border: 1px solid #e2e8f0; border-radius: 14px; background: #fff;
      box-shadow: 0 1px 3px rgba(11, 107, 80, 0.06);
      transition: box-shadow .15s ease;
    }
    .kpi-card:hover { box-shadow: 0 4px 12px rgba(11, 107, 80, 0.1); }
    .kpi-card--alert { border-color: #fecaca; background: #fef2f2; }
    .kpi-card--alert .kpi-value { color: #dc2626; }
    .kpi-card--primary { border-color: #b8e6d4; background: #e8f5f0; }
    .kpi-icon { width: 44px; height: 44px; border-radius: 12px; display: grid; place-items: center; flex-shrink: 0; }
    .kpi-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .kpi-icon--brand { background: #0b6b50; color: #fff; }
    .kpi-icon--green { background: #e8f5f0; color: #0b6b50; }
    .kpi-icon--amber { background: #fef3c7; color: #f59e0b; }
    .kpi-icon--red { background: #fee2e2; color: #dc2626; }
    .kpi-icon--blue { background: #dbeafe; color: #1d4ed8; }
    .kpi-icon--teal { background: #ccfbf1; color: #0f766e; }
    .kpi-icon--slate { background: #f1f5f9; color: #475569; }
    .kpi-label { margin: 0; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; color: #64748b; }
    .kpi-value { margin: 0.25rem 0 0; font-size: 1.5rem; font-weight: 800; color: #0b6b50; line-height: 1.1; }
    .kpi-card--alert .kpi-value { color: #dc2626; }
  `]
})
export class MaintenanceKpiGridComponent {
  readonly kpis = input<MaintenanceKpis | null>(null);

  cards = computed<MaintKpiCard[]>(() => {
    const k = this.kpis();
    if (!k) return [];
    return [
      { key: 'total', label: 'Total Vehicles', value: k.totalVehicles, icon: 'local_shipping', tone: 'brand' },
      { key: 'due', label: 'Due for Service', value: k.dueForService, icon: 'event', tone: 'amber' },
      { key: 'under', label: 'Under Maintenance', value: k.underMaintenance, icon: 'build', tone: 'blue' },
      { key: 'overdue', label: 'Overdue Services', value: k.overdueServices, icon: 'warning', tone: 'red', alert: k.overdueServices > 0 },
      { key: 'cost', label: 'Monthly Cost', value: k.monthlyMaintenanceCost, icon: 'payments', tone: 'teal' },
      { key: 'active', label: 'Active Work Orders', value: k.activeWorkOrders, icon: 'assignment', tone: 'slate' }
    ];
  });
}
