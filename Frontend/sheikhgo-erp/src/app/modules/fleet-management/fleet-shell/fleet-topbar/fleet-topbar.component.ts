import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'fleet-topbar',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="sticky top-0 z-40 flex items-center gap-4 bg-fleet-surface px-page py-4 font-fleet">
      <button
        type="button"
        class="rounded-full p-2 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted lg:hidden"
        aria-label="Open navigation"
        (click)="menuToggle.emit()">
        <mat-icon>menu</mat-icon>
      </button>

      <ng-content select="[topbar-start]"></ng-content>

      <div class="max-w-md flex-1">
        <div class="relative">
          <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
          <input
            type="search"
            placeholder="Search fleet, drivers, or orders..."
            class="w-full rounded-full border-none bg-fleet-surface-muted py-2 pl-10 pr-4 text-sm focus:outline-none focus:ring-2 focus:ring-fleet-primary/20" />
        </div>
      </div>

      <div class="flex items-center gap-3 sm:gap-5">
        <button type="button" class="relative rounded-full p-2 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted" aria-label="Notifications">
          <mat-icon>notifications</mat-icon>
          <span class="absolute right-2 top-2 h-2 w-2 rounded-full border-2 border-fleet-surface bg-fleet-error"></span>
        </button>
        <button type="button" class="rounded-full p-2 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted" aria-label="Settings">
          <mat-icon>settings</mat-icon>
        </button>

        <div class="ml-1 flex cursor-pointer items-center gap-3 rounded-full p-1 pr-2 transition-colors hover:bg-fleet-surface-muted">
          <div class="hidden text-right lg:block">
            <p class="text-[13px] font-semibold leading-tight text-fleet-text">{{ userName() }}</p>
            <p class="text-[11px] leading-tight text-fleet-text-muted">{{ userRole() }}</p>
          </div>
          <span class="flex h-10 w-10 items-center justify-center rounded-full bg-fleet-primary/15 text-sm font-bold text-fleet-primary">
            {{ initials() }}
          </span>
        </div>
      </div>
    </header>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class FleetTopbarComponent {
  readonly userName = input('Alex Thompson');
  readonly userRole = input('Fleet Director');

  readonly menuToggle = output<void>();

  protected readonly initials = computed(() =>
    this.userName()
      .split(' ')
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase()
  );
}
