import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CustomerSessionService } from '../core/services/customer-session.service';

interface PortalNavItem {
  readonly route: string;
  readonly icon: string;
  readonly label: string;
  readonly exact: boolean;
}

@Component({
  selector: 'app-portal-shell',
  standalone: true,
  imports: [
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule
  ],
  templateUrl: './portal-shell.component.html',
  styleUrl: './portal-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PortalShellComponent {
  readonly session = inject(CustomerSessionService);
  private readonly router = inject(Router);

  searchQuery = '';

  readonly navItems: readonly PortalNavItem[] = [
    { route: '/dashboard', icon: 'dashboard', label: 'Dashboard', exact: true },
    { route: '/book', icon: 'edit_calendar', label: 'Book a ride', exact: true },
    { route: '/bookings', icon: 'confirmation_number', label: 'My bookings', exact: true },
    { route: '/payments', icon: 'account_balance_wallet', label: 'Payments', exact: true }
  ];

  /** Local clock (admin shell shows HQ time; portal shows browser-local). */
  readonly timeDisplay = signal<{ time: string; sub: string }>({ time: '', sub: '' });

  constructor() {
    const tick = (): void => {
      const now = new Date();
      this.timeDisplay.set({
        time: now.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }),
        sub: now.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
      });
    };
    tick();
    setInterval(tick, 30_000);
  }

  onSearchEnter(): void {
    const q = this.searchQuery.trim();
    void this.router.navigate(['/bookings'], q ? { queryParams: { q } } : {});
  }

  clearSession(): void {
    this.session.clear();
    void this.router.navigate(['/profile']);
  }

  initials(fullName?: string | null): string {
    if (!fullName?.trim()) return '?';
    return fullName
      .trim()
      .split(/\s+/)
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
