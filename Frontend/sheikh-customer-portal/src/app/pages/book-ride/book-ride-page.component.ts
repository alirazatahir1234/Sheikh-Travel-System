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
import { catchError, finalize } from 'rxjs/operators';
import {
  PortalPaymentPlan,
  PortalRouteDto,
  PortalVehicleDto,
  PortalBookingCreatedDto,
  PriceBreakdown
} from '../../core/models/portal.models';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { portalRouteOptionLabel, portalVehicleOptionLabel } from '../../core/utils/portal-display.util';
import { RoutePreviewMapComponent } from '../../shared/route-preview-map/route-preview-map.component';

/** `datetime-local` min/max use local time without seconds (HTML spec). */
function toDatetimeLocalString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Blocks times more than ~1 minute in the past (covers minute-level input vs clock skew). */
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
  imports: [CommonModule, ReactiveFormsModule, RouterLink, RoutePreviewMapComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './book-ride-page.component.html'
})
export class BookRidePageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly portal = inject(PortalApiService);
  private readonly session = inject(CustomerSessionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly step = signal(0);
  readonly routes = signal<PortalRouteDto[]>([]);
  readonly vehicles = signal<PortalVehicleDto[]>([]);
  readonly routesError = signal<string | null>(null);
  readonly vehiclesError = signal<string | null>(null);
  readonly catalogLoading = signal(true);
  readonly estimateBusy = signal(false);
  readonly submitBusy = signal(false);
  readonly apiError = signal<string | null>(null);
  readonly estimate = signal<PriceBreakdown | null>(null);
  readonly confirmation = signal<PortalBookingCreatedDto | null>(null);

  readonly stepLabel = computed(() => ['Trip', 'Passengers & contact', 'Payment'][this.step()] ?? '');

  /** Earliest selectable pickup in the native datetime picker (current local date & time). */
  readonly pickupDatetimeMin = toDatetimeLocalString(new Date());

  readonly form = this.fb.group({
    routeId: [null as number | null, Validators.required],
    vehicleId: [null as number | null, Validators.required],
    isRoundTrip: [false],
    pickupLocal: ['', [Validators.required, pickupNotInPastValidator]],
    passengerCount: [1, [Validators.required, Validators.min(1), Validators.max(60)]],
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(20)]],
    email: ['', Validators.maxLength(200)],
    notes: ['', Validators.maxLength(900)],
    paymentPlan: this.fb.control<PortalPaymentPlan>('payLater', { nonNullable: true }),
    partialAmount: [null as number | null]
  });

  constructor() {
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
      )
    })
      .pipe(
        finalize(() => this.catalogLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(({ routes, vehicles }) => {
        this.routes.set(routes);
        this.vehicles.set(vehicles);
        this.applyDefaultSelections(routes, vehicles);
      });
  }

  private applyDefaultSelections(routes: PortalRouteDto[], vehicles: PortalVehicleDto[]): void {
    const curR = this.form.value.routeId;
    const curV = this.form.value.vehicleId;
    const patch: { routeId?: number | null; vehicleId?: number | null } = {};
    if (routes.length > 0 && (curR == null || !routes.some((r) => r.id === curR))) {
      patch.routeId = routes[0].id;
    }
    if (vehicles.length > 0 && (curV == null || !vehicles.some((v) => v.id === curV))) {
      patch.vehicleId = vehicles[0].id;
    }
    if (Object.keys(patch).length > 0) {
      this.form.patchValue(patch);
    }
  }

  private catalogLoadMessage(kind: 'routes' | 'vehicles', err: unknown): string {
    const base =
      kind === 'routes'
        ? 'Could not load routes.'
        : 'Could not load vehicles.';
    const startApiHint =
      'Start the API from the repo: `dotnet run --project Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj --launch-profile http` (listens on http://127.0.0.1:5082). Then run the customer portal with `ng serve` so `proxy.conf.json` can forward `/api`.';
    if (err instanceof HttpErrorResponse) {
      const raw =
        typeof err.error === 'string'
          ? err.error
          : err.error != null && typeof err.error === 'object' && 'message' in err.error
            ? String((err.error as { message?: unknown }).message ?? '')
            : '';
      const proxyOrNetwork =
        err.status === 0 ||
        err.status === 502 ||
        err.status === 503 ||
        err.status === 504 ||
        /ECONNREFUSED|ECONNRESET|proxy|socket hang up/i.test(err.message) ||
        /ECONNREFUSED|proxy error/i.test(raw);
      if (proxyOrNetwork) {
        return `${base} ${startApiHint}`;
      }
      if (err.status === 404) {
        return `${base} No /api/customer-portal/${kind} route on this server — rebuild and deploy the latest SheikhTravelSystem.API. ${startApiHint}`;
      }
      if (err.status === 500 && (!raw || raw === 'Internal Server Error')) {
        return `${base} Server error or dev proxy could not reach the API. ${startApiHint}`;
      }
      const body = err.error as { message?: string } | undefined;
      if (body?.message) return `${base} ${body.message}`;
    }
    return base;
  }

  selectedVehicle(): PortalVehicleDto | undefined {
    const id = this.form.value.vehicleId;
    if (id == null) return undefined;
    return this.vehicles().find((v) => v.id === id);
  }

  selectedRoute(): PortalRouteDto | undefined {
    const id = this.form.value.routeId;
    if (id == null) return undefined;
    return this.routes().find((r) => r.id === id);
  }

  /** Dropdown label — same fields as admin route list. */
  routeOptionLabel(r: PortalRouteDto): string {
    return portalRouteOptionLabel(r);
  }

  /** Dropdown label — same fields as admin vehicle list. */
  vehicleOptionLabel(v: PortalVehicleDto): string {
    return portalVehicleOptionLabel(v);
  }

  maxPassengers(): number {
    return this.selectedVehicle()?.seatingCapacity ?? 60;
  }

  canEstimate(): boolean {
    const v = this.form.value;
    const pickup = this.form.controls.pickupLocal;
    return !!(v.routeId && v.vehicleId && v.pickupLocal && !pickup.invalid && !this.estimateBusy());
  }

  getEstimate(): void {
    this.apiError.set(null);
    this.estimate.set(null);
    const routeId = this.form.value.routeId!;
    const vehicleId = this.form.value.vehicleId!;
    const isRoundTrip = !!this.form.value.isRoundTrip;
    this.estimateBusy.set(true);
    this.portal
      .estimatePrice(routeId, vehicleId, isRoundTrip)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (b) => {
          this.estimate.set(b);
          this.estimateBusy.set(false);
        },
        error: (e) => {
          this.apiError.set(this.formatError(e));
          this.estimateBusy.set(false);
        }
      });
  }

  nextFromStep0(): void {
    this.form.controls.routeId.markAsTouched();
    this.form.controls.vehicleId.markAsTouched();
    this.form.controls.pickupLocal.markAsTouched();
    if (this.form.controls.routeId.invalid || this.form.controls.vehicleId.invalid || this.form.controls.pickupLocal.invalid) {
      return;
    }
    if (!this.estimate()) {
      this.apiError.set('Please get a price estimate before continuing.');
      return;
    }
    this.apiError.set(null);
    this.step.set(1);
  }

  nextFromStep1(): void {
    ['fullName', 'phone', 'passengerCount'].forEach((k) => {
      this.form.get(k)?.markAsTouched();
    });
    const cap = this.maxPassengers();
    const pax = Number(this.form.value.passengerCount);
    if (pax > cap) {
      this.apiError.set(`This vehicle seats up to ${cap} passengers.`);
      return;
    }
    if (this.form.controls.fullName.invalid || this.form.controls.phone.invalid || this.form.controls.passengerCount.invalid) {
      return;
    }
    this.apiError.set(null);
    this.step.set(2);
  }

  back(): void {
    this.step.update((s) => Math.max(0, s - 1));
  }

  submit(): void {
    this.apiError.set(null);
    const est = this.estimate();
    if (!est || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const plan = this.form.controls.paymentPlan.value;
    let initial: number | null = null;
    if (plan === 'partial') {
      initial = Number(this.form.value.partialAmount);
      if (!(initial > 0) || initial >= est.totalAmount) {
        this.apiError.set('Enter a partial amount greater than zero and less than the trip total.');
        return;
      }
    } else {
      this.form.patchValue({ partialAmount: null });
    }

    const pickupIso = new Date(this.form.value.pickupLocal!).toISOString();
    this.submitBusy.set(true);
    this.portal
      .createBooking({
        fullName: this.form.value.fullName!.trim(),
        phone: this.form.value.phone!.trim(),
        email: this.form.value.email?.trim() || null,
        routeId: this.form.value.routeId!,
        vehicleId: this.form.value.vehicleId!,
        pickupTime: pickupIso,
        passengerCount: Number(this.form.value.passengerCount),
        isRoundTrip: !!this.form.value.isRoundTrip,
        notes: this.form.value.notes?.trim() || null,
        paymentPlan: plan,
        initialPaymentAmount: plan === 'partial' ? initial : null
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (c) => {
          this.confirmation.set(c);
          this.session.setSession(this.form.value.phone!.trim(), this.form.value.fullName!.trim());
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
      if (err.status === 404) {
        return 'API endpoint not found. Restart the backend and confirm /api/customer-portal is available.';
      }
      if (err.status === 0) {
        return 'Could not reach the API. Check that the backend is running and the dev proxy targets the correct URL.';
      }
      const body = err.error as { message?: string } | undefined;
      if (body?.message) return body.message;
      return err.message || 'Request failed.';
    }
    return 'Something went wrong.';
  }
}
