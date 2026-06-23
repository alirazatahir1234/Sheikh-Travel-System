import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { APP_PRODUCT_NAME, APP_SIDEBAR_LOGO_PATH } from '../../../../core/constants/app-brand';
import { FLEET_NAV_FOOTER, FLEET_NAV_GROUPS, FleetNavLink } from '../fleet-nav.config';

@Component({
  selector: 'fleet-sidebar',
  standalone: true,
  imports: [NgTemplateOutlet, RouterLink, RouterLinkActive, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="flex h-full w-sidebar flex-col bg-fleet-surface-alt py-6 font-fleet">
      <div class="mb-8 px-6">
        <a routerLink="/dashboard" class="fleet-brand" [attr.aria-label]="appProductName">
          <div class="fleet-brand__logo">
            <img [src]="appLogoPath" [alt]="appProductName" />
          </div>
          <span class="fleet-brand__label">{{ appProductName }}</span>
        </a>
      </div>

      <nav class="hide-scrollbar flex-1 space-y-6 overflow-y-auto px-4">
        @for (group of groups; track group.id) {
          <div>
            <h3 class="mb-2 px-4 font-label text-[12px] font-bold uppercase tracking-wider text-fleet-text-muted/70">
              {{ group.label }}
            </h3>
            <div class="space-y-1">
              @for (item of group.items; track item.id) {
                <ng-container [ngTemplateOutlet]="navLink" [ngTemplateOutletContext]="{ $implicit: item }"></ng-container>
              }
            </div>
          </div>
        }
      </nav>

      <div class="mt-auto space-y-1 px-4 pt-4">
        @for (item of footer; track item.id) {
          <ng-container [ngTemplateOutlet]="navLink" [ngTemplateOutletContext]="{ $implicit: item }"></ng-container>
        }
      </div>
    </aside>

    <ng-template #navLink let-item>
      @if (item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="is-active"
          [routerLinkActiveOptions]="{ exact: !!item.exact }"
          class="fleet-nav-link"
          (click)="navigate.emit()">
          <mat-icon class="!text-[20px]">{{ item.icon }}</mat-icon>
          <span class="text-sm font-medium">{{ item.label }}</span>
        </a>
      } @else {
        <span class="fleet-nav-link cursor-default opacity-60">
          <mat-icon class="!text-[20px]">{{ item.icon }}</mat-icon>
          <span class="text-sm font-medium">{{ item.label }}</span>
        </span>
      }
    </ng-template>
  `,
  styles: [`
    :host { display: block; height: 100%; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
    .hide-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
    .hide-scrollbar::-webkit-scrollbar { display: none; }

    .fleet-brand {
      display: flex;
      align-items: center;
      gap: 10px;
      border-radius: 12px;
      padding: 8px;
      text-decoration: none;
      transition: background 0.15s ease;
    }
    .fleet-brand:hover {
      background: rgba(218, 227, 238, 0.5);
    }
    .fleet-brand__logo {
      width: 52px;
      height: 52px;
      border-radius: 13px;
      background: #fff;
      border: 1px solid rgba(17, 129, 119, 0.18);
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 1px 2px rgba(2, 6, 23, 0.05);
      overflow: hidden;
    }
    .fleet-brand__logo img {
      width: 100%;
      height: 100%;
      object-fit: contain;
      object-position: center;
      display: block;
    }

    .fleet-brand__label {
      min-width: 0;
      font-size: 0.92rem;
      font-weight: 700;
      letter-spacing: -0.01em;
      line-height: 1.25;
      color: var(--fleet-text);
    }

    .fleet-nav-link {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 16px;
      border-radius: 10px;
      color: var(--fleet-text-muted);
      text-decoration: none;
      border-left: 4px solid transparent;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .fleet-nav-link:hover {
      background: rgba(218, 227, 238, 0.5);
      color: var(--fleet-text);
    }
    a.fleet-nav-link.is-active {
      background: var(--fleet-primary-soft);
      color: var(--fleet-primary);
      border-left-color: var(--fleet-primary);
      font-weight: 600;
    }
  `]
})
export class FleetSidebarComponent {
  readonly appProductName = APP_PRODUCT_NAME;
  readonly appLogoPath = APP_SIDEBAR_LOGO_PATH;
  readonly navigate = output<void>();

  protected readonly groups = FLEET_NAV_GROUPS;
  protected readonly footer: FleetNavLink[] = FLEET_NAV_FOOTER;
}
