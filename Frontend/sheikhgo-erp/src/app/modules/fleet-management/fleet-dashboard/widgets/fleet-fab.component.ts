import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'fleet-fab',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="fixed bottom-8 right-8 z-50 flex h-14 w-14 items-center justify-center rounded-full bg-fleet-primary text-white shadow-2xl transition-transform hover:scale-110 active:scale-95"
      [attr.aria-label]="ariaLabel()"
      (click)="action.emit()">
      <mat-icon class="!text-[30px]">{{ icon() }}</mat-icon>
    </button>
  `,
  styles: [`mat-icon { display: inline-flex; align-items: center; justify-content: center; }`]
})
export class FleetFabComponent {
  readonly icon = input('add');
  readonly ariaLabel = input('Quick add');

  readonly action = output<void>();
}
