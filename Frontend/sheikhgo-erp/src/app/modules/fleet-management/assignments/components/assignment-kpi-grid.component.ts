import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { FleetAssignmentStats } from '../../../../core/models/fleet-assignment.model';

export interface KpiCard {
  key: string;
  label: string;
  value: number | string;
  icon: string;
  tone: string;
  filterStatus?: string;
}

@Component({
  selector: 'assignment-kpi-grid',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kpi-grid">
      @for (card of cards(); track card.key) {
        <button type="button" class="kpi-card" (click)="kpiClick.emit(card.filterStatus ?? '')">
          <div class="kpi-icon kpi-icon--{{ card.tone }}"><mat-icon>{{ card.icon }}</mat-icon></div>
          <div class="kpi-body">
            <p class="kpi-label">{{ card.label }}</p>
            <p class="kpi-value kpi-value--{{ card.tone }}">{{ card.value }}</p>
          </div>
        </button>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .kpi-grid {
      display: grid;
      gap: 0.75rem;
      margin-bottom: 1rem;
      grid-template-columns: repeat(4, minmax(0, 1fr));
    }

    .kpi-card {
      display: flex;
      gap: 0.625rem;
      align-items: center;
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      background: #fff;
      cursor: pointer;
      text-align: left;
      transition: box-shadow .15s;
      min-width: 0;
    }
    .kpi-card:hover { box-shadow: 0 2px 8px rgba(15,23,42,.08); }

    .kpi-icon {
      width: 36px;
      height: 36px;
      border-radius: 8px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .kpi-icon mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .kpi-icon--blue { background: #dbeafe; color: #1d4ed8; }
    .kpi-icon--green { background: #d1fae5; color: #047857; }
    .kpi-icon--amber { background: #fef3c7; color: #b45309; }
    .kpi-icon--red { background: #fee2e2; color: #b91c1c; }
    .kpi-icon--teal { background: #ccfbf1; color: #0f766e; }
    .kpi-icon--slate { background: #f1f5f9; color: #475569; }
    .kpi-icon--purple { background: #ede9fe; color: #6d28d9; }

    .kpi-body { min-width: 0; }
    .kpi-label {
      margin: 0;
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .04em;
      color: #64748b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .kpi-value {
      margin: 0.125rem 0 0;
      font-size: clamp(1.125rem, 2.5vw, 1.25rem);
      font-weight: 700;
      color: #0f172a;
    }
    .kpi-value--green { color: #047857; }
    .kpi-value--amber { color: #b45309; }
    .kpi-value--red { color: #b91c1c; }
    .kpi-value--teal { color: #0f766e; }

    @media (max-width: 1200px) {
      .kpi-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    }

    @media (max-width: 900px) {
      .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }

    @media (max-width: 480px) {
      .kpi-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class AssignmentKpiGridComponent {
  readonly stats = input<FleetAssignmentStats | null>(null);
  readonly loading = input(false);
  readonly kpiClick = output<string>();

  cards(): KpiCard[] {
    const s = this.stats();
    if (!s) return [];
    return [
      { key: 'active', label: 'Active', value: s.activeAssignments, icon: 'check_circle', tone: 'green', filterStatus: 'Active' },
      { key: 'upcoming', label: 'Upcoming', value: s.upcomingAssignments, icon: 'schedule', tone: 'amber', filterStatus: 'Scheduled' },
      { key: 'overdue', label: 'Overdue', value: s.overdueReturns, icon: 'warning', tone: 'red', filterStatus: 'Overdue' },
      { key: 'ongoing', label: 'Ongoing Trips', value: s.ongoingTrips, icon: 'route', tone: 'purple' },
      { key: 'availableVehicles', label: 'Available Vehicles', value: s.availableVehicles, icon: 'directions_car', tone: 'teal' },
      { key: 'availableDrivers', label: 'Available Drivers', value: s.availableDrivers, icon: 'badge', tone: 'teal' },
      { key: 'utilization', label: 'Utilization', value: `${s.assignmentUtilizationPct ?? 0}%`, icon: 'pie_chart', tone: 'blue' },
      { key: 'expiring', label: 'Expiring Licenses', value: s.expiringLicenses, icon: 'card_membership', tone: 'red' },
      { key: 'expiredDocs', label: 'Expired Docs', value: s.expiredDocuments, icon: 'description', tone: 'red' },
      { key: 'onLeave', label: 'Drivers on Leave', value: s.driversOnLeave, icon: 'event_busy', tone: 'slate' },
      { key: 'maintenance', label: 'Under Maintenance', value: s.vehiclesUnderMaintenance, icon: 'build', tone: 'amber' },
      { key: 'total', label: 'Total', value: s.totalAssignments, icon: 'assignment', tone: 'slate' }
    ];
  }
}
