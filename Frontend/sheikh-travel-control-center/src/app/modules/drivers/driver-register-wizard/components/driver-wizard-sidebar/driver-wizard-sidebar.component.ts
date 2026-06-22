import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { UiStatusBadgeComponent } from '../../../../../shared/components/ui/status-badge/ui-status-badge.component';

export interface DriverWizardStepStatus {
  id: string;
  label: string;
  complete: boolean;
  active: boolean;
}

export interface ProfileCompletionSection {
  label: string;
  percent: number;
}

export interface VerificationStatusRow {
  label: string;
  status: string;
}

export interface OnboardingChecklistItem {
  label: string;
  done: boolean;
}

@Component({
  selector: 'app-driver-wizard-sidebar',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="sidebar">
      <div class="sidebar-card">
        <div class="sidebar-card-head">
          <p class="sidebar-eyebrow">Profile Completion</p>
          <button type="button" class="formula-hint" [matTooltip]="completionHint()" matTooltipPosition="left" aria-label="Completion formula">
            <mat-icon>info_outline</mat-icon>
          </button>
        </div>
        <p class="sidebar-percent">{{ progressPercent() }}%</p>
        <div class="progress-bar">
          <div class="progress-fill" [style.width.%]="progressPercent()"></div>
        </div>
        <ul class="completion-breakdown">
          @for (section of profileSections(); track section.label) {
            <li class="completion-row">
              <span class="completion-label">{{ section.label }}</span>
              <div class="completion-track">
                <div class="completion-fill" [style.width.%]="section.percent"></div>
              </div>
              <span class="completion-pct">{{ section.percent }}%</span>
            </li>
          }
        </ul>

        <p class="checklist-eyebrow">Onboarding Checklist</p>
        <ul class="onboarding-checklist">
          @for (item of onboardingChecklist(); track item.label) {
            <li class="checklist-item">
              @if (item.done) {
                <mat-icon class="checklist-icon done">check_circle</mat-icon>
              } @else {
                <mat-icon class="checklist-icon warn">warning</mat-icon>
              }
              <span [class.done-text]="item.done">{{ item.label }}</span>
            </li>
          }
        </ul>
      </div>

      <div class="sidebar-card">
        <p class="sidebar-eyebrow">Verification Status</p>
        <div class="verification-progress">
          <div class="verification-progress-head">
            <span>Verification Progress</span>
            <span class="verification-pct">{{ verificationProgress() }}%</span>
          </div>
          <div class="progress-bar">
            <div class="progress-fill verification-fill" [style.width.%]="verificationProgress()"></div>
          </div>
        </div>
        <div class="status-list">
          @for (row of verificationRows(); track row.label) {
            <div class="status-row">
              <span>{{ row.label }}</span>
              <ui-status-badge [status]="statusVariant(row.status)" [label]="row.status" />
            </div>
          }
        </div>
        <div class="info-note">
          <mat-icon>info</mat-icon>
          <p>Ensure the full name matches exactly with the Emirates ID to avoid verification delays.</p>
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

    .sidebar-card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }

    .sidebar-card-head .sidebar-eyebrow { margin-bottom: 0; }

    .formula-hint {
      display: grid;
      place-items: center;
      width: 1.5rem;
      height: 1.5rem;
      border: none;
      background: transparent;
      color: var(--fleet-text-muted, #64748b);
      cursor: help;
      padding: 0;

      mat-icon {
        font-size: 16px !important;
        width: 16px !important;
        height: 16px !important;
      }
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

    .verification-fill { background: #2563eb; }

    .completion-breakdown {
      list-style: none;
      margin: 0 0 1rem;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .completion-row {
      display: grid;
      grid-template-columns: 1fr 1.5fr auto;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.75rem;
    }

    .completion-label {
      color: var(--fleet-text-muted, #64748b);
      font-weight: 600;
    }

    .completion-track {
      height: 4px;
      border-radius: 999px;
      background: #f1f5f9;
      overflow: hidden;
    }

    .completion-fill {
      height: 100%;
      background: #14b8a6;
      border-radius: 999px;
    }

    .completion-pct {
      font-weight: 700;
      color: var(--fleet-text, #0f172a);
      min-width: 2rem;
      text-align: right;
    }

    .checklist-eyebrow {
      margin: 0 0 0.5rem;
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--fleet-text-muted, #64748b);
      border-top: 1px solid var(--fleet-border, #e2e8f0);
      padding-top: 0.875rem;
    }

    .onboarding-checklist {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
    }

    .checklist-item {
      display: flex;
      align-items: center;
      gap: 0.45rem;
      font-size: 0.8125rem;
      color: var(--fleet-text-muted, #64748b);
    }

    .checklist-icon {
      font-size: 16px !important;
      width: 16px !important;
      height: 16px !important;

      &.done { color: #16a34a; }
      &.warn { color: #d97706; }
    }

    .done-text { color: var(--fleet-text, #0f172a); font-weight: 600; }

    .verification-progress {
      margin-bottom: 0.875rem;
    }

    .verification-progress-head {
      display: flex;
      justify-content: space-between;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--fleet-text, #0f172a);
      margin-bottom: 0.35rem;
    }

    .verification-pct { color: #2563eb; }

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
  `]
})
export class DriverWizardSidebarComponent {
  readonly progressPercent = input(33);
  readonly profileSections = input<ProfileCompletionSection[]>([]);
  readonly completionHint = input('');
  readonly onboardingChecklist = input<OnboardingChecklistItem[]>([]);
  readonly verificationProgress = input(0);
  readonly verificationRows = input<VerificationStatusRow[]>([]);

  statusVariant(status: string): 'valid' | 'pending' | 'inactive' {
    const s = status.toUpperCase();
    if (['VERIFIED', 'ASSIGNED', 'AVAILABLE'].includes(s)) return 'valid';
    if (['DRAFT', 'NOT STARTED'].includes(s)) return 'inactive';
    return 'pending';
  }
}
