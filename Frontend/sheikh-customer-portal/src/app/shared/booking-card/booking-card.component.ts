import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PortalBookingCardDto } from '../../core/models/portal.models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-booking-card',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      class="rounded-2xl border border-slate-200 bg-white p-4 shadow-card"
      [class.opacity-60]="card().bookingStatus === 5"
    >
      <div class="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p class="text-sm font-semibold text-slate-900">{{ card().routeLabel || 'Route' }}</p>
          <p class="text-xs text-slate-500">{{ card().pickupTime | date : 'medium' }}</p>
          <p class="mt-1 text-xs font-medium text-slate-600">{{ card().bookingNumber }}</p>
        </div>
        <app-status-badge [payState]="card().payState" />
      </div>
      <div class="mt-3 grid grid-cols-2 gap-2 text-sm">
        <div>
          <p class="text-xs text-slate-500">Paid</p>
          <p class="font-semibold text-emerald-700">PKR {{ card().paidAmount | number : '1.2-2' }}</p>
        </div>
        <div>
          <p class="text-xs text-slate-500">Remaining</p>
          <p class="font-semibold" [class.text-rose-600]="card().remaining > 0" [class.text-slate-400]="card().remaining <= 0">
            PKR {{ card().remaining | number : '1.2-2' }}
          </p>
        </div>
      </div>
      <div class="mt-2 flex flex-wrap gap-2">
        <app-status-badge [bookingStatus]="card().bookingStatus" />
      </div>
      <div class="mt-4 flex flex-wrap gap-2">
        <a
          [routerLink]="['/bookings', card().id]"
          class="inline-flex flex-1 min-w-[8rem] justify-center rounded-xl bg-slate-100 px-4 py-2.5 text-sm font-semibold text-slate-800 ring-1 ring-slate-200 transition hover:bg-slate-200"
        >
          View details
        </a>
        @if (card().remaining > 0 && card().bookingStatus !== 5) {
          <a
            [routerLink]="['/bookings', card().id]"
            [queryParams]="{ pay: '1' }"
            class="inline-flex flex-1 min-w-[8rem] justify-center rounded-xl bg-primary-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-primary-700"
          >
            Pay now
          </a>
        }
      </div>
    </article>
  `
})
export class BookingCardComponent {
  readonly card = input.required<PortalBookingCardDto>();
}
