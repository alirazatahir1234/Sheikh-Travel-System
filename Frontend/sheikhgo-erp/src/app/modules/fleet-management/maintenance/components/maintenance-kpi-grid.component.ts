import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceKpis } from '../../../../core/models/maintenance.model';
import { DEFAULT_CURRENCY, formatCurrency } from '../../../../core/models/platform.model';

export interface MaintKpiCard {
  key: string;
  label: string;
  value: string | number;
  displayValue: string;
  icon: string;
  tone: string;
  alert?: boolean;
  currency?: boolean;
  title?: string;
}

@Component({
  selector: 'maintenance-kpi-grid',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kpi-grid">
      @for (card of cards(); track card.key) {
        <div
          class="kpi-card"
          [class.kpi-card--alert]="card.alert"
          [class.kpi-card--primary]="card.tone === 'brand'"
          [class.kpi-card--cost]="card.currency">
          <div class="kpi-icon kpi-icon--{{ card.tone }}"><mat-icon>{{ card.icon }}</mat-icon></div>
          <div class="kpi-body">
            <p class="kpi-label">{{ card.label }}</p>
            <p
              class="kpi-value"
              [class.kpi-value--currency]="card.currency"
              [attr.title]="card.title || null">
              {{ card.displayValue }}
            </p>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(6, minmax(0, 1fr));
      gap: 0.875rem;
    }
    .kpi-card {
      display: flex;
      gap: 0.75rem;
      align-items: center;
      padding: 1.125rem 1rem;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      background: #fff;
      box-shadow: 0 1px 3px rgba(11, 107, 80, 0.06);
      transition: box-shadow .15s ease;
      min-width: 0;
      overflow: hidden;
    }
    .kpi-card--cost { min-width: 0; }
    .kpi-card:hover { box-shadow: 0 4px 12px rgba(11, 107, 80, 0.1); }
    .kpi-card--alert { border-color: #fecaca; background: #fef2f2; }
    .kpi-card--alert .kpi-value { color: #dc2626; }
    .kpi-card--primary { border-color: #b8e6d4; background: #e8f5f0; }
    .kpi-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .kpi-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .kpi-icon--brand { background: #0b6b50; color: #fff; }
    .kpi-icon--green { background: #e8f5f0; color: #0b6b50; }
    .kpi-icon--amber { background: #fef3c7; color: #f59e0b; }
    .kpi-icon--red { background: #fee2e2; color: #dc2626; }
    .kpi-icon--blue { background: #dbeafe; color: #1d4ed8; }
    .kpi-icon--teal { background: #ccfbf1; color: #0f766e; }
    .kpi-icon--purple { background: #ede9fe; color: #7c3aed; }
    .kpi-icon--slate { background: #f1f5f9; color: #475569; }
    .kpi-body {
      min-width: 0;
      flex: 1 1 auto;
      overflow: hidden;
    }
    .kpi-label {
      margin: 0;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: .05em;
      color: #64748b;
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .kpi-value {
      margin: 0.25rem 0 0;
      font-size: 1.5rem;
      font-weight: 800;
      color: #0b6b50;
      line-height: 1.15;
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .kpi-value--currency {
      font-size: clamp(0.95rem, 1.35vw, 1.35rem);
      font-variant-numeric: tabular-nums;
      letter-spacing: -0.02em;
    }
    .kpi-card--alert .kpi-value { color: #dc2626; }
    @media (max-width: 1280px) {
      .kpi-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
      .kpi-value--currency { font-size: clamp(1rem, 2vw, 1.35rem); }
    }
    @media (max-width: 900px) {
      .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 640px) {
      .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.625rem; }
      .kpi-card { padding: 0.875rem; gap: 0.625rem; }
      .kpi-icon { width: 36px; height: 36px; }
      .kpi-icon mat-icon { font-size: 18px; width: 18px; height: 18px; }
      .kpi-value { font-size: 1.25rem; }
      .kpi-value--currency { font-size: 1.05rem; }
    }
    @media (max-width: 400px) {
      .kpi-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class MaintenanceKpiGridComponent {
  readonly kpis = input<MaintenanceKpis | null>(null);

  cards = computed<MaintKpiCard[]>(() => {
    const k = this.kpis();
    if (!k) return [];
    const cost = Number(k.monthlyMaintenanceCost ?? 0);
    const costFull = new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: DEFAULT_CURRENCY,
      maximumFractionDigits: 0
    }).format(Number.isFinite(cost) ? cost : 0);

    return [
      { key: 'total', label: 'Total Vehicles', value: k.totalVehicles, displayValue: String(k.totalVehicles), icon: 'local_shipping', tone: 'brand' },
      { key: 'due', label: 'Due for Service', value: k.dueForService, displayValue: String(k.dueForService), icon: 'event', tone: 'amber' },
      { key: 'under', label: 'Under Maintenance', value: k.underMaintenance, displayValue: String(k.underMaintenance), icon: 'build', tone: 'blue' },
      { key: 'overdue', label: 'Overdue Services', value: k.overdueServices, displayValue: String(k.overdueServices), icon: 'warning', tone: 'red', alert: k.overdueServices > 0 },
      { key: 'active', label: 'Active Work Orders', value: k.activeWorkOrders, displayValue: String(k.activeWorkOrders), icon: 'assignment', tone: 'purple' },
      {
        key: 'cost',
        label: 'Monthly Cost',
        value: cost,
        displayValue: formatCurrency(cost, DEFAULT_CURRENCY),
        icon: 'payments',
        tone: 'teal',
        currency: true,
        title: costFull
      }
    ];
  });
}
