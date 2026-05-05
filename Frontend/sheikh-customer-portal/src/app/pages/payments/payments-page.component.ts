import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';

@Component({
  selector: 'app-payments-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Payments</h1>
        <p class="mt-1 text-sm text-slate-600">Outstanding balances for your trips.</p>
      </div>
      @if (!session.hasSession()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-5 text-sm shadow-card">
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Connect your phone</a>
          first.
        </div>
      } @else if (error()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
      } @else if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (outstanding().length === 0) {
        <p class="rounded-2xl border border-emerald-100 bg-emerald-50/80 p-6 text-center text-sm font-medium text-emerald-900">
          You have no outstanding balances.
        </p>
      } @else {
        <div class="space-y-4">
          @for (b of outstanding(); track b.id) {
            <article class="rounded-2xl border border-rose-100 bg-white p-4 shadow-card">
              <p class="text-xs font-semibold uppercase text-slate-500">Outstanding</p>
              <p class="mt-1 text-lg font-bold text-slate-900">{{ b.bookingNumber }}</p>
              <p class="text-sm text-slate-600">{{ b.routeLabel }}</p>
              <p class="mt-3 text-sm text-slate-600">
                Remaining:
                <span class="text-lg font-bold text-rose-600">PKR {{ b.remaining | number : '1.2-2' }}</span>
              </p>
              <a
                [routerLink]="['/bookings', b.id]"
                [queryParams]="{ pay: '1' }"
                class="mt-4 inline-flex w-full justify-center rounded-xl bg-primary-600 py-2.5 text-sm font-bold text-white hover:bg-primary-700"
                >Pay now</a
              >
            </article>
          }
        </div>
      }
    </div>
  `
})
export class PaymentsPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly outstanding = signal<PortalBookingCardDto[]>([]);

  constructor() {
    const phone = this.session.phone();
    if (!phone) {
      this.loading.set(false);
      return;
    }
    this.api
      .getMyBookings(phone)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.outstanding.set(list.filter((b) => b.remaining > 0 && b.bookingStatus !== 5));
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load balances.');
          this.loading.set(false);
        }
      });
  }
}
