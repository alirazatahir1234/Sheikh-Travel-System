import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { BookingCardComponent } from '../../shared/booking-card/booking-card.component';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink, BookingCardComponent, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Dashboard</h1>
        <p class="mt-1 text-sm text-slate-600">Your trips, loyalty, and wallet at a glance.</p>
      </div>

      @if (!session.hasSession()) {
        <div class="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950">
          <p class="font-semibold">Connect your profile</p>
          <p class="mt-1 text-amber-900/90">Add your phone on the Profile page to see active bookings and history.</p>
          <a routerLink="/profile" class="mt-3 inline-flex rounded-xl bg-amber-600 px-4 py-2 text-sm font-semibold text-white hover:bg-amber-700"
            >Go to profile</a
          >
        </div>
      } @else if (error()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
      } @else {
        @if (session.isAuthenticated()) {
          <div class="grid gap-3 sm:grid-cols-2">
            @if (loyalty()) {
              <div class="rounded-2xl border border-primary-200 bg-primary-50/40 p-4 shadow-card">
                <p class="text-xs font-semibold uppercase text-primary-800">Loyalty</p>
                <p class="mt-1 text-2xl font-bold text-slate-900">{{ loyalty()!.points }} pts</p>
                <p class="text-sm text-slate-600">Tier: {{ loyalty()!.tier }}</p>
              </div>
            }
            @if (wallet()) {
              <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card">
                <p class="text-xs font-semibold uppercase text-slate-500">Wallet balance</p>
                <p class="mt-1 text-2xl font-bold text-slate-900">PKR {{ wallet()!.balance | number : '1.2-2' }}</p>
                <p class="text-xs text-slate-500">Top-up via gateway coming soon.</p>
              </div>
            }
          </div>
        }

        @if (active()) {
          <section>
            <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Active booking</h2>
            <app-booking-card [card]="active()!" />
          </section>
        } @else if (!loading()) {
          <p class="rounded-2xl border border-dashed border-slate-200 bg-white p-6 text-center text-sm text-slate-600">
            No upcoming booking with a balance. Book your next ride anytime.
          </p>
        }
      }

      <section class="grid gap-3 sm:grid-cols-2">
        <a
          routerLink="/book"
          class="flex flex-col rounded-2xl bg-primary-600 p-4 text-white shadow-lg shadow-primary-600/25 transition hover:bg-primary-700"
        >
          <span class="text-sm font-semibold opacity-90">Quick action</span>
          <span class="mt-1 text-lg font-bold">Book a ride</span>
        </a>
        <a
          routerLink="/bookings"
          class="flex flex-col rounded-2xl border border-slate-200 bg-white p-4 shadow-card transition hover:border-primary-200"
        >
          <span class="text-sm font-semibold text-slate-500">Quick action</span>
          <span class="mt-1 text-lg font-bold text-slate-900">My bookings</span>
        </a>
      </section>
    </div>
  `
})
export class DashboardPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly active = signal<PortalBookingCardDto | null>(null);
  readonly loyalty = signal<{ points: number; tier: string } | null>(null);
  readonly wallet = signal<{ balance: number } | null>(null);

  constructor() {
    this.reload();
  }

  reload(): void {
    if (!this.session.isAuthenticated()) {
      this.loading.set(false);
      this.active.set(null);
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      bookings: this.api.getMyBookings(),
      loyalty: this.api.getLoyalty().pipe(catchError(() => of(null))),
      wallet: this.api.getWallet().pipe(catchError(() => of(null)))
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ bookings, loyalty, wallet }) => {
          const open = bookings.filter((b) => b.bookingStatus !== 5 && b.bookingStatus !== 4);
          const withBalance = open.filter((b) => b.remaining > 0);
          const pick =
            withBalance.sort(
              (x, y) => new Date(x.pickupTime).getTime() - new Date(y.pickupTime).getTime()
            )[0] ??
            open.sort((x, y) => new Date(y.pickupTime).getTime() - new Date(x.pickupTime).getTime())[0] ??
            null;
          this.active.set(pick);
          this.loyalty.set(loyalty);
          this.wallet.set(wallet);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load your bookings.');
          this.loading.set(false);
        }
      });
  }
}
