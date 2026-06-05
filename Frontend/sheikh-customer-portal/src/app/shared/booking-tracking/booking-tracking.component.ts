import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Input,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { switchMap, take } from 'rxjs/operators';
import { PortalBookingTrackingDto } from '../../core/models/portal.models';
import { PortalApiService } from '../../core/services/portal-api.service';
import { PortalTrackingService } from '../../core/services/portal-tracking.service';
import { TripMapComponent, TripMapPoint } from '../trip-map/trip-map.component';

const STEPS = [
  { status: 2, label: 'Driver assigned' },
  { status: 3, label: 'On the way' },
  { status: 4, label: 'Trip completed' }
] as const;

@Component({
  selector: 'app-booking-tracking',
  standalone: true,
  imports: [DatePipe, DecimalPipe, TripMapComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bookingStatus === 2 || bookingStatus === 3 || tracking()) {
      <section class="rounded-2xl border border-sky-200 bg-sky-50/60 p-4 shadow-card space-y-4">
        <h2 class="text-xs font-semibold uppercase tracking-wide text-sky-800">Live trip</h2>

        <ol class="flex flex-wrap gap-2 text-xs">
          @for (step of steps; track step.status) {
            <li
              class="rounded-full px-3 py-1 font-medium"
              [class.bg-sky-600]="bookingStatus >= step.status"
              [class.text-white]="bookingStatus >= step.status"
              [class.bg-white]="bookingStatus < step.status"
              [class.text-slate-600]="bookingStatus < step.status"
            >
              {{ step.label }}
            </li>
          }
        </ol>

        @if (tracking()?.trackingAvailable) {
          @if (driverMapPoint()) {
            <app-trip-map [pickup]="pickupMapPoint()" [driver]="driverMapPoint()" />
          }
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
        } @else if (bookingStatus === 2) {
          <p class="text-sm text-slate-600">Your driver is assigned. Live map will appear when the trip starts.</p>
        } @else {
          <p class="text-sm text-slate-600">Waiting for live GPS from the vehicle…</p>
        }
        <p class="text-xs text-slate-500">Updates every few seconds while your driver is en route.</p>
      </section>
    }
  `
})
export class BookingTrackingComponent implements OnInit {
  @Input({ required: true }) bookingId!: number;
  @Input() bookingStatus = 0;

  readonly steps = STEPS;

  private readonly api = inject(PortalApiService);
  private readonly realtime = inject(PortalTrackingService);
  private readonly destroyRef = inject(DestroyRef);

  readonly tracking = signal<PortalBookingTrackingDto | null>(null);

  readonly driverMapPoint = computed<TripMapPoint | null>(() => {
    const t = this.tracking();
    if (!t?.driverLatitude || !t?.driverLongitude) return null;
    return { lat: t.driverLatitude, lng: t.driverLongitude, label: 'Driver' };
  });

  readonly pickupMapPoint = computed<TripMapPoint | null>(() => {
    const t = this.tracking();
    if (!t?.pickupLatitude || !t?.pickupLongitude) return null;
    return { lat: t.pickupLatitude, lng: t.pickupLongitude, label: 'Pickup' };
  });

  ngOnInit(): void {
    const active = this.bookingStatus === 2 || this.bookingStatus === 3;
    if (!active) return;

    this.refreshTracking();

    interval(30_000)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        switchMap(() => this.api.getBookingTracking(this.bookingId))
      )
      .subscribe((t) => this.applyTracking(t));
  }

  private refreshTracking(): void {
    this.api
      .getBookingTracking(this.bookingId)
      .pipe(take(1))
      .subscribe((t) => this.applyTracking(t));
  }

  private applyTracking(t: PortalBookingTrackingDto): void {
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
}
