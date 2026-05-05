import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { BookingCardComponent } from '../../shared/booking-card/booking-card.component';

@Component({
  selector: 'app-my-bookings-page',
  standalone: true,
  imports: [RouterLink, BookingCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">My bookings</h1>
        <p class="mt-1 text-sm text-slate-600">Cards only — easy on mobile.</p>
      </div>
      @if (!session.hasSession()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-card">
          Save your phone on
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Profile</a>
          to load trips.
        </div>
      } @else if (error()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
      } @else if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (allItems().length === 0) {
        <p class="rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center text-sm text-slate-600">No bookings yet.</p>
      } @else if (items().length === 0) {
        <p class="rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
          No bookings match “{{ filterQ() }}”. Try another search.
        </p>
      } @else {
        <div class="space-y-4">
          @for (b of items(); track b.id) {
            <app-booking-card [card]="b" />
          }
        </div>
      }
    </div>
  `
})
export class MyBookingsPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly allItems = signal<PortalBookingCardDto[]>([]);
  readonly filterQ = signal('');

  readonly items = computed(() => {
    const q = this.filterQ().trim().toLowerCase();
    const list = this.allItems();
    if (!q) return list;
    return list.filter(
      (b) =>
        b.bookingNumber.toLowerCase().includes(q) || b.routeLabel.toLowerCase().includes(q)
    );
  });

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((pm) => {
      this.filterQ.set(pm.get('q') ?? '');
    });

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
          this.allItems.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load bookings.');
          this.loading.set(false);
        }
      });
  }
}
