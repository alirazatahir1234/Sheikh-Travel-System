import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { BookingCardComponent } from '../../shared/booking-card/booking-card.component';

type BookingTab = 'upcoming' | 'completed' | 'cancelled';

@Component({
  selector: 'app-my-bookings-page',
  standalone: true,
  imports: [RouterLink, BookingCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">My bookings</h1>
        <p class="mt-1 text-sm text-slate-600">Upcoming, completed, and cancelled trips.</p>
      </div>

      @if (!session.isAuthenticated()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-card">
          Save your phone on
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Profile</a>
          to load trips.
        </div>
      } @else {
        <div class="flex flex-wrap gap-2 border-b border-slate-200 pb-2">
          @for (t of tabs; track t.id) {
            <button
              type="button"
              class="rounded-full px-4 py-1.5 text-sm font-semibold transition"
              [class.bg-primary-600]="activeTab() === t.id"
              [class.text-white]="activeTab() === t.id"
              [class.bg-slate-100]="activeTab() !== t.id"
              [class.text-slate-700]="activeTab() !== t.id"
              (click)="activeTab.set(t.id)"
            >
              {{ t.label }}
            </button>
          }
        </div>

        @if (error()) {
          <p class="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">{{ error() }}</p>
        } @else if (loading()) {
          <p class="text-sm text-slate-500">Loading…</p>
        } @else if (items().length === 0) {
          <p class="rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
            @switch (activeTab()) {
              @case ('upcoming') {
                No upcoming bookings.
              }
              @case ('completed') {
                No completed trips yet.
              }
              @default {
                No cancelled bookings.
              }
            }
          </p>
        } @else {
          <div class="space-y-4">
            @for (b of items(); track b.id) {
              <app-booking-card [card]="b" (cancelled)="loadBookings()" />
            }
          </div>
        }
      }
    </div>
  `
})
export class MyBookingsPageComponent {
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly tabs: { id: BookingTab; label: string }[] = [
    { id: 'upcoming', label: 'Upcoming' },
    { id: 'completed', label: 'Completed' },
    { id: 'cancelled', label: 'Cancelled' }
  ];

  readonly activeTab = signal<BookingTab>('upcoming');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly allItems = signal<PortalBookingCardDto[]>([]);
  readonly filterQ = signal('');

  readonly items = computed(() => {
    const q = this.filterQ().trim().toLowerCase();
    let list = this.allItems();
    const tab = this.activeTab();
    if (tab === 'upcoming') {
      list = list.filter((b) => b.bookingStatus === 1 || b.bookingStatus === 2 || b.bookingStatus === 3);
    } else if (tab === 'completed') {
      list = list.filter((b) => b.bookingStatus === 4);
    } else {
      list = list.filter((b) => b.bookingStatus === 5);
    }
    if (!q) return list;
    return list.filter(
      (b) => b.bookingNumber.toLowerCase().includes(q) || b.routeLabel.toLowerCase().includes(q)
    );
  });

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((pm) => {
      this.filterQ.set(pm.get('q') ?? '');
    });

    this.loadBookings();
    effect(() => {
      this.session.sessionVersion();
      this.loadBookings();
    });
  }

  loadBookings(): void {
    if (!this.session.isAuthenticated()) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.api
      .getMyBookings()
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
