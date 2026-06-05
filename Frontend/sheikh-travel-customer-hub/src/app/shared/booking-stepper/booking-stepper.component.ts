import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface BookingStep {
  label: string;
  complete: boolean;
}

@Component({
  selector: 'app-booking-stepper',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-0">
      @for (step of steps(); track step.label; let i = $index; let last = $last) {
        <li class="flex flex-1 items-center gap-2 min-w-0">
          <span
            class="flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            [class.bg-primary-600]="activeIndex() >= i"
            [class.text-white]="activeIndex() >= i"
            [class.bg-emerald-100]="step.complete && activeIndex() > i"
            [class.text-emerald-800]="step.complete && activeIndex() > i"
            [class.bg-slate-200]="activeIndex() < i && !step.complete"
            [class.text-slate-600]="activeIndex() < i && !step.complete"
          >
            @if (step.complete && activeIndex() > i) {
              ✓
            } @else {
              {{ i + 1 }}
            }
          </span>
          <span
            class="truncate text-xs font-semibold sm:text-sm"
            [class.text-primary-800]="activeIndex() === i"
            [class.text-slate-700]="activeIndex() !== i"
            >{{ step.label }}</span
          >
          @if (!last) {
            <span class="mx-2 hidden h-px flex-1 bg-slate-200 sm:block" aria-hidden="true"></span>
          }
        </li>
      }
    </ol>
  `
})
export class BookingStepperComponent {
  readonly steps = input.required<BookingStep[]>();
  readonly activeIndex = input(0);
}
