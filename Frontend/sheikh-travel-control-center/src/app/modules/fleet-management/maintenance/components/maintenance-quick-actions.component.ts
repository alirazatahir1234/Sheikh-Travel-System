import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'maintenance-quick-actions',
  standalone: true,
  imports: [MatIconModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <h3>Quick Actions</h3>
      <button type="button" class="btn btn--primary" routerLink="/fleet/maintenance/requests" [queryParams]="{ create: true }">
        <mat-icon>add</mat-icon> Create Request
      </button>
      <button type="button" class="btn" routerLink="/fleet/maintenance/work-orders" [queryParams]="{ create: true }">
        <mat-icon>description</mat-icon> Create Work Order
      </button>
      <button type="button" class="btn" routerLink="/fleet/maintenance/schedules">
        <mat-icon>schedule</mat-icon> Schedule Service
      </button>
      <button type="button" class="btn" routerLink="/fleet/maintenance/workshops" [queryParams]="{ create: true }">
        <mat-icon>home_repair_service</mat-icon> Add Workshop
      </button>
    </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; display: flex; flex-direction: column; gap: 0.5rem; }
    h3 { margin: 0 0 0.5rem; font-size: 1rem; font-weight: 700; }
    .btn { display: flex; align-items: center; gap: 0.5rem; padding: 0.625rem 1rem; border-radius: 8px; border: 1px solid #e2e8f0; background: #fff; cursor: pointer; font-weight: 600; font-size: 0.875rem; text-decoration: none; color: #0f172a; }
    .btn--primary { background: #064e3b; color: #fff; border-color: #064e3b; }
    .btn mat-icon { font-size: 18px; width: 18px; height: 18px; }
  `]
})
export class MaintenanceQuickActionsComponent {
  readonly action = output<string>();
}
