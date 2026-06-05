import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { PortalCustomerNotificationDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      li.unread-notif {
        background: color-mix(in srgb, var(--stb-primary, #1d4ed8) 8%, white);
      }
    `
  ],
  template: `
    <div class="space-y-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Notifications</h1>
        <p class="mt-1 text-sm text-slate-600">Booking updates and trip alerts.</p>
      </div>

      @if (!session.isAuthenticated()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-card">
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Sign in</a>
          to see your notifications.
        </div>
      } @else if (error()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
      } @else if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (items().length === 0) {
        <p class="rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
          No notifications yet. You will see updates when you book or when your driver is assigned.
        </p>
      } @else {
        <ul class="space-y-3">
          @for (n of items(); track n.id) {
            <li
              class="rounded-2xl border bg-white p-4 shadow-card text-sm"
              [class.border-primary-200]="n.isRead === false"
              [class.border-slate-200]="n.isRead"
              [class.unread-notif]="n.isRead === false"
            >
              <div class="flex justify-between gap-2">
                <p class="font-semibold text-slate-900">{{ n.title }}</p>
                <time class="shrink-0 text-xs text-slate-500">{{ n.createdAt | date : 'short' }}</time>
              </div>
              <p class="mt-1 text-slate-700">{{ n.message }}</p>
              @if (n.bookingId) {
                <a [routerLink]="['/bookings', n.bookingId]" class="mt-2 inline-block text-xs font-semibold text-primary-600"
                  >View booking</a
                >
              }
            </li>
          }
        </ul>
      }
    </div>
  `
})
export class NotificationsPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PortalCustomerNotificationDto[]>([]);

  constructor() {
    if (!this.session.isAuthenticated()) {
      this.loading.set(false);
      return;
    }
    this.api
      .getNotifications()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.items.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load notifications.');
          this.loading.set(false);
        }
      });
  }
}
