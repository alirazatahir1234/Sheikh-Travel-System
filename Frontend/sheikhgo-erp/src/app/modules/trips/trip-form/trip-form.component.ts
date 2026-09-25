import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  QueryList,
  ViewChild,
  ViewChildren
} from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MapGeocoder } from '@angular/google-maps';
import { forkJoin, merge, Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
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
import { SHEIKHGO_DIRECTIONS_REGION } from '../../../core/utils/google-places-options';
import {
  attachSheikhGoPlacesAutocomplete,
  SheikhGoPlaceSelection,
  SheikhGoPlacesAutocompleteHandle
} from '../../../core/google-maps/place-autocomplete.new';
import {
  computeDrivingRoute,
  ComputedDrivingRoute,
  pathCenter
} from '../../../core/google-maps/gmap-routes';
import {
  canCalculateRoute,
  locationSelectionError,
  PLACE_SELECTION_WARNING,
  readCoordinate,
  routeMetricsForPayload
} from '../../../core/utils/trip-location.rules';
import {
  calendarDateToApiIso,
  combineLocalDateAndTime,
  computeArrivalTimeHHmm,
  isoToLocalTimeHHmm,
  toLocalDateTimeApiString
} from '../../../core/utils/trip-datetime.util';

type TripFormStepId = 'basic' | 'location' | 'waypoints' | 'schedule' | 'assignment' | 'review';

@Component({
  standalone: false,
  selector: 'app-trip-form',
  templateUrl: './trip-form.component.html',
  styleUrls: ['./trip-form.component.scss']
})
export class TripFormComponent implements OnInit, OnDestroy {
  @ViewChild('pickupInput') pickupInput?: ElementRef<HTMLInputElement>;
  @ViewChild('destinationInput') destinationInput?: ElementRef<HTMLInputElement>;
  @ViewChildren('stopLocationInput') stopLocationInputs?: QueryList<ElementRef<HTMLInputElement>>;

  form!: FormGroup;
  loading = false;
  saving = false;
  isEdit = false;
  tripId: number | null = null;
  mapsConfigured = false;
  mapsUnavailable = false;
  mapsError: string | null = null;
  routingBusy = false;
  routePrefillActive = false;
  routingError: string | null = null;
  routeClearedNotice: string | null = null;
  /** Saved route dropdown vs manual address entry. */
  locationMode: 'route' | 'manual' = 'manual';
  /** Coordinates stay visible in the redesigned location cards. */
  showCoordinates = true;

  customers: Customer[] = [];
  routes: Route[] = [];
  drivers: Array<{ id: number; fullName: string }> = [];
  vehicles: Array<{ id: number; name: string }> = [];

  readonly tripTypes = TRIP_TYPES;
  readonly priorities = TRIP_PRIORITIES;
  readonly notesMaxLength = 500;

  readonly tripSteps: Array<{
    id: TripFormStepId;
    number: number;
    label: string;
    title: string;
    subtitle: string;
    nextLabel?: string;
  }> = [
    {
      id: 'basic',
      number: 1,
      label: 'Trip Details',
      title: 'Trip Details',
      subtitle: 'Enter the basic details for this trip.',
      nextLabel: 'Next: Pickup & Destination'
    },
    {
      id: 'location',
      number: 2,
      label: 'Pickup Location',
      title: 'Pickup & Destination',
      subtitle: 'Define pickup and destination locations.',
      nextLabel: 'Next: Waypoints'
    },
    {
      id: 'waypoints',
      number: 3,
      label: 'Waypoints',
      title: 'Waypoints',
      subtitle: 'Add intermediate stops along the route.',
      nextLabel: 'Next: Schedule'
    },
    {
      id: 'schedule',
      number: 4,
      label: 'Schedule',
      title: 'Trip Schedule',
      subtitle: 'Set the timing and duration for this trip.',
      nextLabel: 'Next: Assignment'
    },
    {
      id: 'assignment',
      number: 5,
      label: 'Assignment',
      title: 'Trip Assignment',
      subtitle: 'Assign driver and vehicle for this trip.',
      nextLabel: 'Next: Review'
    },
    {
      id: 'review',
      number: 6,
      label: 'Review',
      title: 'Review & Confirm',
      subtitle: 'Confirm trip details before creating.'
    }
  ];

  currentStepId: TripFormStepId = 'basic';

  /** Planned route path for the preview map (Routes API). */
  routePath: google.maps.LatLngLiteral[] = [];
  routeDurationBaseSeconds = 0;
  routeDurationTrafficSeconds: number | null = null;
  mapCenter: google.maps.LatLngLiteral = { lat: 24.8607, lng: 67.0011 };
  mapZoom = 11;
  readonly mapOptions: google.maps.MapOptions = {
    disableDefaultUI: true,
    zoomControl: true,
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: true
  };
  readonly routePolylineOptions: google.maps.PolylineOptions = {
    strokeColor: '#0d9488',
    strokeWeight: 5,
    strokeOpacity: 0.9
  };

  private arrivalManuallyEdited = false;
  private metricsManuallyEdited = false;
  private applyingRoute = false;
  private patchingComputed = false;
  private subs = new Subscription();
  private placesHandles: SheikhGoPlacesAutocompleteHandle[] = [];
  private stopPlacesHandles: SheikhGoPlacesAutocompleteHandle[] = [];
  private placesInitAttempts = 0;
  private stopPlacesInitAttempts = 0;
  private ignoreAddressEdits = 0;
  private committedPickup = '';
  private committedDestination = '';
  private committedStops: string[] = [];

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
    private geocoder: MapGeocoder,
    private toast: UiToastService,
    private zone: NgZone,
    private cdr: ChangeDetectorRef
  ) {
    this.mapsConfigured = this.mapsLoader.isConfigured;
  }

  get stops(): FormArray {
    return this.form.get('stops') as FormArray;
  }

  get routingAllowed(): boolean {
    if (!this.form) return false;
    const raw = this.form.getRawValue();
    return canCalculateRoute(
      raw.pickupLatitude,
      raw.pickupLongitude,
      raw.destinationLatitude,
      raw.destinationLongitude
    );
  }

  get pickupMissingCoords(): boolean {
    if (!this.form || this.routingBusy || this.applyingRoute) return false;
    const raw = this.form.getRawValue();
    return !!locationSelectionError(raw.pickupAddress, raw.pickupLatitude, raw.pickupLongitude);
  }

  get destinationMissingCoords(): boolean {
    if (!this.form || this.routingBusy || this.applyingRoute) return false;
    const raw = this.form.getRawValue();
    return !!locationSelectionError(
      raw.destinationAddress,
      raw.destinationLatitude,
      raw.destinationLongitude
    );
  }

  get loadedRouteDistance(): number | null {
    return readCoordinate(this.form?.get('plannedDistanceKm')?.value);
  }

  get loadedRouteDuration(): number | null {
    return readCoordinate(this.form?.get('estimatedDurationMinutes')?.value);
  }

  get hasAnyCoordinates(): boolean {
    if (!this.form) return false;
    const raw = this.form.getRawValue();
    return (
      readCoordinate(raw.pickupLatitude) != null ||
      readCoordinate(raw.pickupLongitude) != null ||
      readCoordinate(raw.destinationLatitude) != null ||
      readCoordinate(raw.destinationLongitude) != null
    );
  }

  /** Duration/distance stay system-filled unless Maps is down. */
  get allowMetricsOverride(): boolean {
    return this.mapsUnavailable || !this.mapsConfigured;
  }

  get notesLength(): number {
    return String(this.form?.get('driverNotes')?.value ?? '').length;
  }

  get currentStepIndex(): number {
    return this.tripSteps.findIndex(s => s.id === this.currentStepId);
  }

  get currentStepMeta() {
    const meta = this.tripSteps[this.currentStepIndex] ?? this.tripSteps[0];
    if (this.isEdit && meta.id === 'basic') {
      return { ...meta, title: 'Edit Trip Details' };
    }
    if (this.isEdit && meta.id === 'review') {
      return { ...meta, title: 'Review & Save' };
    }
    return meta;
  }

  get nextStepButtonLabel(): string {
    return this.currentStepMeta.nextLabel || 'Next';
  }

  get mapMarkers(): Array<{ position: google.maps.LatLngLiteral; label: string; title: string }> {
    if (!this.form) return [];
    const raw = this.form.getRawValue();
    const markers: Array<{ position: google.maps.LatLngLiteral; label: string; title: string }> = [];
    const pLat = readCoordinate(raw.pickupLatitude);
    const pLng = readCoordinate(raw.pickupLongitude);
    const dLat = readCoordinate(raw.destinationLatitude);
    const dLng = readCoordinate(raw.destinationLongitude);
    if (pLat != null && pLng != null) {
      markers.push({ position: { lat: pLat, lng: pLng }, label: 'P', title: raw.pickupAddress || 'Pickup' });
    }
    (raw.stops || []).forEach(
      (s: { location?: string; latitude?: number | null; longitude?: number | null }, i: number) => {
        const lat = readCoordinate(s.latitude);
        const lng = readCoordinate(s.longitude);
        if (lat == null || lng == null) return;
        markers.push({
          position: { lat, lng },
          label: String(i + 1),
          title: s.location || `Stop ${i + 1}`
        });
      }
    );
    if (dLat != null && dLng != null) {
      markers.push({ position: { lat: dLat, lng: dLng }, label: 'D', title: raw.destinationAddress || 'Destination' });
    }
    return markers;
  }

  get trafficLabel(): string {
    if (!this.routePath.length) return '—';
    const base = this.routeDurationBaseSeconds;
    const traffic = this.routeDurationTrafficSeconds;
    if (traffic == null || base <= 0) return 'Unknown';
    const ratio = traffic / base;
    if (ratio < 1.1) return 'Light';
    if (ratio < 1.35) return 'Moderate';
    return 'Heavy';
  }

  get trafficTone(): 'light' | 'moderate' | 'heavy' | 'unknown' {
    const label = this.trafficLabel;
    if (label === 'Light') return 'light';
    if (label === 'Moderate') return 'moderate';
    if (label === 'Heavy') return 'heavy';
    return 'unknown';
  }

  markerOptions(label: string): google.maps.MarkerOptions {
    const isPickup = label === 'P';
    return {
      label: {
        text: label,
        color: '#fff',
        fontWeight: '700',
        fontSize: '12px'
      },
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 12,
        fillColor: isPickup ? '#0d9488' : '#ef4444',
        fillOpacity: 1,
        strokeColor: '#fff',
        strokeWeight: 2
      }
    };
  }

  swapLocations(): void {
    if (!this.form) return;
    const raw = this.form.getRawValue();
    this.ignoreAddressEdits += 2;
    this.form.patchValue({
      pickupAddress: raw.destinationAddress,
      pickupLatitude: raw.destinationLatitude,
      pickupLongitude: raw.destinationLongitude,
      destinationAddress: raw.pickupAddress,
      destinationLatitude: raw.pickupLatitude,
      destinationLongitude: raw.pickupLongitude
    });
    this.committedPickup = String(raw.destinationAddress || '');
    this.committedDestination = String(raw.pickupAddress || '');
    this.routePrefillActive = false;
    this.metricsManuallyEdited = false;
    this.arrivalManuallyEdited = false;
    this.retryRouting();
  }

  get isFirstStep(): boolean {
    return this.currentStepIndex <= 0;
  }

  get isLastStep(): boolean {
    return this.currentStepId === 'review';
  }

  get customerLabel(): string {
    const id = this.form?.get('customerId')?.value;
    return this.customers.find(c => c.id === id)?.fullName || '—';
  }

  get driverLabel(): string {
    const id = this.form?.get('driverId')?.value;
    return this.drivers.find(d => d.id === id)?.fullName || 'Not assigned';
  }

  get assistantDriverLabel(): string {
    const id = this.form?.get('assistantDriverId')?.value;
    return this.drivers.find(d => d.id === id)?.fullName || 'Not assigned';
  }

  get vehicleLabel(): string {
    const id = this.form?.get('vehicleId')?.value;
    return this.vehicles.find(v => v.id === id)?.name || 'Not assigned';
  }

  get routeLabel(): string {
    const id = this.form?.get('routeId')?.value;
    if (id == null) return 'Manual addresses';
    const r = this.routes.find(x => x.id === id);
    return r ? r.name || `${r.source} → ${r.destination}` : '—';
  }

  isStepComplete(stepId: TripFormStepId): boolean {
    const idx = this.tripSteps.findIndex(s => s.id === stepId);
    return idx > -1 && idx < this.currentStepIndex;
  }

  isStepActive(stepId: TripFormStepId): boolean {
    return this.currentStepId === stepId;
  }

  goToStep(stepId: TripFormStepId): void {
    const targetIdx = this.tripSteps.findIndex(s => s.id === stepId);
    if (targetIdx < 0) return;

    // Allow going back freely; going forward requires prior steps valid
    if (targetIdx > this.currentStepIndex) {
      for (let i = 0; i < targetIdx; i++) {
        if (!this.validateStep(this.tripSteps[i].id, false)) {
          this.currentStepId = this.tripSteps[i].id;
          this.validateStep(this.tripSteps[i].id, true);
          return;
        }
      }
    }

    this.currentStepId = stepId;
    this.afterStepChange();
  }

  nextStep(): void {
    if (!this.validateStep(this.currentStepId, true)) return;
    const next = this.tripSteps[this.currentStepIndex + 1];
    if (!next) return;
    this.currentStepId = next.id;
    this.afterStepChange();
  }

  prevStep(): void {
    const prev = this.tripSteps[this.currentStepIndex - 1];
    if (!prev) return;
    this.currentStepId = prev.id;
    this.afterStepChange();
  }

  private afterStepChange(): void {
    this.cdr.detectChanges();
    if (this.currentStepId === 'location') {
      this.placesInitAttempts = 0;
      setTimeout(() => void this.initPlacesAutocomplete(), 0);
    }
    if (this.currentStepId === 'waypoints') {
      this.stopPlacesInitAttempts = 0;
      setTimeout(() => void this.initStopPlacesAutocomplete(), 0);
    }
  }

  private stepControls(stepId: TripFormStepId): string[] {
    switch (stepId) {
      case 'basic':
        return ['tripName', 'tripType', 'customerId', 'passengerCount', 'priority'];
      case 'location':
        return ['pickupAddress', 'destinationAddress'];
      case 'waypoints':
        return [];
      case 'schedule':
        return ['tripDate', 'pickupTime'];
      case 'assignment':
        return ['driverNotes'];
      case 'review':
        return [];
      default:
        return [];
    }
  }

  validateStep(stepId: TripFormStepId, markTouched: boolean): boolean {
    if (!this.form) return false;

    const controls = this.stepControls(stepId);
    let valid = true;

    for (const key of controls) {
      const c = this.form.get(key);
      if (!c) continue;
      if (markTouched) c.markAsTouched();
      if (c.invalid) valid = false;
    }

    if (stepId === 'waypoints') {
      this.stops.controls.forEach(group => {
        const loc = group.get('location');
        if (markTouched) loc?.markAsTouched();
        if (loc?.invalid) valid = false;
      });
    }

    if (stepId === 'location' && valid) {
      const raw = this.form.getRawValue();
      const pickupErr = locationSelectionError(raw.pickupAddress, raw.pickupLatitude, raw.pickupLongitude);
      const destErr = locationSelectionError(
        raw.destinationAddress,
        raw.destinationLatitude,
        raw.destinationLongitude
      );
      if (pickupErr || destErr) {
        if (markTouched) {
          this.toast.warning(pickupErr || destErr || PLACE_SELECTION_WARNING);
        }
        valid = false;
      }
    }

    return valid;
  }

  setLocationMode(mode: 'route' | 'manual'): void {
    if (this.locationMode === mode) return;
    this.locationMode = mode;
    this.routeClearedNotice = null;

    if (mode === 'manual') {
      const hadRoute = this.routePrefillActive || this.form.get('routeId')?.value != null;
      this.routePrefillActive = false;
      this.form.patchValue({ routeId: null }, { emitEvent: false });
      if (hadRoute) {
        this.routeClearedNotice = 'Route cleared because manual entry was selected.';
      }
    }
  }

  toggleCoordinates(): void {
    this.showCoordinates = !this.showCoordinates;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      tripName: ['', Validators.required],
      tripType: ['Transfer' as TripType, Validators.required],
      customerId: [null, Validators.required],
      routeId: [null],
      passengerCount: [1, [Validators.required, Validators.min(1)]],
      priority: ['Normal' as TripPriority, Validators.required],
      pickupAddress: ['', Validators.required],
      pickupLatitude: [{ value: null, disabled: true }],
      pickupLongitude: [{ value: null, disabled: true }],
      destinationAddress: ['', Validators.required],
      destinationLatitude: [{ value: null, disabled: true }],
      destinationLongitude: [{ value: null, disabled: true }],
      tripDate: ['', Validators.required],
      pickupTime: ['', Validators.required],
      arrivalTime: [''],
      estimatedDurationMinutes: [null],
      plannedDistanceKm: [null],
      driverNotes: ['', Validators.maxLength(500)],
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
        this.cdr.detectChanges();

        const idParam = this.route.snapshot.paramMap.get('id');
        const isEditRoute = this.router.url.includes('/edit');
        if (idParam && isEditRoute) {
          this.isEdit = true;
          this.tripId = +idParam;
          this.loadTrip(+idParam);
        }
        // Places autocomplete initializes when the location step becomes visible.
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load form data.'));
      }
    });
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.destroyAutocompletes();
  }

  /** User typed in an address field — coordinates and route metrics are no longer valid. */
  onAddressTyped(field: 'pickup' | 'destination'): void {
    if (this.ignoreAddressEdits > 0 || this.applyingRoute || this.patchingComputed) return;

    const addressKey = field === 'pickup' ? 'pickupAddress' : 'destinationAddress';
    const committed = field === 'pickup' ? this.committedPickup : this.committedDestination;
    const current = String(this.form.get(addressKey)?.value || '').trim();
    if (committed && current === committed) return;

    const hadRoute = this.routePrefillActive || this.form.get('routeId')?.value != null;
    this.routePrefillActive = false;
    this.locationMode = 'manual';
    this.routingError = null;

    if (field === 'pickup') {
      this.committedPickup = '';
      this.form.patchValue(
        { pickupLatitude: null, pickupLongitude: null, routeId: null },
        { emitEvent: false }
      );
    } else {
      this.committedDestination = '';
      this.form.patchValue(
        { destinationLatitude: null, destinationLongitude: null, routeId: null },
        { emitEvent: false }
      );
    }

    this.clearDerivedMetrics();
    this.routeClearedNotice = hadRoute
      ? 'Route cleared because the address was changed.'
      : null;
  }

  retryMaps(): void {
    void this.retryMapsLoad();
  }

  retryRouting(): void {
    this.routingError = null;
    const coords = this.currentCoords();
    if (!coords) {
      this.clearDerivedMetrics();
      this.toast.warning(PLACE_SELECTION_WARNING);
      return;
    }
    void this.runDirections(coords.pickup, coords.dest);
  }

  private wireReactiveBehavior(): void {
    this.subs.add(
      this.mapsLoader.authFailures$.subscribe(message => {
        this.zone.run(() => {
          this.mapsUnavailable = true;
          this.mapsError = message;
          this.routingError = null;
          this.clearDerivedMetrics();
          this.destroyAutocompletes();
          this.cdr.markForCheck();
        });
      })
    );

    this.subs.add(
      merge(
        this.form.get('tripDate')!.valueChanges,
        this.form.get('pickupTime')!.valueChanges,
        this.form.get('estimatedDurationMinutes')!.valueChanges
      )
        .pipe(debounceTime(150))
        .subscribe(() => this.recomputeArrivalTime())
    );

    this.subs.add(
      this.form.get('arrivalTime')!.valueChanges.subscribe(() => {
        if (!this.patchingComputed) {
          this.arrivalManuallyEdited = true;
        }
      })
    );

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

    this.subs.add(
      this.form.get('routeId')!.valueChanges.subscribe(routeId => {
        if (this.applyingRoute || this.patchingComputed) return;
        void this.applySelectedRoute(routeId);
      })
    );

    this.subs.add(
      merge(
        this.form.get('pickupLatitude')!.valueChanges,
        this.form.get('pickupLongitude')!.valueChanges,
        this.form.get('destinationLatitude')!.valueChanges,
        this.form.get('destinationLongitude')!.valueChanges
      )
        .pipe(debounceTime(400))
        .subscribe(() => {
          if (this.applyingRoute || this.patchingComputed) return;
          const coords = this.currentCoords();
          if (!coords || this.mapsLoader.authFailed) return;
          this.computeDrivingRoute(coords);
        })
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
          pickupTime: isoToLocalTimeHHmm(trip.plannedStart),
          arrivalTime: trip.plannedEnd ? isoToLocalTimeHHmm(trip.plannedEnd) : '',
          estimatedDurationMinutes: trip.estimatedDurationMinutes,
          plannedDistanceKm: trip.plannedDistanceKm,
          driverNotes: trip.driverNotes,
          driverId: trip.driverId,
          assistantDriverId: trip.assistantDriverId,
          vehicleId: trip.vehicleId
        }, { emitEvent: false });
        this.routePrefillActive = !!trip.routeId;
        this.locationMode = trip.routeId ? 'route' : 'manual';
        this.showCoordinates = canCalculateRoute(
          trip.pickupLatitude,
          trip.pickupLongitude,
          trip.destinationLatitude,
          trip.destinationLongitude
        );
        this.committedPickup = trip.pickupAddress || '';
        this.committedDestination = trip.destinationAddress || '';
        this.routeClearedNotice = null;
        if (
          !canCalculateRoute(
            trip.pickupLatitude,
            trip.pickupLongitude,
            trip.destinationLatitude,
            trip.destinationLongitude
          )
        ) {
          this.form.patchValue(
            { plannedDistanceKm: null, estimatedDurationMinutes: null },
            { emitEvent: false }
          );
        }
        this.stops.clear();
        this.committedStops = [];
        for (const s of trip.stops || []) {
          this.stops.push(this.fb.group({
            sequence: [s.sequence],
            location: [s.location, Validators.required],
            latitude: [{ value: s.latitude, disabled: true }],
            longitude: [{ value: s.longitude, disabled: true }],
            eta: [s.eta ? this.toLocalInput(s.eta) : '']
          }));
          this.committedStops.push(s.location || '');
        }
        this.patchingComputed = false;
        this.cdr.detectChanges();
        // Autocomplete binds when user opens the location step.
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load trip.'))
    });
  }

  addStop(): void {
    this.stops.push(this.fb.group({
      sequence: [this.stops.length + 1],
      location: ['', Validators.required],
      latitude: [{ value: null, disabled: true }],
      longitude: [{ value: null, disabled: true }],
      eta: ['']
    }));
    this.committedStops.push('');
    this.cdr.detectChanges();
    if (this.currentStepId === 'waypoints') {
      this.stopPlacesInitAttempts = 0;
      setTimeout(() => void this.initStopPlacesAutocomplete(), 0);
    }
  }

  removeStop(index: number): void {
    this.destroyStopPlacesAutocomplete();
    this.stops.removeAt(index);
    this.committedStops.splice(index, 1);
    this.stops.controls.forEach((c, i) => c.patchValue({ sequence: i + 1 }));
    this.cdr.detectChanges();
    if (this.currentStepId === 'waypoints') {
      this.stopPlacesInitAttempts = 0;
      setTimeout(() => void this.initStopPlacesAutocomplete(), 0);
    }
    this.recomputeRouteFromForm();
  }

  onStopAddressTyped(index: number): void {
    if (this.ignoreAddressEdits > 0) return;
    const group = this.stops.at(index);
    if (!group) return;
    const location = String(group.get('location')?.value || '');
    const committed = this.committedStops[index] || '';
    if (location === committed) return;
    group.patchValue({ latitude: null, longitude: null }, { emitEvent: false });
    this.committedStops[index] = '';
    this.recomputeRouteFromForm();
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
    const pickupError = locationSelectionError(v.pickupAddress, v.pickupLatitude, v.pickupLongitude);
    const destinationError = locationSelectionError(
      v.destinationAddress,
      v.destinationLatitude,
      v.destinationLongitude
    );
    if (pickupError || destinationError) {
      this.toast.warning(pickupError || destinationError || PLACE_SELECTION_WARNING);
      return;
    }

    const plannedStart = combineLocalDateAndTime(v.tripDate, v.pickupTime);
    if (!plannedStart) {
      this.toast.error('Trip date and pickup time are required.');
      return;
    }

    const metrics = routeMetricsForPayload(v);
    const routeReady = canCalculateRoute(
      v.pickupLatitude,
      v.pickupLongitude,
      v.destinationLatitude,
      v.destinationLongitude
    );

    let plannedEnd: Date | null = null;
    if (v.arrivalTime) {
      plannedEnd = combineLocalDateAndTime(v.tripDate, v.arrivalTime);
      if (plannedEnd && plannedEnd.getTime() <= plannedStart.getTime()) {
        plannedEnd = new Date(plannedEnd.getTime() + 24 * 60 * 60 * 1000);
      }
    } else if (routeReady && metrics.estimatedDurationMinutes != null && metrics.estimatedDurationMinutes > 0) {
      plannedEnd = new Date(plannedStart.getTime() + Number(metrics.estimatedDurationMinutes) * 60_000);
    }

    const payload: CreateTripDto = {
      tripName: v.tripName,
      tripType: v.tripType,
      customerId: +v.customerId,
      routeId: routeReady && v.routeId ? +v.routeId : null,
      passengerCount: +v.passengerCount,
      priority: v.priority,
      pickupAddress: v.pickupAddress || null,
      pickupLatitude: readCoordinate(v.pickupLatitude),
      pickupLongitude: readCoordinate(v.pickupLongitude),
      destinationAddress: v.destinationAddress || null,
      destinationLatitude: readCoordinate(v.destinationLatitude),
      destinationLongitude: readCoordinate(v.destinationLongitude),
      tripDate: calendarDateToApiIso(v.tripDate),
      plannedStart: toLocalDateTimeApiString(plannedStart),
      plannedEnd: plannedEnd ? toLocalDateTimeApiString(plannedEnd) : null,
      estimatedDurationMinutes: metrics.estimatedDurationMinutes,
      plannedDistanceKm: metrics.plannedDistanceKm,
      driverNotes: v.driverNotes || null,
      driverId: v.driverId ? +v.driverId : null,
      assistantDriverId: v.assistantDriverId ? +v.assistantDriverId : null,
      vehicleId: v.vehicleId ? +v.vehicleId : null,
      stops: (v.stops || []).map(
        (s: { sequence: number; location: string; latitude?: number | null; longitude?: number | null; eta?: string }) => ({
          sequence: s.sequence,
          location: s.location,
          latitude: readCoordinate(s.latitude),
          longitude: readCoordinate(s.longitude),
          eta: s.eta ? new Date(s.eta).toISOString() : null
        })
      )
    };

    this.saving = true;
    if (this.isEdit && this.tripId) {
      const tripId = this.tripId;
      const { driverId, assistantDriverId, vehicleId, bookingId, ...update } = payload as CreateTripDto & { bookingId?: number };
      this.trips.update(tripId, update).subscribe({
        next: () => this.afterUpdateAssignments(tripId, payload),
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

  private afterUpdateAssignments(tripId: number, payload: CreateTripDto): void {
    const tasks = [];
    if (payload.driverId) {
      tasks.push(
        this.trips.assignDriver(
          tripId,
          payload.driverId,
          payload.assistantDriverId,
          payload.driverNotes || undefined
        )
      );
    }
    if (payload.vehicleId) {
      tasks.push(this.trips.assignVehicle(tripId, payload.vehicleId));
    }

    if (!tasks.length) {
      this.saving = false;
      this.toast.success('Trip updated.');
      this.router.navigate(['/trips', tripId]);
      return;
    }

    forkJoin(tasks).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Trip updated.');
        this.router.navigate(['/trips', tripId]);
      },
      error: err => {
        this.saving = false;
        this.toast.warning(
          apiErrorMessage(err, 'Trip saved, but driver/vehicle assignment could not be updated.')
        );
        this.router.navigate(['/trips', tripId]);
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
    this.locationMode = 'route';
    this.routePrefillActive = true;
    this.routeClearedNotice = null;
    this.arrivalManuallyEdited = false;
    this.metricsManuallyEdited = false;
    this.routingError = null;
    this.committedPickup = selected.source || '';
    this.committedDestination = selected.destination || '';

    this.form.patchValue({
      pickupAddress: selected.source || '',
      destinationAddress: selected.destination || '',
      pickupLatitude: null,
      pickupLongitude: null,
      destinationLatitude: null,
      destinationLongitude: null,
      plannedDistanceKm: null,
      estimatedDurationMinutes: null,
      arrivalTime: ''
    }, { emitEvent: false });

    this.applyingRoute = false;
    void this.resolveSelectedRoute(selected);
  }

  private recomputeArrivalTime(): void {
    if (this.arrivalManuallyEdited) return;

    const tripDate = this.form.get('tripDate')?.value;
    const pickupTime = this.form.get('pickupTime')?.value;
    const duration = Number(this.form.get('estimatedDurationMinutes')?.value);
    const arrival = computeArrivalTimeHHmm(tripDate, pickupTime, duration);

    this.patchingComputed = true;
    this.form.patchValue({ arrivalTime: arrival || '' }, { emitEvent: false });
    this.patchingComputed = false;
  }

  private currentCoords(): {
    pickup: google.maps.LatLngLiteral;
    dest: google.maps.LatLngLiteral;
  } | null {
    const raw = this.form.getRawValue();
    const plat = readCoordinate(raw.pickupLatitude);
    const plng = readCoordinate(raw.pickupLongitude);
    const dlat = readCoordinate(raw.destinationLatitude);
    const dlng = readCoordinate(raw.destinationLongitude);
    if (plat == null || plng == null || dlat == null || dlng == null) return null;
    return {
      pickup: { lat: plat, lng: plng },
      dest: { lat: dlat, lng: dlng }
    };
  }

  private stopWaypoints(): google.maps.LatLngLiteral[] {
    const raw = this.form.getRawValue();
    const waypoints: google.maps.LatLngLiteral[] = [];
    for (const s of raw.stops || []) {
      const lat = readCoordinate(s.latitude);
      const lng = readCoordinate(s.longitude);
      if (lat != null && lng != null) waypoints.push({ lat, lng });
    }
    return waypoints;
  }

  private recomputeRouteFromForm(): void {
    const coords = this.currentCoords();
    if (!coords) {
      this.clearDerivedMetrics();
      return;
    }
    this.computeDrivingRoute(coords);
  }

  private computeDrivingRoute(coords: {
    pickup: google.maps.LatLngLiteral;
    dest: google.maps.LatLngLiteral;
  }): void {
    if (!this.mapsConfigured || this.mapsLoader.authFailed) return;
    void this.runDirections(coords.pickup, coords.dest);
  }

  private async resolveSelectedRoute(selected: Route): Promise<void> {
    if (!(await this.ensureMapsReady())) {
      this.clearDerivedMetrics();
      this.routingError = null;
      this.cdr.markForCheck();
      return;
    }

    this.routingBusy = true;
    this.cdr.markForCheck();
    const pickup = await this.geocodeAddress(selected.source);
    const destination = await this.geocodeAddress(selected.destination);
    this.routingBusy = false;

    if (!pickup || !destination) {
      this.clearDerivedMetrics();
      this.routingError = this.mapsUnavailable ? null : PLACE_SELECTION_WARNING;
      this.cdr.markForCheck();
      return;
    }

    this.ignoreAddressEdits++;
    this.applyingRoute = true;
    this.committedPickup = pickup.label;
    this.committedDestination = destination.label;
    this.form.patchValue({
      pickupAddress: pickup.label,
      pickupLatitude: pickup.lat,
      pickupLongitude: pickup.lng,
      destinationAddress: destination.label,
      destinationLatitude: destination.lat,
      destinationLongitude: destination.lng
    }, { emitEvent: false });
    this.applyingRoute = false;
    this.ignoreAddressEdits = Math.max(0, this.ignoreAddressEdits - 1);
    this.showCoordinates = true;
    await this.runDirections(
      { lat: pickup.lat, lng: pickup.lng },
      { lat: destination.lat, lng: destination.lng }
    );
  }

  private async runDirections(
    origin: google.maps.LatLngLiteral,
    destination: google.maps.LatLngLiteral
  ): Promise<void> {
    if (!canCalculateRoute(origin.lat, origin.lng, destination.lat, destination.lng)) {
      this.clearDerivedMetrics();
      return;
    }

    if (!(await this.ensureMapsReady())) {
      this.clearDerivedMetrics();
      this.routingError = null;
      this.cdr.markForCheck();
      return;
    }

    this.routingBusy = true;
    this.routingError = null;

    try {
      const routes = await computeDrivingRoute(
        {
          origin,
          destination,
          waypoints: this.stopWaypoints(),
          travelMode: 'DRIVING',
          routingPreference: 'TRAFFIC_AWARE'
        },
        this.mapsLoader
      );
      this.routingBusy = false;
      const route = routes[0];
      if (!route) {
        this.clearDerivedMetrics();
        this.routingError = 'No driving route found between pickup and destination.';
        this.cdr.markForCheck();
        return;
      }

      this.applyComputedRoute(route);
    } catch (err) {
      this.routingBusy = false;
      this.clearDerivedMetrics();
      const message = err instanceof Error ? err.message : '';
      if (/403|denied|not enabled|api key/i.test(message)) {
        this.markMapsDenied();
        this.routingError = null;
      } else {
        this.routingError = 'Route calculation failed. Check Places and Routes API access for this key.';
      }
      this.cdr.markForCheck();
    }
  }

  private applyComputedRoute(route: ComputedDrivingRoute): void {
    this.routePath = route.path;
    this.routeDurationBaseSeconds = route.durationSeconds;
    this.routeDurationTrafficSeconds = route.durationInTrafficSeconds ?? null;

    const center = pathCenter(route.path);
    if (center) {
      this.mapCenter = center;
      this.mapZoom = 11;
    }

    const hasTraffic = route.durationInTrafficSeconds != null;
    const km = Math.round(route.distanceMeters / 100) / 10;
    const seconds = hasTraffic ? route.durationInTrafficSeconds! : route.durationSeconds;
    const minutes = Math.max(1, Math.round(seconds / 60));

    if (!this.currentCoords()) {
      this.clearDerivedMetrics();
      this.cdr.markForCheck();
      return;
    }

    this.patchingComputed = true;
    this.applyingRoute = true;
    const patch: Record<string, unknown> = {};
    if (!this.metricsManuallyEdited) {
      patch['plannedDistanceKm'] = km;
      patch['estimatedDurationMinutes'] = minutes;
    }
    this.form.patchValue(patch, { emitEvent: false });
    this.applyingRoute = false;
    this.patchingComputed = false;
    this.routingError = null;
    this.recomputeArrivalTime();
    this.cdr.markForCheck();
  }

  private async initPlacesAutocomplete(): Promise<void> {
    if (!this.mapsConfigured) {
      this.mapsUnavailable = true;
      this.mapsError = 'Google Maps API key is not configured. Address search and auto distance/duration are disabled.';
      return;
    }
    if (!this.pickupInput?.nativeElement || !this.destinationInput?.nativeElement) {
      if (this.placesInitAttempts++ < 20) {
        setTimeout(() => void this.initPlacesAutocomplete(), 50);
      }
      return;
    }
    this.placesInitAttempts = 0;

    try {
      await this.mapsLoader.importLibrary('places');
      if (this.mapsLoader.authFailed) {
        this.mapsUnavailable = true;
        this.mapsError = this.mapsLoader.failureMessage
          || 'Address search and routing are temporarily unavailable. Google Maps could not be initialized.';
        this.destroyAutocompletes();
        this.cdr.markForCheck();
        return;
      }

      this.destroyAutocompletes();
      this.mapsUnavailable = false;
      this.mapsError = null;

      const pickupHandle = await attachSheikhGoPlacesAutocomplete({
        input: this.pickupInput.nativeElement,
        ngZone: this.zone,
        mapsLoader: this.mapsLoader,
        onSelect: place => this.onPlaceSelected('pickup', place)
      });
      const destinationHandle = await attachSheikhGoPlacesAutocomplete({
        input: this.destinationInput.nativeElement,
        ngZone: this.zone,
        mapsLoader: this.mapsLoader,
        onSelect: place => this.onPlaceSelected('destination', place)
      });
      this.placesHandles = [pickupHandle, destinationHandle];
    } catch (err) {
      console.warn('Trip form Places autocomplete init failed:', err);
      this.mapsUnavailable = true;
      this.mapsError = this.mapsLoader.failureMessage
        || 'Address search and routing are temporarily unavailable. Google Maps could not be initialized.';
      this.destroyAutocompletes();
      this.cdr.markForCheck();
    }
  }

  private async initStopPlacesAutocomplete(): Promise<void> {
    if (!this.mapsConfigured || this.mapsLoader.authFailed) return;
    const inputs = this.stopLocationInputs?.toArray() ?? [];
    if (inputs.length !== this.stops.length) {
      if (this.stopPlacesInitAttempts++ < 20) {
        setTimeout(() => void this.initStopPlacesAutocomplete(), 50);
      }
      return;
    }
    this.stopPlacesInitAttempts = 0;
    this.destroyStopPlacesAutocomplete();

    try {
      await this.mapsLoader.importLibrary('places');
      const handles: SheikhGoPlacesAutocompleteHandle[] = [];
      for (let i = 0; i < inputs.length; i++) {
        const index = i;
        const handle = await attachSheikhGoPlacesAutocomplete({
          input: inputs[i].nativeElement,
          ngZone: this.zone,
          mapsLoader: this.mapsLoader,
          onSelect: place => this.onStopPlaceSelected(index, place)
        });
        handles.push(handle);
      }
      this.stopPlacesHandles = handles;
    } catch (err) {
      console.warn('Trip stop Places autocomplete init failed:', err);
    }
  }

  private onStopPlaceSelected(index: number, place: SheikhGoPlaceSelection): void {
    const group = this.stops.at(index);
    if (!group) return;
    const label = place.address || place.name || '';
    this.ignoreAddressEdits++;
    this.committedStops[index] = label;
    this.metricsManuallyEdited = false;
    group.patchValue(
      {
        location: label,
        latitude: roundCoord(place.lat),
        longitude: roundCoord(place.lng)
      },
      { emitEvent: false }
    );
    this.ignoreAddressEdits = Math.max(0, this.ignoreAddressEdits - 1);
    this.recomputeRouteFromForm();
    this.cdr.markForCheck();
  }

  private onPlaceSelected(field: 'pickup' | 'destination', place: SheikhGoPlaceSelection): void {
    const label = place.address || place.name || '';
    const hadRoute = this.routePrefillActive || this.form.get('routeId')?.value != null;

    this.ignoreAddressEdits++;
    this.applyingRoute = true;
    this.routingError = null;
    this.routePrefillActive = false;
    this.locationMode = 'manual';
    this.metricsManuallyEdited = false;
    this.routeClearedNotice = hadRoute
      ? 'Route cleared because the address was changed.'
      : null;

    if (field === 'pickup') {
      this.committedPickup = label;
      this.form.patchValue({
        pickupAddress: label,
        pickupLatitude: roundCoord(place.lat),
        pickupLongitude: roundCoord(place.lng),
        routeId: null
      }, { emitEvent: false });
    } else {
      this.committedDestination = label;
      this.form.patchValue({
        destinationAddress: label,
        destinationLatitude: roundCoord(place.lat),
        destinationLongitude: roundCoord(place.lng),
        routeId: null
      }, { emitEvent: false });
    }

    this.applyingRoute = false;
    this.ignoreAddressEdits = Math.max(0, this.ignoreAddressEdits - 1);
    this.showCoordinates = true;

    const coords = this.currentCoords();
    if (!coords) {
      this.clearDerivedMetrics();
      this.cdr.markForCheck();
      return;
    }

    this.computeDrivingRoute(coords);
    this.cdr.markForCheck();
  }

  private async ensureMapsReady(): Promise<boolean> {
    if (!this.mapsConfigured) {
      this.mapsUnavailable = true;
      this.mapsError = 'Google Maps API key is not configured. Address search and auto distance/duration are disabled.';
      return false;
    }
    if (this.mapsLoader.authFailed) {
      this.mapsUnavailable = true;
      this.mapsError = this.mapsLoader.failureMessage;
      return false;
    }

    try {
      await this.mapsLoader.importLibrary('geocoding');
      this.mapsUnavailable = false;
      this.mapsError = null;
      return true;
    } catch {
      this.mapsUnavailable = true;
      this.mapsError = this.mapsLoader.failureMessage
        || 'Address search and routing are temporarily unavailable. Google Maps could not be initialized.';
      return false;
    }
  }

  private geocodeAddress(
    address: string
  ): Promise<{ label: string; lat: number; lng: number } | null> {
    const query = address?.trim();
    if (!query) return Promise.resolve(null);

    return new Promise(resolve => {
      const sub = this.geocoder
        .geocode({ address: query, region: SHEIKHGO_DIRECTIONS_REGION })
        .subscribe({
          next: ({ results, status }) => {
            sub.unsubscribe();
            const hit = results?.[0];
            const loc = hit?.geometry?.location;
            if (status === 'REQUEST_DENIED') {
              this.markMapsDenied();
              resolve(null);
              return;
            }
            if (status !== 'OK' || !loc) {
              resolve(null);
              return;
            }
            resolve({
              label: hit.formatted_address || query,
              lat: roundCoord(loc.lat()),
              lng: roundCoord(loc.lng())
            });
          },
          error: () => {
            sub.unsubscribe();
            resolve(null);
          }
        });
    });
  }

  private markMapsDenied(): void {
    this.mapsUnavailable = true;
    this.mapsError = this.mapsLoader.failureMessage
      || 'Address search and routing are temporarily unavailable. Google denied the Maps request. Enable Cloud billing and the Maps JavaScript, Places, Geocoding, and Routes APIs, then retry.';
    this.destroyAutocompletes();
    this.cdr.markForCheck();
  }

  private clearDerivedMetrics(): void {
    this.routePath = [];
    this.routeDurationBaseSeconds = 0;
    this.routeDurationTrafficSeconds = null;
    const patch: Record<string, unknown> = {
      plannedDistanceKm: null,
      estimatedDurationMinutes: null
    };
    if (!this.arrivalManuallyEdited) {
      patch['arrivalTime'] = '';
    }
    this.patchingComputed = true;
    this.form.patchValue(patch, { emitEvent: false });
    this.patchingComputed = false;
    this.metricsManuallyEdited = false;
  }

  private async retryMapsLoad(): Promise<void> {
    this.routingError = null;
    this.destroyAutocompletes();
    const ok = await this.mapsLoader.retry();
    if (!ok) {
      this.mapsUnavailable = true;
      this.mapsError = this.mapsLoader.failureMessage
        || 'Address search and routing are temporarily unavailable. Google Maps could not be initialized.';
      this.cdr.markForCheck();
      return;
    }

    this.mapsUnavailable = false;
    this.mapsError = null;
    this.placesInitAttempts = 0;
    await this.initPlacesAutocomplete();
    this.cdr.markForCheck();
  }

  private destroyAutocompletes(): void {
    this.placesHandles.forEach(handle => handle.destroy());
    this.placesHandles = [];
    this.destroyStopPlacesAutocomplete();
  }

  private destroyStopPlacesAutocomplete(): void {
    this.stopPlacesHandles.forEach(handle => handle.destroy());
    this.stopPlacesHandles = [];
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
