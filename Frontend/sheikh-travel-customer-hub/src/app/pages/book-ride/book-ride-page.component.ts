import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
  computed
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, debounceTime, finalize, map, switchMap } from 'rxjs/operators';
import {
  PortalPaymentPlan,
  PortalPaymentGatewayInfoDto,
  PortalRouteDto,
  PortalSavedAddressDto,
  PortalSeatLayoutDto,
  PortalVehicleDto,
  PortalBookingCreatedDto,
  PriceBreakdown
} from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { BookingStepperComponent, BookingStep } from '../../shared/booking-stepper/booking-stepper.component';
import { FareBreakdownCardComponent } from '../../shared/fare-breakdown-card/fare-breakdown-card.component';
import { RouteSummaryCardComponent } from '../../shared/route-summary-card/route-summary-card.component';
import { VehicleCardGridComponent } from '../../shared/vehicle-card-grid/vehicle-card-grid.component';
import { RoutePreviewMapComponent } from '../../shared/route-preview-map/route-preview-map.component';
import {
  LocationAutocompleteComponent,
  LocationValue
} from '../../shared/location-autocomplete/location-autocomplete.component';
import { TripMapComponent } from '../../shared/trip-map/trip-map.component';
import { CounterStepperComponent } from '../../shared/counter-stepper/counter-stepper.component';
import { SeatMapComponent } from '../../shared/seat-map/seat-map.component';
import { PhoneDigitsOnlyDirective } from '../../shared/directives/phone-digits-only.directive';

function toDatetimeLocalString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function pickupNotInPastValidator(control: AbstractControl): ValidationErrors | null {
  const raw = control.value;
  if (!raw || typeof raw !== 'string') return null;
  const ms = new Date(raw).getTime();
  if (Number.isNaN(ms)) return null;
  if (ms < Date.now() - 60_000) return { pickupPast: true };
  return null;
}

