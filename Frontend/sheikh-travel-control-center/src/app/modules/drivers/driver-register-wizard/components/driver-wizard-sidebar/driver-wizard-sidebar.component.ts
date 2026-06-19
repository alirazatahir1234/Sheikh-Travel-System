import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiStatusBadgeComponent } from '../../../../../shared/components/ui/status-badge/ui-status-badge.component';

export interface DriverWizardStepStatus {
  id: string;
  label: string;
  complete: boolean;
  active: boolean;
}

@Component({
  selector: 'app-driver-wizard-sidebar',
  standalone: true,
  imports: [MatIconModule, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="sidebar">
      <div class="sidebar-card">
        <p class="sidebar-eyebrow">Registration Progress</p>
        <p class="sidebar-percent">{{ progressPercent() }}%</p>
        <div class="progress-bar">
          <div class="progress-fill" [style.width.%]="progressPercent()"></div>
        </div>
        <ul class="step-checklist">
          @for (step of stepCompletion(); track step.id) {
            <li class="step-item">
              @if (step.complete) {
                <mat-icon class="step-icon done">check_circle</mat-icon>
              } @else if (step.active) {
                <mat-icon class="step-icon active">radio_button_checked</mat-icon>
              } @else {
                <mat-icon class="step-icon pending">hourglass_empty</mat-icon>
              }
              <span [class.text-fleet-primary]="step.active" [class.font-medium]="step.active">{{ step.label }}</span>
            </li>
          }
        </ul>
      </div>

      <div class="sidebar-card">
        <p class="sidebar-eyebrow">Verification Status</p>
        <div class="status-list">
          <div class="status-row">
            <span>Driver Status</span>
            <ui-status-badge [status]="driverStatus() === 'Draft' ? 'pending' : 'valid'" [label]="driverStatus()" />
          </div>
          <div class="status-row">
            <span>License Status</span>
            <ui-status-badge
              [status]="statusVariant(licenseStatus())"
              [label]="licenseStatus()" />
          </div>
          <div class="status-row">
            <span>Org. Assignment</span>
            <ui-status-badge
              [status]="statusVariant(orgStatus())"
              [label]="orgStatus()" />
          </div>
        </div>
        <div class="info-note">
          <mat-icon>info</mat-icon>
          <p>Ensure the full name matches exactly with the Emirates ID to avoid verification delays.</p>
        </div>
      </div>

      <div class="branding-card">
        <div class="branding-overlay">
          <p class="branding-title">Premium Fleet Registry</p>
          <p class="branding-sub">Managing excellence since 2008</p>
        </div>
      </div>
    </aside>
  `,
  styles: [`
    .sidebar {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      position: sticky;
      top: 1rem;
    }

    .sidebar-card {
      border-radius: 0.75rem;
      border: 1px solid var(--fleet-border, #e2e8f0);
      background: #fff;
      padding: 1.25rem;
    }

    .sidebar-eyebrow {
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--fleet-text-muted, #64748b);
      margin: 0 0 0.75rem;
    }

    .sidebar-percent {
      font-size: 2rem;
      font-weight: 700;
      color: var(--fleet-text, #0f172a);
      margin: 0 0 0.75rem;
      line-height: 1;
    }

    .progress-bar {
      height: 6px;
      border-radius: 999px;
      background: #e2e8f0;
      overflow: hidden;
      margin-bottom: 1rem;
    }

    .progress-fill {
      height: 100%;
      background: var(--fleet-primary, #006b54);
      border-radius: 999px;
      transition: width 0.3s ease;
    }

    .step-checklist {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.625rem;
    }

    .step-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      color: var(--fleet-text-muted, #64748b);
    }

    .step-icon {
      font-size: 18px !important;
      width: 18px !important;
      height: 18px !important;

      &.done { color: #16a34a; }
      &.active { color: var(--fleet-primary, #006b54); }
      &.pending { color: #94a3b8; }
    }

    .status-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .status-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      font-size: 0.875rem;
      color: var(--fleet-text, #0f172a);
    }

    .info-note {
      display: flex;
      gap: 0.5rem;
      padding: 0.75rem;
      border-radius: 0.5rem;
      background: #ecfdf5;
      border: 1px solid #a7f3d0;

      mat-icon {
        font-size: 18px !important;
        width: 18px !important;
        height: 18px !important;
        color: #059669;
        flex-shrink: 0;
      }

      p {
        margin: 0;
        font-size: 0.75rem;
        color: #065f46;
        line-height: 1.4;
      }
    }

    .branding-card {
      border-radius: 0.75rem;
      overflow: hidden;
      min-height: 120px;
      background: linear-gradient(135deg, #0f172a 0%, #1e3a5f 40%, #006b54 100%);
      position: relative;
    }

    .branding-overlay {
      padding: 1.25rem;
      height: 100%;
      display: flex;
      flex-direction: column;
      justify-content: flex-end;
    }

    .branding-title {
      font-size: 0.9375rem;
      font-weight: 700;
      color: #fff;
      margin: 0 0 0.25rem;
    }

    .branding-sub {
      font-size: 0.75rem;
      color: rgba(255, 255, 255, 0.75);
      margin: 0;
    }
  `]
})
export class DriverWizardSidebarComponent {
  readonly progressPercent = input(33);
  readonly stepCompletion = input<DriverWizardStepStatus[]>([]);
  readonly licenseStatus = input('NOT STARTED');
  readonly orgStatus = input('NOT STARTED');
  readonly driverStatus = input('Draft');

  statusVariant(status: string): 'valid' | 'pending' | 'inactive' {
    const s = status.toUpperCase();
    if (s === 'ASSIGNED' || s === 'VALID' || s === 'IN PROGRESS') return 'valid';
    if (s === 'NOT STARTED') return 'inactive';
    return 'pending';
  }
}
