import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'ui-empty-state',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center px-6 py-12 text-center">
      <span class="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-fleet-surface-muted text-fleet-text-muted">
        <mat-icon class="!text-[28px]">{{ icon() }}</mat-icon>
      </span>
      <h3 class="text-base font-bold text-fleet-text">{{ title() }}</h3>
      @if (description()) {
        <p class="mt-1 max-w-sm text-sm text-fleet-text-muted">{{ description() }}</p>
      }
      <div class="mt-5 empty:hidden">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class UiEmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('Nothing here yet');
  readonly description = input<string>();
}
