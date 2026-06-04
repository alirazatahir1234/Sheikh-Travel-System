import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Input,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { take } from 'rxjs/operators';
import { PortalBookingTrackingDto } from '../../core/models/portal.models';
import { PortalApiService } from '../../core/services/portal-api.service';
import { PortalTrackingService } from '../../core/services/portal-tracking.service';

@Component({
  selector: 'app-booking-tracking',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (tracking()?.trackingAvailable) {
      <section class="rounded-2xl border border-sky-200 bg-sky-50/60 p-4 shadow-card space-y-2">
        <h2 class="text-xs font-semibold uppercase tracking-wide text-sky-800">Live trip</h2>
        @if (tracking()!.vehicleName) {
          <p class="text-sm text-slate-700">Vehicle: <strong>{{ tracking()!.vehicleName }}</strong></p>
        }
        @if (tracking()!.etaMinutes != null) {
          <p class="text-lg font-bold text-sky-900">ETA ~ {{ tracking()!.etaMinutes }} min</p>
        }
        @if (tracking()!.distanceKm != null) {
          <p class="text-sm text-slate-600">{{ tracking()!.distanceKm | number : '1.1-1' }} km to pickup</p>
        }
        @if (tracking()!.speedKmh != null) {
          <p class="text-sm text-slate-600">Speed: {{ tracking()!.speedKmh | number : '1.0-0' }} km/h</p>
        }
        @if (tracking()!.lastUpdatedUtc) {
          <p class="text-xs text-slate-500">Last update: {{ tracking()!.lastUpdatedUtc | date : 'short' }}</p>
        }
        @if (tracking()!.driverPhoneMasked) {
          <a [href]="'tel:' + tracking()!.driverPhoneMasked" class="text-sm font-semibold text-primary-600 underline"
            >Call driver</a
          >
        }
        @if (mapUrl()) {
          <a
            [href]="mapUrl()"
            target="_blank"
            rel="noopener noreferrer"
            class="inline-flex text-sm font-semibold text-primary-600 underline"
            >Open driver location in Maps</a
          >
        }
        <p class="text-xs text-slate-500">Updates every few seconds while your driver is en route.</p>
      </section>
    }
  `
})
export class BookingTrackingComponent implements OnInit {
  @Input({ required: true }) bookingId!: number;
  @Input() bookingStatus = 0;

  private readonly api = inject(PortalApiService);
  private readonly realtime = inject(PortalTrackingService);
  private readonly destroyRef = inject(DestroyRef);

  readonly tracking = signal<PortalBookingTrackingDto | null>(null);

  ngOnInit(): void {
    const active = this.bookingStatus === 2 || this.bookingStatus === 3;
    if (!active) return;

    this.api
      .getBookingTracking(this.bookingId)
      .pipe(take(1))
      .subscribe({
        next: (t) => {
          this.tracking.set(t);
          if (t.trackingAvailable && t.vehicleId) {
            void this.realtime.connectAndSubscribeBooking(this.bookingId, t.vehicleId);
            this.realtime.locationUpdates$
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe((u) => {
                if (u.bookingId && u.bookingId !== this.bookingId) return;
                this.tracking.update((prev) =>
                  prev
                    ? {
                        ...prev,
                        driverLatitude: u.latitude,
                        driverLongitude: u.longitude,
                        trackingAvailable: true
                      }
                    : prev
                );
              });
          }
        }
      });
  }

  mapUrl(): string | null {
    const t = this.tracking();
    if (!t?.driverLatitude || !t?.driverLongitude) return null;
    return `https://www.google.com/maps?q=${t.driverLatitude},${t.driverLongitude}`;
  }
}
