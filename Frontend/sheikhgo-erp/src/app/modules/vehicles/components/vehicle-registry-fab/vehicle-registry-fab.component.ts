import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

interface FabAction {
  label: string;
  icon: string;
  route?: string;
  disabled?: boolean;
  title?: string;
}

@Component({
  selector: 'vehicle-registry-fab',
  standalone: true,
  imports: [RouterModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fab-root">
      @if (expanded()) {
        <div class="fab-menu">
          @for (action of actions; track action.label) {
            @if (action.route && !action.disabled) {
              <a [routerLink]="action.route" class="fab-item" [title]="action.title ?? action.label" (click)="expanded.set(false)">
                <mat-icon>{{ action.icon }}</mat-icon>
                <span>{{ action.label }}</span>
              </a>
            } @else {
              <button type="button" class="fab-item" [disabled]="action.disabled" [title]="action.title ?? action.label">
                <mat-icon>{{ action.icon }}</mat-icon>
                <span>{{ action.label }}</span>
              </button>
            }
          }
        </div>
      }

      <button type="button" class="fab-trigger" [class.fab-trigger--open]="expanded()" (click)="toggle()" aria-label="Quick actions">
        <mat-icon>{{ expanded() ? 'close' : 'add' }}</mat-icon>
      </button>
    </div>
  `,
  styles: [`
    :host { display: none; }
    @media (max-width: 767px) {
      :host { display: block; }
    }
    .fab-root { position: fixed; bottom: 1.25rem; right: 1rem; z-index: 40; display: flex; flex-direction: column; align-items: flex-end; gap: 0.75rem; }
    .fab-menu { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 0.25rem; }
    .fab-item {
      display: inline-flex; align-items: center; gap: 0.5rem;
      padding: 0.5rem 0.875rem; border-radius: 9999px;
      background: white; border: 1px solid #e2e8f0;
      box-shadow: 0 4px 12px rgba(0,0,0,0.08);
      font-size: 0.8125rem; font-weight: 600; color: #1e293b;
      text-decoration: none; white-space: nowrap;
    }
    .fab-item:disabled { opacity: 0.5; cursor: not-allowed; }
    .fab-trigger {
      display: flex; align-items: center; justify-content: center;
      width: 3.5rem; height: 3.5rem; border-radius: 9999px;
      background: #005f49; color: white; border: none;
      box-shadow: 0 8px 24px rgba(0,95,73,0.35);
      cursor: pointer; transition: transform 0.15s;
    }
    .fab-trigger:hover { transform: scale(1.05); }
    .fab-trigger--open { background: #334155; }
    mat-icon { display: inline-flex; }
  `]
})
export class VehicleRegistryFabComponent {
  readonly expanded = signal(false);

  readonly actions: FabAction[] = [
    { label: 'Add Vehicle', icon: 'directions_bus', route: '/vehicles/new' },
    { label: 'Import', icon: 'upload', disabled: true, title: 'Coming soon' },
    { label: 'Assign Tracker', icon: 'gps_fixed', route: '/gps-tracking/devices' },
    { label: 'Add Driver', icon: 'person_add', route: '/drivers/new' }
  ];

  toggle(): void {
    this.expanded.update(v => !v);
  }
}
