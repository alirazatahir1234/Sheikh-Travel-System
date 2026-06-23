import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export interface WizardStepConfig {
  id: string;
  number: number;
  label: string;
}

@Component({
  selector: 'app-wizard-stepper',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="flex flex-wrap items-center gap-2 md:gap-0" aria-label="Registration steps">
      @for (step of steps(); track step.id; let i = $index; let last = $last) {
        <button
          type="button"
          class="flex items-center gap-2 rounded-sm px-2 py-1 text-left transition-colors"
          [class.opacity-50]="!isActive(step.id) && !isComplete(step.id)"
          (click)="stepClick.emit(step.id)">
          <span
            class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold"
            [class.bg-fleet-primary]="isActive(step.id)"
            [class.text-white]="isActive(step.id)"
            [class.bg-emerald-100]="isComplete(step.id) && !isActive(step.id)"
            [class.text-emerald-700]="isComplete(step.id) && !isActive(step.id)"
            [class.bg-fleet-surface-muted]="!isActive(step.id) && !isComplete(step.id)"
            [class.text-fleet-text-muted]="!isActive(step.id) && !isComplete(step.id)">
            @if (isComplete(step.id) && !isActive(step.id)) {
              <mat-icon class="!text-[18px]">check</mat-icon>
            } @else {
              {{ step.number }}
            }
          </span>
          <span
            class="hidden text-sm font-medium sm:inline"
            [class.text-fleet-primary]="isActive(step.id)"
            [class.text-fleet-text]="!isActive(step.id)">
            {{ step.label }}
          </span>
        </button>
        @if (!last) {
          <mat-icon class="hidden text-fleet-border md:inline !text-[20px]">chevron_right</mat-icon>
        }
      }
    </nav>
  `
})
export class WizardStepperComponent {
  readonly steps = input.required<WizardStepConfig[]>();
  readonly currentStep = input.required<string>();
  readonly stepClick = output<string>();

  isActive(id: string): boolean {
    return this.currentStep() === id;
  }

  isComplete(id: string): boolean {
    const steps = this.steps();
    const currentIdx = steps.findIndex(s => s.id === this.currentStep());
    const stepIdx = steps.findIndex(s => s.id === id);
    return stepIdx < currentIdx;
  }
}
