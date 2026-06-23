import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { UiBreadcrumb } from '../types/ui.types';

@Component({
  selector: 'ui-page-header',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div class="min-w-0">
        @if (breadcrumbs().length) {
          <nav class="mb-1 flex flex-wrap items-center gap-1 text-[12px] text-fleet-text-muted">
            @for (crumb of breadcrumbs(); track crumb.label; let last = $last) {
              @if (crumb.route && !last) {
                <a [routerLink]="crumb.route" class="hover:text-fleet-primary">{{ crumb.label }}</a>
              } @else {
                <span [class.font-semibold]="last" [class.text-fleet-text]="last">{{ crumb.label }}</span>
              }
              @if (!last) { <mat-icon class="!h-4 !w-4 !text-[16px] opacity-60">chevron_right</mat-icon> }
            }
          </nav>
        }

        @if (eyebrow()) {
          <p class="text-[12px] font-bold uppercase tracking-wider text-fleet-primary">{{ eyebrow() }}</p>
        }

        <h1 class="flex items-center gap-2 text-2xl font-bold tracking-tight text-fleet-text sm:text-3xl">
          @if (icon()) { <mat-icon class="text-fleet-primary">{{ icon() }}</mat-icon> }
          {{ title() }}
        </h1>

        @if (subtitle()) {
          <p class="mt-1 text-sm text-fleet-text-muted">{{ subtitle() }}</p>
        }
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <ng-content></ng-content>
      </div>
    </header>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; }
  `]
})
export class UiPageHeaderComponent {
  readonly title = input('');
  readonly subtitle = input<string>();
  readonly eyebrow = input<string>();
  readonly icon = input<string>();
  readonly breadcrumbs = input<UiBreadcrumb[]>([]);
}