@Component({
  selector: 'app-book-ride-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    BookingStepperComponent,
    FareBreakdownCardComponent,
    RouteSummaryCardComponent,
    VehicleCardGridComponent,
    RoutePreviewMapComponent,
    LocationAutocompleteComponent,
    TripMapComponent,
    CounterStepperComponent,
    SeatMapComponent,
    PhoneDigitsOnlyDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './book-ride-page.component.html'
})
export class BookRidePageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly portal = inject(PortalApiService);
  readonly session = inject(CustomerSessionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly step = signal(0);
  readonly useCustomRoute = signal(false);
  readonly routes = signal<PortalRouteDto[]>([]);
  readonly vehicles = signal<PortalVehicleDto[]>([]);
  readonly savedAddresses = signal<PortalSavedAddressDto[]>([]);
  readonly routesError = signal<string | null>(null);
  readonly vehiclesError = signal<string | null>(null);
  readonly catalogLoading = signal(true);
  readonly estimateBusy = signal(false);
  readonly submitBusy = signal(false);
  readonly apiError = signal<string | null>(null);
  readonly routeStale = signal(false);
  readonly estimate = signal<PriceBreakdown | null>(null);
  readonly quoteDistanceKm = signal<number | null>(null);
  readonly quoteDurationMin = signal<number | null>(null);
  readonly confirmation = signal<PortalBookingCreatedDto | null>(null);
  readonly promoDiscount = signal(0);
  readonly promoMessage = signal<string | null>(null);
  readonly seatLayout = signal<PortalSeatLayoutDto[]>([]);
  readonly selectedSeats = signal<string[]>([]);
  readonly gatewayInfo = signal<PortalPaymentGatewayInfoDto | null>(null);

  readonly pickupDatetimeMin = toDatetimeLocalString(new Date());

  readonly paymentMethods = [
    { value: 'Cash', label: 'Cash' },
    { value: 'BankTransfer', label: 'Bank transfer' },
    { value: 'Easypaisa', label: 'Easypaisa' },
    { value: 'JazzCash', label: 'JazzCash' },
    { value: 'Card', label: 'Card (online)' }
  ] as const;

  readonly form = this.fb.group({
    routeId: [null as number | null],
    vehicleId: [null as number | null, Validators.required],
    pickupLocation: [null as LocationValue | null],
    dropoffLocation: [null as LocationValue | null],
    isRoundTrip: [false],
    pickupLocal: ['', [Validators.required, pickupNotInPastValidator]],
    adultCount: [1, [Validators.required, Validators.min(1), Validators.max(60)]],
    childCount: [0, [Validators.min(0), Validators.max(20)]],
    luggageCount: [0, [Validators.min(0), Validators.max(20)]],
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(20)]],
    email: ['', Validators.maxLength(200)],
    notes: ['', Validators.maxLength(900)],
    promoCode: ['', Validators.maxLength(32)],
    preferredPaymentMethod: ['Cash', Validators.required],
    paymentPlan: this.fb.control<PortalPaymentPlan>('payLater', { nonNullable: true }),
    partialAmount: [null as number | null]
  });

  readonly passengerCount = computed(
    () => Number(this.form.value.adultCount ?? 1) + Number(this.form.value.childCount ?? 0)
  );

  readonly stepperSteps = computed((): BookingStep[] => [
    { label: 'Route & schedule', complete: !!this.estimate() },
    { label: 'Passengers & contact', complete: this.step() > 1 },
    { label: 'Payment & confirm', complete: !!this.confirmation() }
  ]);

  readonly scheduleHints = computed(() => {
    const pickup = this.form.value.pickupLocal;
    if (!pickup) return null;
    const start = new Date(pickup);
    if (Number.isNaN(start.getTime())) return null;
    const mins =
      this.quoteDurationMin() ??
      this.selectedRoute()?.estimatedDurationMinutes ??
      (this.selectedRoute() ? Math.ceil((this.selectedRoute()!.distanceKm / 70) * 60) : 60);
    const arrival = new Date(start.getTime() + mins * 60_000);
    const cap = this.selectedVehicle()?.seatingCapacity;
    const seatsLeft = cap != null ? Math.max(0, cap - this.passengerCount()) : null;
    return { departure: start, arrival, seatsLeft, durationMin: mins };
  });

  constructor() {
    this.setBookingMode(false);

    const name = this.session.fullName();
    const phone = this.session.phone();
    if (name) this.form.patchValue({ fullName: name });
    if (phone) this.form.patchValue({ phone });

    forkJoin({
      routes: this.portal.getRoutes().pipe(
        catchError((e) => {
          this.routesError.set(this.catalogLoadMessage('routes', e));
          return of([] as PortalRouteDto[]);
        })
      ),
      vehicles: this.portal.getVehicles().pipe(
        catchError((e) => {
          this.vehiclesError.set(this.catalogLoadMessage('vehicles', e));
          return of([] as PortalVehicleDto[]);
        })
      ),
      gateway: this.portal.getPaymentGatewayInfo().pipe(catchError(() => of(null)))
    })
      .pipe(
        finalize(() => this.catalogLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(({ routes, vehicles, gateway }) => {
        this.routes.set(routes);
        this.vehicles.set(vehicles);
        if (gateway) this.gatewayInfo.set(gateway);
        this.applyDefaultSelections(routes, vehicles);
      });

    if (this.session.isAuthenticated()) {
      this.portal
        .getSavedAddresses()
        .pipe(
          catchError(() => of([] as PortalSavedAddressDto[])),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe((a) => this.savedAddresses.set(a));
    }

    this.form.valueChanges
      .pipe(debounceTime(450), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.triggerAutoEstimate());

    this.form.controls.vehicleId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadSeatsIfReady());
  }

  setBookingMode(custom: boolean): void {
    this.useCustomRoute.set(custom);
    this.estimate.set(null);
    this.quoteDistanceKm.set(null);
    this.quoteDurationMin.set(null);
    this.apiError.set(null);
    if (custom) {
      this.form.patchValue({ routeId: null });
      this.form.controls.pickupLocation.setValidators([Validators.required]);
      this.form.controls.dropoffLocation.setValidators([Validators.required]);
      this.form.controls.routeId.clearValidators();
    } else {
      this.form.patchValue({ pickupLocation: null, dropoffLocation: null });
      this.form.controls.routeId.setValidators([Validators.required]);
      this.form.controls.pickupLocation.clearValidators();
      this.form.controls.dropoffLocation.clearValidators();
    }
    this.form.controls.routeId.updateValueAndValidity();
    this.form.controls.pickupLocation.updateValueAndValidity();
    this.form.controls.dropoffLocation.updateValueAndValidity();
    this.triggerAutoEstimate();
  }

  applyPopularRoute(routeId: number): void {
    this.useCustomRoute.set(false);
    this.form.patchValue({ routeId });
    this.setBookingMode(false);
  }

  savePickupAsAddress(): void {
    const loc = this.form.value.pickupLocation;
    if (!loc) return;
    const label = prompt('Name this place (e.g. Home, Office)', 'Saved place');
    if (!label?.trim()) return;
    this.portal
      .saveAddress({
        label: label.trim(),
        addressLine: loc.address,
        latitude: loc.lat,
        longitude: loc.lng
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.portal.getSavedAddresses().subscribe((a) => this.savedAddresses.set(a));
        }
      });
  }

  applySavedAddress(addr: PortalSavedAddressDto, which: 'pickup' | 'dropoff'): void {
    const val: LocationValue = {
      address: addr.addressLine,
      lat: addr.latitude,
      lng: addr.longitude
    };
    if (which === 'pickup') this.form.patchValue({ pickupLocation: val });
    else this.form.patchValue({ dropoffLocation: val });
    this.useCustomRoute.set(true);
    this.setBookingMode(true);
  }

  reloadRoutes(): void {
    this.routesError.set(null);
    this.routeStale.set(false);
    this.catalogLoading.set(true);
    this.portal
      .getRoutes()
      .pipe(
        finalize(() => this.catalogLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (routes) => {
          this.routes.set(routes);
          this.form.patchValue({ routeId: routes[0]?.id ?? null });
        },
        error: (e) => this.routesError.set(this.catalogLoadMessage('routes', e))
      });
  }

  private applyDefaultSelections(routes: PortalRouteDto[], vehicles: PortalVehicleDto[]): void {
    const patch: { routeId?: number | null; vehicleId?: number | null } = {};
    if (!this.useCustomRoute() && routes.length > 0) patch.routeId = routes[0].id;
    if (vehicles.length > 0) patch.vehicleId = vehicles[0].id;
    if (Object.keys(patch).length) this.form.patchValue(patch);
  }

  private catalogLoadMessage(kind: 'routes' | 'vehicles', err: unknown): string {
    const base = kind === 'routes' ? 'Could not load routes.' : 'Could not load vehicles.';
    const startApi =
      'Start the API with: `dotnet run --project Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj --launch-profile http` (port 5082).';
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        return `${base} ${startApi} Ensure \`ng serve\` is using the dev proxy (requests should go to /api, not :7012).`;
      }
      const body = err.error as { message?: string } | undefined;
      if (body?.message) return `${base} ${body.message}`;
      if (err.status === 500) return `${base} Server error — restart the API after pulling latest changes. ${startApi}`;
    }
    return `${base} ${startApi}`;
  }

  selectedVehicle(): PortalVehicleDto | undefined {
    const id = this.form.value.vehicleId;
    return id == null ? undefined : this.vehicles().find((v) => v.id === id);
  }

  selectedRoute(): PortalRouteDto | undefined {
    const id = this.form.value.routeId;
    return id == null ? undefined : this.routes().find((r) => r.id === id);
  }

  maxPassengers(): number {
    return this.selectedVehicle()?.seatingCapacity ?? 60;
  }

  seatsRemaining(): number | null {
    const cap = this.selectedVehicle()?.seatingCapacity;
    return cap != null ? Math.max(0, cap - this.passengerCount()) : null;
  }

  canProceedStep0(): boolean {
    const pickup = this.form.controls.pickupLocal;
    if (pickup.invalid) return false;
    if (!this.form.value.vehicleId) return false;
    if (this.useCustomRoute()) {
      return !!(this.form.value.pickupLocation && this.form.value.dropoffLocation && this.estimate());
    }
    return !!(this.form.value.routeId && this.estimate());
  }

  private triggerAutoEstimate(): void {
    if (this.step() !== 0 || this.confirmation()) return;
    const v = this.form.getRawValue();
    if (!v.vehicleId || !v.pickupLocal || this.form.controls.pickupLocal.invalid) return;

    if (this.useCustomRoute()) {
      const p = v.pickupLocation;
      const d = v.dropoffLocation;
      if (!p || !d) return;
      this.runEstimate(() =>
        this.portal
          .quotePointToPoint({
            vehicleId: v.vehicleId!,
            pickupLat: p.lat,
            pickupLng: p.lng,
            dropLat: d.lat,
            dropLng: d.lng,
            isRoundTrip: !!v.isRoundTrip,
            routeId: v.routeId
          })
          .pipe(
            map((q) => ({
              priceBreakdown: q.priceBreakdown,
              distanceKm: q.distanceKm,
              durationMinutes: q.durationMinutes
            }))
          )
      );
      return;
    }

    if (!v.routeId) return;
    this.runEstimate(() =>
      this.portal.estimatePrice(v.routeId!, v.vehicleId!, !!v.isRoundTrip).pipe(
        switchMap((b) =>
          of({
            priceBreakdown: b,
            distanceKm: this.selectedRoute()?.distanceKm ?? 0,
            durationMinutes:
              this.selectedRoute()?.estimatedDurationMinutes ??
              Math.ceil(((this.selectedRoute()?.distanceKm ?? 0) / 70) * 60)
          })
        )
      )
    );
  }

  private runEstimate(
    factory: () => import('rxjs').Observable<{
      priceBreakdown: PriceBreakdown;
      distanceKm: number;
      durationMinutes: number;
    }>
  ): void {
    this.estimateBusy.set(true);
    this.apiError.set(null);
    this.routeStale.set(false);
    factory()
      .pipe(
        finalize(() => this.estimateBusy.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (q) => {
          this.estimate.set(q.priceBreakdown);
          this.quoteDistanceKm.set(q.distanceKm);
          this.quoteDurationMin.set(q.durationMinutes);
          void this.applyPromoIfAny();
        },
        error: (e) => {
          const msg = this.formatError(e);
          this.apiError.set(msg);
          if (/route.*not found|reload/i.test(msg)) this.routeStale.set(true);
        }
      });
  }

  applyPromo(): void {
    void this.applyPromoIfAny();
  }

  private async applyPromoIfAny(): Promise<void> {
    const code = this.form.value.promoCode?.trim();
    const est = this.estimate();
    if (!code || !est) {
      this.promoDiscount.set(0);
      this.promoMessage.set(null);
      return;
    }
    this.portal
      .validatePromo(code, est.totalAmount)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.promoDiscount.set(r.valid ? r.discountAmount : 0);
          this.promoMessage.set(r.message);
        },
        error: () => {
          this.promoDiscount.set(0);
          this.promoMessage.set('Could not validate promo code.');
        }
      });
  }

  private loadSeatsIfReady(): void {
    const vid = this.form.value.vehicleId;
    const pickup = this.form.value.pickupLocal;
    if (!vid || !pickup) {
      this.seatLayout.set([]);
      return;
    }
    const iso = new Date(pickup).toISOString();
    this.portal
      .getVehicleSeats(vid, iso)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => this.seatLayout.set(s),
        error: () => this.seatLayout.set([])
      });
  }

  onSeatsChange(labels: string[]): void {
    this.selectedSeats.set(labels);
    const total = labels.length;
    if (total > 0) {
      this.form.patchValue({ adultCount: Math.max(1, total), childCount: 0 });
    }
  }

  nextFromStep0(): void {
    this.form.controls.vehicleId.markAsTouched();
    this.form.controls.pickupLocal.markAsTouched();
    if (this.useCustomRoute()) {
      this.form.controls.pickupLocation.markAsTouched();
      this.form.controls.dropoffLocation.markAsTouched();
    } else {
      this.form.controls.routeId.markAsTouched();
    }
    if (!this.canProceedStep0()) {
      if (!this.estimate()) this.apiError.set('Waiting for fare estimate — adjust trip details if needed.');
      return;
    }
    this.apiError.set(null);
    this.loadSeatsIfReady();
    this.step.set(1);
  }

  nextFromStep1(): void {
    ['fullName', 'phone', 'adultCount'].forEach((k) => this.form.get(k)?.markAsTouched());
    const cap = this.maxPassengers();
    if (this.passengerCount() > cap) {
      this.apiError.set(`This vehicle seats up to ${cap} passengers.`);
      return;
    }
    if (this.form.controls.fullName.invalid || this.form.controls.phone.invalid) return;
    this.apiError.set(null);
    this.step.set(2);
  }

  back(): void {
    this.step.update((s) => Math.max(0, s - 1));
  }

  cardPaymentDisabled(): boolean {
    const g = this.gatewayInfo();
    return this.form.value.preferredPaymentMethod === 'Card' && g != null && !g.enabled;
  }

  submit(): void {
    this.apiError.set(null);
    const est = this.estimate();
    if (!est || this.form.invalid || this.cardPaymentDisabled()) {
      this.form.markAllAsTouched();
      if (this.cardPaymentDisabled()) {
        this.apiError.set(this.gatewayInfo()?.message ?? 'Card payments are not available yet.');
      }
      return;
    }
    const plan = this.form.controls.paymentPlan.value;
    const totalAfterDiscount = Math.max(0, est.totalAmount - this.promoDiscount());
    let initial: number | null = null;
    if (plan === 'partial') {
      initial = Number(this.form.value.partialAmount);
      if (!(initial > 0) || initial >= totalAfterDiscount) {
        this.apiError.set('Enter a partial amount greater than zero and less than the trip total.');
        return;
      }
    }

    const v = this.form.getRawValue();
    const phone = this.session.isAuthenticated() && this.session.phone()
      ? this.session.phone()!.trim()
      : v.phone!.trim();
    const pickupIso = new Date(v.pickupLocal!).toISOString();
    const p = v.pickupLocation;
    const d = v.dropoffLocation;

    this.submitBusy.set(true);
    this.portal
      .createBooking({
        fullName: v.fullName!.trim(),
        phone,
        email: v.email?.trim() || null,
        routeId: this.useCustomRoute() ? (v.routeId ?? null) : v.routeId!,
        vehicleId: v.vehicleId!,
        pickupTime: pickupIso,
        passengerCount: this.passengerCount(),
        isRoundTrip: !!v.isRoundTrip,
        notes: v.notes?.trim() || null,
        paymentPlan: plan,
        initialPaymentAmount: plan === 'partial' ? initial : null,
        preferredPaymentMethod: v.preferredPaymentMethod,
        pickupAddress: p?.address ?? this.selectedRoute()?.source ?? null,
        dropoffAddress: d?.address ?? this.selectedRoute()?.destination ?? null,
        pickupLat: p?.lat ?? null,
        pickupLng: p?.lng ?? null,
        dropLat: d?.lat ?? null,
        dropLng: d?.lng ?? null,
        quotedDistanceKm: this.quoteDistanceKm(),
        quotedDurationMinutes: this.quoteDurationMin(),
        adultCount: v.adultCount,
        childCount: v.childCount,
        luggageCount: v.luggageCount,
        promoCode: v.promoCode?.trim() || null,
        seatLabels: this.selectedSeats().length ? this.selectedSeats() : null
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (c) => {
          this.confirmation.set(c);
          if (!this.session.isAuthenticated()) {
            this.session.setLocalProfile(phone, v.fullName!.trim());
          }
          this.submitBusy.set(false);
        },
        error: (e) => {
          this.apiError.set(this.formatError(e));
          this.submitBusy.set(false);
        }
      });
  }

  private formatError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const body = err.error as { message?: string } | undefined;
      const msg = body?.message ?? err.message;
      if (/route.*not found/i.test(msg)) {
        this.routeStale.set(true);
        return 'That route is no longer available. Tap Reload routes and pick again.';
      }
      if (msg) return msg;
      return 'Request failed.';
    }
    return 'Something went wrong.';
  }
}
