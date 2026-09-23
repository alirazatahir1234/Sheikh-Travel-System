import {
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  ViewChild
} from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MapDirectionsService } from '@angular/google-maps';
import { forkJoin, merge, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs/operators';
import { TripService } from '../../../core/services/trip.service';
import { CustomerService } from '../../../core/services/customer.service';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { RouteService } from '../../../core/services/route.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import { Customer } from '../../../core/models/customer.model';
import { Route } from '../../../core/models/route.model';
import {
  CreateTripDto,
  TRIP_PRIORITIES,
  TRIP_TYPES,
  TripPriority,
  TripType
} from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-form',
  templateUrl: './trip-form.component.html',
  styleUrls: ['./trip-form.component.scss']
})
export class TripFormComponent implements OnInit, OnDestroy {
  @ViewChild('pickupInput') pickupInput?: ElementRef<HTMLInputElement>;
  @ViewChild('destinationInput') destinationInput?: ElementRef<HTMLInputElement>;

  form!: FormGroup;
  loading = false;
  saving = false;
  isEdit = false;
  tripId: number | null = null;
  mapsConfigured = false;
  routingBusy = false;
  routePrefillActive = false;

  customers: Customer[] = [];
  routes: Route[] = [];
  drivers: Array<{ id: number; fullName: string }> = [];
  vehicles: Array<{ id: number; name: string }> = [];

  readonly tripTypes = TRIP_TYPES;
  readonly priorities = TRIP_PRIORITIES;

