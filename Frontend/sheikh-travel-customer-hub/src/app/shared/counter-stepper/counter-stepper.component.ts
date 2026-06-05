import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-counter-stepper',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-3 py-2">
      <span class="text-sm font-medium text-slate-800">{{ label() }}</span>
      <div class="flex items-center gap-2">
        <button type="button" class="size-8 rounded-lg border border-slate-200 font-bold" (click)="dec()">−</button>
        <span class="w-8 text-center font-semibold">{{ value() }}</span>
        <button type="button" class="size-8 rounded-lg border border-slate-200 font-bold" (click)="inc()">+</button>
      </div>
    </div>
  `
})
export class CounterStepperComponent {
  readonly label = input('Count');
  readonly value = input(0);
  readonly min = input(0);
  readonly max = input(99);
  readonly valueChange = output<number>();

  dec(): void {
    this.valueChange.emit(Math.max(this.min(), this.value() - 1));
  }

  inc(): void {
    this.valueChange.emit(Math.min(this.max(), this.value() + 1));
  }
}
