import {
  Component,
  ChangeDetectionStrategy,
  input,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="h-2 bg-gray-100 rounded-full overflow-hidden">
      <div
        [class]="barClasses()"
        [style.width.%]="clampedValue()"
      ></div>
    </div>
  `,
})
export class ProgressBarComponent {
  readonly value = input<number>(0);
  readonly color = input<'green' | 'yellow' | 'red' | 'blue'>('green');
  readonly height = input<'sm' | 'md' | 'lg'>('md');

  readonly clampedValue = computed(() => {
    const v = this.value();
    return Math.max(0, Math.min(100, v));
  });

  readonly barClasses = computed(() => {
    const base = 'h-full rounded-full transition-all duration-300';
    
    const colorClass = {
      green: 'bg-emerald-500',
      yellow: 'bg-amber-500',
      red: 'bg-red-500',
      blue: 'bg-blue-500',
    }[this.color()];

    return `${base} ${colorClass}`;
  });
}
