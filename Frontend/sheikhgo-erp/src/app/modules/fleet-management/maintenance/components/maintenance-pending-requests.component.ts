import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'maintenance-pending-requests',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="pending-card" routerLink="/fleet/maintenance/requests" [queryParams]="{ status: 'Open' }">
      <div class="pending-icon"><mat-icon>pending_actions</mat-icon></div>
      <div>
        <p class="pending-label">Pending Requests</p>
        <p class="pending-value">{{ count() }}</p>
        <p class="pending-hint">Open & awaiting approval</p>
      </div>
      <mat-icon class="pending-arrow">chevron_right</mat-icon>
    </a>
  `,
  styles: [`
    .pending-card {
      display: flex; align-items: center; gap: 1rem; padding: 1rem 1.25rem;
      background: #fff; border: 1px solid #fde68a; border-radius: 14px;
      text-decoration: none; color: inherit;
      box-shadow: 0 1px 3px rgba(245, 158, 11, 0.1);
      min-width: 0;
    }
    .pending-icon {
      width: 44px; height: 44px; border-radius: 12px; background: #fef3c7; color: #f59e0b;
      display: grid; place-items: center;
    }
    .pending-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .pending-label { margin: 0; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    .pending-value { margin: 0.125rem 0 0; font-size: 1.5rem; font-weight: 800; color: #f59e0b; }
    .pending-hint { margin: 0.125rem 0 0; font-size: 0.75rem; color: #94a3b8; }
    .pending-arrow { margin-left: auto; color: #cbd5e1; flex-shrink: 0; }
    @media (max-width: 480px) {
      .pending-card { padding: 0.875rem 1rem; gap: 0.75rem; }
      .pending-value { font-size: 1.25rem; }
    }
  `]
})
export class MaintenancePendingRequestsComponent {
  readonly count = input(0);
}