  private arrivalManuallyEdited = false;
  private metricsManuallyEdited = false;
  private applyingRoute = false;
  private patchingComputed = false;
  private subs = new Subscription();
  private directionsSub?: Subscription;
  private pickupAutocomplete: google.maps.places.Autocomplete | null = null;
  private destinationAutocomplete: google.maps.places.Autocomplete | null = null;
  private placesListeners: google.maps.MapsEventListener[] = [];

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private trips: TripService,
    private customersApi: CustomerService,
    private driversApi: DriverService,
    private vehiclesApi: VehicleService,
    private routesApi: RouteService,
    private mapsLoader: GoogleMapsLoaderService,
    private directionsService: MapDirectionsService,
    private toast: UiToastService,
    private zone: NgZone
  ) {
    this.mapsConfigured = this.mapsLoader.isConfigured;
  }

  get stops(): FormArray {
    return this.form.get('stops') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      tripName: ['', Validators.required],
      tripType: ['Transfer' as TripType, Validators.required],
      customerId: [null, Validators.required],
      routeId: [null],
      passengerCount: [1, [Validators.required, Validators.min(1)]],
      priority: ['Normal' as TripPriority, Validators.required],
      pickupAddress: [''],
      pickupLatitude: [{ value: null, disabled: true }],
      pickupLongitude: [{ value: null, disabled: true }],
      destinationAddress: [''],
      destinationLatitude: [{ value: null, disabled: true }],
      destinationLongitude: [{ value: null, disabled: true }],
      tripDate: ['', Validators.required],
      pickupTime: ['', Validators.required],
      arrivalTime: [''],
      estimatedDurationMinutes: [null],
      plannedDistanceKm: [null],
      driverNotes: [''],
      driverId: [null],
      assistantDriverId: [null],
      vehicleId: [null],
      stops: this.fb.array([])
    });

    this.wireReactiveBehavior();

    this.loading = true;
    forkJoin({
      customers: this.customersApi.getAll(1, 500),
      routes: this.routesApi.getAll(1, 500),
      drivers: this.driversApi.getAll(1, 500),
      vehicles: this.vehiclesApi.getAll(1, 500)
    }).subscribe({
      next: res => {
        this.customers = res.customers.items;
        this.routes = res.routes.items;
        this.drivers = res.drivers.items.map(d => ({ id: d.id, fullName: d.fullName }));
        this.vehicles = res.vehicles.items.map(v => ({ id: v.id, name: v.name }));
        this.loading = false;

        const idParam = this.route.snapshot.paramMap.get('id');
        const isEditRoute = this.router.url.includes('/edit');
        if (idParam && isEditRoute) {
          this.isEdit = true;
          this.tripId = +idParam;
          this.loadTrip(+idParam);
        } else {
          queueMicrotask(() => this.initPlacesAutocomplete());
        }
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load form data.'));
      }
    });
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.directionsSub?.unsubscribe();
    this.placesListeners.forEach(l => l.remove());
    this.placesListeners = [];
  }

  private wireReactiveBehavior(): void {
    // Auto-calc Expected Arrival from Trip Date + Pickup Time + Duration
    this.subs.add(
      merge(
        this.form.get('tripDate')!.valueChanges,
        this.form.get('pickupTime')!.valueChanges,
        this.form.get('estimatedDurationMinutes')!.valueChanges
      )
        .pipe(debounceTime(150))
        .subscribe(() => this.recomputeArrivalTime())
    );

    // Track manual arrival override
    this.subs.add(
      this.form.get('arrivalTime')!.valueChanges.subscribe(() => {
        if (!this.patchingComputed) {
          this.arrivalManuallyEdited = true;
        }
      })
    );

    // Track manual duration/distance override
    this.subs.add(
      merge(
        this.form.get('estimatedDurationMinutes')!.valueChanges,
        this.form.get('plannedDistanceKm')!.valueChanges
      ).subscribe(() => {
        if (!this.patchingComputed && !this.applyingRoute) {
          this.metricsManuallyEdited = true;
        }
      })
    );

    // Route dropdown → prefill addresses / metrics
    this.subs.add(
      this.form.get('routeId')!.valueChanges.subscribe(routeId => {
        if (this.applyingRoute) return;
        this.applySelectedRoute(routeId);
      })
    );

    // Manual address edits clear Route selection
    this.subs.add(
      merge(
        this.form.get('pickupAddress')!.valueChanges,
        this.form.get('destinationAddress')!.valueChanges
      )
        .pipe(debounceTime(200))
        .subscribe(() => {
          if (this.applyingRoute || this.patchingComputed) return;
          if (this.form.get('routeId')?.value != null) {
            this.applyingRoute = true;
            this.form.patchValue({ routeId: null }, { emitEvent: false });
            this.applyingRoute = false;
            this.routePrefillActive = false;
          }
        })
    );

    // When coords change, request driving route metrics
    this.subs.add(
      merge(
        this.form.get('pickupLatitude')!.valueChanges,
        this.form.get('pickupLongitude')!.valueChanges,
        this.form.get('destinationLatitude')!.valueChanges,
        this.form.get('destinationLongitude')!.valueChanges
      )
        .pipe(
          debounceTime(400),
          map(() => this.currentCoords()),
          filter((c): c is NonNullable<ReturnType<TripFormComponent['currentCoords']>> => !!c),
          distinctUntilChanged(
            (a, b) =>
              a.pickup.lat === b.pickup.lat &&
              a.pickup.lng === b.pickup.lng &&
              a.dest.lat === b.dest.lat &&
              a.dest.lng === b.dest.lng
          )
        )
        .subscribe(coords => this.computeDrivingRoute(coords))
    );
  }

  private loadTrip(id: number): void {
    this.trips.getById(id).subscribe({
      next: trip => {
        this.patchingComputed = true;
        this.arrivalManuallyEdited = true;
        this.metricsManuallyEdited = true;
        this.form.patchValue({
          tripName: trip.tripName,
          tripType: trip.tripType,
          customerId: trip.customerId,
          routeId: trip.routeId,
          passengerCount: trip.passengerCount,
          priority: trip.priority,
          pickupAddress: trip.pickupAddress,
          pickupLatitude: trip.pickupLatitude,
          pickupLongitude: trip.pickupLongitude,
          destinationAddress: trip.destinationAddress,
          destinationLatitude: trip.destinationLatitude,
          destinationLongitude: trip.destinationLongitude,
          tripDate: trip.tripDate?.substring(0, 10),
          pickupTime: this.toTimeInput(trip.plannedStart),
          arrivalTime: trip.plannedEnd ? this.toTimeInput(trip.plannedEnd) : '',
          estimatedDurationMinutes: trip.estimatedDurationMinutes,
          plannedDistanceKm: trip.plannedDistanceKm,
          driverNotes: trip.driverNotes,
          driverId: trip.driverId,
          assistantDriverId: trip.assistantDriverId,
          vehicleId: trip.vehicleId
        });
        this.routePrefillActive = !!trip.routeId;
        this.stops.clear();
        for (const s of trip.stops || []) {
          this.stops.push(this.fb.group({
            sequence: [s.sequence],
            location: [s.location, Validators.required],
            latitude: [s.latitude],
            longitude: [s.longitude],
            eta: [s.eta ? this.toLocalInput(s.eta) : '']
          }));
        }
        this.patchingComputed = false;
        queueMicrotask(() => this.initPlacesAutocomplete());
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load trip.'))
    });
  }

  addStop(): void {
    this.stops.push(this.fb.group({
      sequence: [this.stops.length + 1],
      location: ['', Validators.required],
      latitude: [null],
      longitude: [null],
      eta: ['']
    }));
  }

  removeStop(index: number): void {
    this.stops.removeAt(index);
    this.stops.controls.forEach((c, i) => c.patchValue({ sequence: i + 1 }));
  }

  onArrivalManualEdit(): void {
    this.arrivalManuallyEdited = true;
  }

  onMetricsManualEdit(): void {
    this.metricsManuallyEdited = true;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const plannedStart = this.combineDateAndTime(v.tripDate, v.pickupTime);
    if (!plannedStart) {
      this.toast.error('Trip date and pickup time are required.');
      return;
    }

    let plannedEnd: Date | null = null;
    if (v.arrivalTime) {
      plannedEnd = this.combineDateAndTime(v.tripDate, v.arrivalTime);
      if (plannedEnd && plannedEnd.getTime() <= plannedStart.getTime()) {
        plannedEnd = new Date(plannedEnd.getTime() + 24 * 60 * 60 * 1000);
      }
    } else if (v.estimatedDurationMinutes != null && v.estimatedDurationMinutes > 0) {
      plannedEnd = new Date(plannedStart.getTime() + Number(v.estimatedDurationMinutes) * 60_000);
    }

    const payload: CreateTripDto = {
      tripName: v.tripName,
      tripType: v.tripType,
      customerId: +v.customerId,
      routeId: v.routeId ? +v.routeId : null,
      passengerCount: +v.passengerCount,
      priority: v.priority,
      pickupAddress: v.pickupAddress || null,
      pickupLatitude: v.pickupLatitude,
      pickupLongitude: v.pickupLongitude,
      destinationAddress: v.destinationAddress || null,
      destinationLatitude: v.destinationLatitude,
      destinationLongitude: v.destinationLongitude,
      tripDate: new Date(`${v.tripDate}T00:00:00`).toISOString(),
      plannedStart: plannedStart.toISOString(),
      plannedEnd: plannedEnd ? plannedEnd.toISOString() : null,
      estimatedDurationMinutes: v.estimatedDurationMinutes,
      plannedDistanceKm: v.plannedDistanceKm,
      driverNotes: v.driverNotes || null,
      driverId: v.driverId ? +v.driverId : null,
      assistantDriverId: v.assistantDriverId ? +v.assistantDriverId : null,
      vehicleId: v.vehicleId ? +v.vehicleId : null,
      stops: (v.stops || []).map((s: { sequence: number; location: string; latitude?: number; longitude?: number; eta?: string }) => ({
        sequence: s.sequence,
        location: s.location,
        latitude: s.latitude,
        longitude: s.longitude,
        eta: s.eta ? new Date(s.eta).toISOString() : null
      }))
    };

    this.saving = true;
    if (this.isEdit && this.tripId) {
      const { driverId, assistantDriverId, vehicleId, bookingId, ...update } = payload as CreateTripDto & { bookingId?: number };
      this.trips.update(this.tripId, update).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Trip updated.');
          this.router.navigate(['/trips', this.tripId]);
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Update failed.'));
        }
      });
      return;
    }

    this.trips.create(payload).subscribe({
      next: id => {
        this.saving = false;
        this.toast.success('Trip created.');
        this.router.navigate(['/trips', id]);
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Create failed.'));
      }
    });
  }

  private applySelectedRoute(routeId: number | null): void {
    if (routeId == null) {
      this.routePrefillActive = false;
      return;
    }

    const selected = this.routes.find(r => r.id === +routeId);
    if (!selected) return;

    this.applyingRoute = true;
    this.routePrefillActive = true;
    this.arrivalManuallyEdited = false;
    this.metricsManuallyEdited = false;

    this.form.patchValue({
      pickupAddress: selected.source || '',
      destinationAddress: selected.destination || '',
      plannedDistanceKm: selected.distance ?? null,
      estimatedDurationMinutes: selected.estimatedMinutes ?? null
    });

    this.applyingRoute = false;
    this.recomputeArrivalTime();

    // Resolve lat/lng (+ refine distance/duration) via Directions
    void this.geocodeAddressesAndRoute(selected.source, selected.destination);
  }

  private recomputeArrivalTime(): void {
    if (this.arrivalManuallyEdited) return;

    const tripDate = this.form.get('tripDate')?.value;
    const pickupTime = this.form.get('pickupTime')?.value;
    const duration = Number(this.form.get('estimatedDurationMinutes')?.value);

    if (!tripDate || !pickupTime || !Number.isFinite(duration) || duration <= 0) return;

    const start = this.combineDateAndTime(tripDate, pickupTime);
    if (!start) return;

    const end = new Date(start.getTime() + duration * 60_000);
    this.patchingComputed = true;
    this.form.patchValue({ arrivalTime: this.formatTime(end) }, { emitEvent: false });
    this.patchingComputed = false;
  }

  private currentCoords(): {
    pickup: google.maps.LatLngLiteral;
    dest: google.maps.LatLngLiteral;
  } | null {
    const raw = this.form.getRawValue();
    const plat = Number(raw.pickupLatitude);
    const plng = Number(raw.pickupLongitude);
    const dlat = Number(raw.destinationLatitude);
    const dlng = Number(raw.destinationLongitude);
    if (![plat, plng, dlat, dlng].every(Number.isFinite)) return null;
    return {
      pickup: { lat: plat, lng: plng },
      dest: { lat: dlat, lng: dlng }
    };
  }

  private computeDrivingRoute(coords: {
    pickup: google.maps.LatLngLiteral;
    dest: google.maps.LatLngLiteral;
  }): void {
    if (!this.mapsConfigured) return;

    void this.runDirections(coords.pickup, coords.dest);
  }

  private async geocodeAddressesAndRoute(origin: string, destination: string): Promise<void> {
    if (!this.mapsConfigured || !origin?.trim() || !destination?.trim()) return;
    await this.runDirections(origin.trim(), destination.trim());
  }

  private async runDirections(
    origin: string | google.maps.LatLngLiteral,
    destination: string | google.maps.LatLngLiteral
  ): Promise<void> {
    try {
      await this.mapsLoader.load();
      await this.mapsLoader.importLibrary('maps');
    } catch {
      return;
    }

    this.routingBusy = true;
    this.directionsSub?.unsubscribe();

    const request: google.maps.DirectionsRequest = {
      origin,
      destination,
      travelMode: google.maps.TravelMode.DRIVING,
      region: 'PK',
      drivingOptions: {
        departureTime: new Date(),
        trafficModel: google.maps.TrafficModel.BEST_GUESS
      }
    };

    this.directionsSub = this.directionsService.route(request).subscribe({
      next: ({ status, result }) => {
        this.routingBusy = false;
        if (status !== 'OK' || !result?.routes?.length) return;

        const route = result.routes[0];
        const legs = route.legs || [];
        let totalMeters = 0;
        let totalSeconds = 0;
        let trafficSeconds = 0;
        let hasTraffic = false;

        legs.forEach(leg => {
          totalMeters += leg.distance?.value ?? 0;
          totalSeconds += leg.duration?.value ?? 0;
          if (leg.duration_in_traffic?.value != null) {
            hasTraffic = true;
            trafficSeconds += leg.duration_in_traffic.value;
          } else {
            trafficSeconds += leg.duration?.value ?? 0;
          }
        });

        const first = legs[0];
        const last = legs[legs.length - 1];
        const km = Math.round(totalMeters / 100) / 10;
        const minutes = Math.max(1, Math.round((hasTraffic ? trafficSeconds : totalSeconds) / 60));

        this.patchingComputed = true;
        this.applyingRoute = true;
        const patch: Record<string, unknown> = {};

        if (first?.start_location) {
          patch['pickupLatitude'] = roundCoord(first.start_location.lat());
          patch['pickupLongitude'] = roundCoord(first.start_location.lng());
          if (!this.form.get('pickupAddress')?.value) {
            patch['pickupAddress'] = first.start_address || '';
          }
        }
        if (last?.end_location) {
          patch['destinationLatitude'] = roundCoord(last.end_location.lat());
          patch['destinationLongitude'] = roundCoord(last.end_location.lng());
          if (!this.form.get('destinationAddress')?.value) {
            patch['destinationAddress'] = last.end_address || '';
          }
        }

        if (!this.metricsManuallyEdited) {
          patch['plannedDistanceKm'] = km;
          patch['estimatedDurationMinutes'] = minutes;
        }

        this.form.patchValue(patch, { emitEvent: false });
        this.applyingRoute = false;
        this.patchingComputed = false;
        this.recomputeArrivalTime();
      },
      error: () => {
        this.routingBusy = false;
      }
    });
  }

  private async initPlacesAutocomplete(): Promise<void> {
    if (!this.mapsConfigured) return;
    if (!this.pickupInput?.nativeElement || !this.destinationInput?.nativeElement) {
      setTimeout(() => void this.initPlacesAutocomplete(), 50);
      return;
    }

    try {
      const placesLib = await this.mapsLoader.importLibrary<typeof google.maps.places>('places');
      if (!placesLib?.Autocomplete) return;

      this.placesListeners.forEach(l => l.remove());
      this.placesListeners = [];

      const options: google.maps.places.AutocompleteOptions = {
        fields: ['formatted_address', 'name', 'geometry'],
        types: ['geocode'],
        componentRestrictions: { country: 'pk' }
      };

      this.pickupAutocomplete = new placesLib.Autocomplete(this.pickupInput.nativeElement, options);
      this.destinationAutocomplete = new placesLib.Autocomplete(
        this.destinationInput.nativeElement,
        options
      );

      this.placesListeners.push(
        this.pickupAutocomplete.addListener('place_changed', () =>
          this.onPlaceSelected('pickup', this.pickupAutocomplete!)
        )
      );
      this.placesListeners.push(
        this.destinationAutocomplete.addListener('place_changed', () =>
          this.onPlaceSelected('destination', this.destinationAutocomplete!)
        )
      );
    } catch (err) {
      console.warn('Trip form Places autocomplete init failed:', err);
    }
  }

  private onPlaceSelected(
    field: 'pickup' | 'destination',
    ac: google.maps.places.Autocomplete
  ): void {
    const place = ac.getPlace();
    const label = place?.formatted_address || place?.name || '';
    const loc = place?.geometry?.location;

    this.zone.run(() => {
      this.applyingRoute = true;
      if (field === 'pickup') {
        this.form.patchValue({
          pickupAddress: label,
          pickupLatitude: loc ? roundCoord(loc.lat()) : null,
          pickupLongitude: loc ? roundCoord(loc.lng()) : null,
          routeId: null
        });
      } else {
        this.form.patchValue({
          destinationAddress: label,
          destinationLatitude: loc ? roundCoord(loc.lat()) : null,
          destinationLongitude: loc ? roundCoord(loc.lng()) : null,
          routeId: null
        });
      }
      this.routePrefillActive = false;
      this.applyingRoute = false;
      this.metricsManuallyEdited = false;

      const coords = this.currentCoords();
      if (coords) {
        this.computeDrivingRoute(coords);
      }
    });
  }

  private combineDateAndTime(dateStr: string, timeStr: string): Date | null {
    if (!dateStr || !timeStr) return null;
    const d = new Date(`${dateStr}T${timeStr}:00`);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  private toTimeInput(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '';
    return this.formatTime(d);
  }

  private formatTime(d: Date): string {
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  private toLocalInput(iso: string): string {
    const d = new Date(iso);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}

function roundCoord(n: number): number {
  return Math.round(n * 1_000_000) / 1_000_000;
}
