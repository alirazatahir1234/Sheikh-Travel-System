import { ChangeDetectionStrategy, Component, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import {
  ASSIGNMENT_PURPOSES,
  ASSIGNMENT_TYPES,
  AssignmentValidationIssue,
  CreateAssignmentRequest
} from '../../../../core/models/fleet-assignment.model';

export interface AssignmentWizardForm extends CreateAssignmentRequest {
  vehicleIdStr: string;
  driverIdStr: string;
}

@Component({
  selector: 'assignment-create-wizard',
  standalone: true,
  imports: [FormsModule, MatIconModule, UiSelectComponent, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wizard">
      <div class="wizard-steps">
        @for (s of steps; track s.id; let i = $index) {
          <div class="wizard-step" [class.wizard-step--active]="step() === i" [class.wizard-step--done]="step() > i">
            <span class="wizard-step-num">{{ i + 1 }}</span>
            <span class="wizard-step-label">{{ s.label }}</span>
          </div>
        }
      </div>

      @switch (step()) {
        @case (0) {
          <ui-select label="Select vehicle" placeholder="Search vehicle…" [searchable]="true"
            [options]="vehicleOptions()" [(ngModel)]="form().vehicleIdStr" [required]="true" />
        }
        @case (1) {
          <ui-select label="Select driver" placeholder="Search driver…" [searchable]="true"
            [options]="driverOptions()" [(ngModel)]="form().driverIdStr" [required]="true" />
        }
        @case (2) {
          <label class="field">
            <span>Assignment type</span>
            <select class="input" [ngModel]="form().assignmentType" (ngModelChange)="patch({ assignmentType: $event })">
              @for (t of types; track t) { <option [value]="t">{{ t }}</option> }
            </select>
          </label>
          <label class="field">
            <span>Purpose</span>
            <select class="input" [ngModel]="form().purpose" (ngModelChange)="patch({ purpose: $event })">
              @for (p of purposes; track p) { <option [value]="p">{{ p }}</option> }
            </select>
          </label>
        }
        @case (3) {
          <div class="date-row">
            <label class="field"><span>Start date</span><input class="input" type="date" [ngModel]="form().startDate" (ngModelChange)="patch({ startDate: $event })" /></label>
            <label class="field"><span>End date (optional)</span><input class="input" type="date" [ngModel]="form().endDate" (ngModelChange)="patch({ endDate: $event || null })" /></label>
          </div>
          <label class="field"><span>Odometer start</span><input class="input" type="number" [ngModel]="form().odometerStart" (ngModelChange)="patch({ odometerStart: $event ? +$event : null })" /></label>
          <label class="field"><span>Remarks</span><textarea class="input" rows="2" [ngModel]="form().notes" (ngModelChange)="patch({ notes: $event })"></textarea></label>
        }
        @case (4) {
          <div class="review">
            <p><strong>Vehicle:</strong> {{ vehicleLabel() }}</p>
            <p><strong>Driver:</strong> {{ driverLabel() }}</p>
            <p><strong>Type:</strong> {{ form().assignmentType }} · {{ form().purpose }}</p>
            <p><strong>Dates:</strong> {{ form().startDate }} → {{ form().endDate || 'Open' }}</p>
          </div>
          @for (issue of validationIssues(); track issue.code + issue.message) {
            <div class="alert" [class.alert--error]="issue.severity === 'Error'" [class.alert--warn]="issue.severity === 'Warning'">
              {{ issue.message }}
            </div>
          }
        }
      }

      <div class="wizard-footer">
        @if (step() > 0) {
          <ui-button variant="ghost" (clicked)="back()">Back</ui-button>
        }
        @if (step() < 4) {
          <ui-button variant="primary" [disabled]="!canNext()" (clicked)="next()">Next</ui-button>
        } @else {
          <ui-button variant="primary" [loading]="saving()" [disabled]="!canSubmit()" (clicked)="submit.emit()">Confirm Assignment</ui-button>
        }
      </div>
    </div>
  `,
  styles: [`
    .wizard { display: grid; gap: 1rem; }
    .wizard-steps { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .wizard-step { display: flex; align-items: center; gap: 0.375rem; font-size: 0.75rem; color: #94a3b8; }
    .wizard-step--active { color: #0f766e; font-weight: 700; }
    .wizard-step--done { color: #047857; }
    .wizard-step-num { width: 22px; height: 22px; border-radius: 50%; display: grid; place-items: center; background: #f1f5f9; font-size: 0.6875rem; font-weight: 700; }
    .wizard-step--active .wizard-step-num { background: #ccfbf1; }
    .field { display: grid; gap: 0.35rem; font-size: 0.8125rem; margin-bottom: 0.75rem; }
    .field span { font-weight: 600; color: #334155; }
    .input { width: 100%; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.5rem 0.75rem; font-size: 0.8125rem; }
    .date-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.625rem; }
    .review p { margin: 0.35rem 0; font-size: 0.8125rem; }
    .alert { padding: 0.5rem 0.75rem; border-radius: 8px; font-size: 0.8125rem; margin-top: 0.5rem; }
    .alert--error { background: #fef2f2; color: #b91c1c; border: 1px solid #fecaca; }
    .alert--warn { background: #fffbeb; color: #b45309; border: 1px solid #fde68a; }
    .wizard-footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; flex-wrap: wrap; }

    @media (max-width: 767px) {
      .date-row { grid-template-columns: 1fr; }
      .wizard-footer {
        flex-direction: column-reverse;
        align-items: stretch;
      }
      .wizard-footer ui-button { width: 100%; }
      .input, select.input { min-height: 44px; font-size: 0.875rem; }
      .wizard-steps { gap: 0.375rem; }
      .wizard-step-label { font-size: 0.6875rem; }
    }
  `]
})
export class AssignmentCreateWizardComponent {
  readonly vehicleOptions = input<UiSelectOption[]>([]);
  readonly driverOptions = input<UiSelectOption[]>([]);
  readonly validationIssues = input<AssignmentValidationIssue[]>([]);
  readonly saving = input(false);
  readonly canSubmit = input(true);

  readonly form = model.required<AssignmentWizardForm>();
  readonly submit = output<void>();
  readonly validateRequest = output<void>();

  readonly step = signal(0);
  readonly steps = [
    { id: 'vehicle', label: 'Vehicle' },
    { id: 'driver', label: 'Driver' },
    { id: 'type', label: 'Type' },
    { id: 'dates', label: 'Dates' },
    { id: 'confirm', label: 'Confirm' }
  ];

  readonly types = ASSIGNMENT_TYPES;
  readonly purposes = ASSIGNMENT_PURPOSES;

  patch(partial: Partial<AssignmentWizardForm>): void {
    this.form.update(f => ({ ...f, ...partial }));
  }

  canNext(): boolean {
    const f = this.form();
    if (this.step() === 0) return !!f.vehicleIdStr;
    if (this.step() === 1) return !!f.driverIdStr;
    if (this.step() === 2) return !!f.assignmentType;
    if (this.step() === 3) return !!f.startDate;
    return true;
  }

  next(): void {
    if (this.step() === 3) this.validateRequest.emit();
    if (this.step() < 4 && this.canNext()) this.step.update(s => s + 1);
    if (this.step() === 4) this.validateRequest.emit();
  }

  back(): void {
    if (this.step() > 0) this.step.update(s => s - 1);
  }

  vehicleLabel(): string {
    const id = this.form().vehicleIdStr;
    return this.vehicleOptions().find(o => o.value === id)?.label ?? '—';
  }

  driverLabel(): string {
    const id = this.form().driverIdStr;
    return this.driverOptions().find(o => o.value === id)?.label ?? '—';
  }

  reset(): void {
    this.step.set(0);
  }
}
