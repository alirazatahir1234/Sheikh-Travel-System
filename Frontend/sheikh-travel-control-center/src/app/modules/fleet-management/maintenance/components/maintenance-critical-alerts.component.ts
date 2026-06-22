import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceAlert } from '../../../../core/models/maintenance.model';

@Component({
  selector: 'maintenance-critical-alerts',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <div class="card-head">
        <h3>Critical Alerts</h3>
        @if (newCount() > 0) {
          <span class="badge">{{ newCount() }} NEW</span>
        }
      </div>
      <div class="alerts">
        @for (a of alerts(); track a.id) {
          <button type="button" class="alert alert--{{ severityClass(a.severity) }}" (click)="onAlertClick(a)">
            <div class="alert-top">
              <mat-icon class="type-icon">{{ typeIcon(a.alertType) }}</mat-icon>
              <span class="severity-chip severity--{{ severityClass(a.severity) }}">{{ a.severity }}</span>
            </div>
            <p class="alert-title">{{ a.title }}</p>
            <p class="alert-msg">{{ a.message }}</p>
            @if (a.vehicleRegistration) {
              <small>{{ a.vehicleName }} · {{ a.vehicleRegistration }}</small>
            }
          </button>
        } @empty {
          <p class="empty">No critical alerts.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 1.25rem; min-width: 0; }
    .card-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; }
    h3 { margin: 0; font-size: 1rem; font-weight: 700; color: #0b6b50; }
    .badge { background: #dc2626; color: #fff; font-size: 0.625rem; font-weight: 700; padding: 0.125rem 0.5rem; border-radius: 999px; }
    .alerts { display: flex; flex-direction: column; gap: 0.5rem; }
    .alert {
      padding: 0.75rem; border-radius: 10px; border-left: 3px solid;
      text-align: left; width: 100%; cursor: pointer; border-top: none; border-right: none; border-bottom: none;
    }
    .alert--critical { background: #fef2f2; border-color: #dc2626; }
    .alert--warn { background: #fffbeb; border-color: #f59e0b; }
    .alert--info { background: #e8f5f0; border-color: #0b6b50; }
    .alert--low { background: #f8fafc; border-color: #94a3b8; }
    .alert-top { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem; }
    .type-icon { font-size: 18px; width: 18px; height: 18px; color: #64748b; }
    .severity-chip { font-size: 0.625rem; font-weight: 700; text-transform: uppercase; padding: 0.125rem 0.375rem; border-radius: 4px; }
    .severity--critical { background: #fee2e2; color: #dc2626; }
    .severity--warn { background: #fef3c7; color: #f59e0b; }
    .severity--info { background: #e8f5f0; color: #0b6b50; }
    .severity--low { background: #f1f5f9; color: #64748b; }
    .alert-title { margin: 0; font-weight: 700; font-size: 0.8125rem; }
    .alert-msg { margin: 0.25rem 0 0; font-size: 0.75rem; color: #475569; }
    small { font-size: 0.6875rem; color: #94a3b8; }
    .empty { color: #94a3b8; font-size: 0.875rem; margin: 0; }
  `]
})
export class MaintenanceCriticalAlertsComponent {
  private readonly router = inject(Router);

  readonly alerts = input<MaintenanceAlert[]>([]);
  readonly alertClick = output<MaintenanceAlert>();

  newCount(): number {
    return this.alerts().filter(a => a.severity === 'Critical' || a.severity === 'High').length;
  }

  severityClass(s: string): string {
    if (s === 'Critical' || s === 'Error') return 'critical';
    if (s === 'High' || s === 'Warning') return 'warn';
    if (s === 'Low') return 'low';
    return 'info';
  }

  typeIcon(type: string): string {
    const t = type.toLowerCase();
    if (t.includes('breakdown')) return 'car_crash';
    if (t.includes('inspection')) return 'fact_check';
    if (t.includes('insurance')) return 'shield';
    if (t.includes('overdue') || t.includes('service')) return 'schedule';
    return 'warning';
  }

  onAlertClick(a: MaintenanceAlert): void {
    this.alertClick.emit(a);
    if (a.vehicleId) this.router.navigate(['/vehicles', a.vehicleId]);
  }
}
