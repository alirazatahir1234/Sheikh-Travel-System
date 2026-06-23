import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

export interface ServiceHistoryStats {
  totalServices: number;
  totalCost: number;
  avgIntervalDays: number | null;
  accidentRecords: number;
}

@Component({
  selector: 'service-history-stats',
  standalone: true,
  imports: [CurrencyPipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats-row">
      <div class="stat-card">
        <div class="stat-icon stat-icon--brand"><mat-icon>build</mat-icon></div>
        <div class="stat-card__info">
          <p class="stat-card__num">{{ loading() ? '—' : stats().totalServices }}</p>
          <p class="stat-card__label">Total Services</p>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon stat-icon--green"><mat-icon>payments</mat-icon></div>
        <div class="stat-card__info">
          <p class="stat-card__num">
            @if (loading()) { — } @else { {{ stats().totalCost | currency }} }
          </p>
          <p class="stat-card__label">Total Cost</p>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon stat-icon--amber"><mat-icon>schedule</mat-icon></div>
        <div class="stat-card__info">
          <p class="stat-card__num">
            @if (loading()) { — }
            @else if (stats().avgIntervalDays != null) { {{ stats().avgIntervalDays }} days }
            @else { — }
          </p>
          <p class="stat-card__label">Avg Service Interval</p>
        </div>
      </div>
      <div class="stat-card" [class.stat-card--alert]="!loading() && stats().accidentRecords > 0">
        <div class="stat-icon stat-icon--red"><mat-icon>car_crash</mat-icon></div>
        <div class="stat-card__info">
          <p class="stat-card__num">{{ loading() ? '—' : stats().accidentRecords }}</p>
          <p class="stat-card__label">Accident Records</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .stats-row {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;
      margin-bottom: 1.5rem;
    }
    .stat-card {
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
      border: 1px solid #e2e8f0;
      padding: 1.125rem 1.25rem;
      display: flex;
      align-items: center;
      gap: 0.875rem;
    }
    .stat-card--alert { border-color: #fecaca; background: #fef2f2; }
    .stat-card--alert .stat-card__num { color: #dc2626; }
    .stat-icon {
      width: 44px;
      height: 44px;
      border-radius: 10px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
    }
    .stat-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .stat-icon--brand { background: #0b6b50; color: #fff; }
    .stat-icon--green { background: #e8f5f0; color: #0b6b50; }
    .stat-icon--amber { background: #fef3c7; color: #f59e0b; }
    .stat-icon--red { background: #fee2e2; color: #dc2626; }
    .stat-card__num {
      margin: 0;
      font-size: 1.375rem;
      font-weight: 800;
      color: #0f172a;
      line-height: 1.1;
    }
    .stat-card__label {
      margin: 0.15rem 0 0;
      font-size: 0.75rem;
      color: #64748b;
    }
    @media (max-width: 900px) {
      .stats-row { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 480px) {
      .stats-row { grid-template-columns: 1fr; }
    }
  `]
})
export class ServiceHistoryStatsComponent {
  readonly stats = input.required<ServiceHistoryStats>();
  readonly loading = input(false);
}
