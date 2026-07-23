import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  NgZone,
  OnDestroy,
  OnInit,
  QueryList,
  ViewChild,
  ViewChildren
} from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { GoogleMap, MapDirectionsService } from '@angular/google-maps';
import { Observable, Subscription } from 'rxjs';

import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { RouteService } from '../../../core/services/route.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import {
  CreateRouteDto,
  CreateRouteRequest,
  parseRouteWaypoints,
  serializeRouteWaypoints,
  UpdateRouteDto,
  UpdateRouteRequest
} from '../../../core/models/route.model';

interface RoutePreset {
  label: string;
  source: string;
  destination: string;
}

type RouteOptimizeMode = 'balanced' | 'fastest' | 'efficient' | 'no_tolls';
type TrafficLevel = 'clear' | 'moderate' | 'heavy' | 'unknown';
type MapPinRole = 'origin' | 'stop' | 'destination';

interface MapMarkerPoint {
  position: google.maps.LatLngLiteral;
  title: string;
  label: string;
  role: MapPinRole;
}

const LIGHT_MAP_STYLES: google.maps.MapTypeStyle[] = [
  { featureType: 'poi', stylers: [{ visibility: 'off' }] },
  { featureType: 'transit', stylers: [{ visibility: 'simplified' }] }
];

const DRAFT_KEY = 'stb-route-draft';

function differentEndpointsValidator(group: AbstractControl): ValidationErrors | null {
  const source = (group.get('source')?.value || '').toString().trim().toLowerCase();
  const destination = (group.get('destination')?.value || '').toString().trim().toLowerCase();
  if (!source || !destination) return null;
  return source === destination ? { sameEndpoints: true } : null;
}

@Component({
  standalone: false,
  selector: 'app-route-form',
  templateUrl: './route-form.component.html',
  styleUrls: ['./route-form.component.scss']
})
export class RouteFormComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('originInput') originInput?: ElementRef<HTMLInputElement>;
  @ViewChild('destinationInput') destinationInput?: ElementRef<HTMLInputElement>;
  @ViewChildren('stopInput') stopInputs?: QueryList<ElementRef<HTMLInputElement>>;
  @ViewChild(GoogleMap) googleMap?: GoogleMap;

  form: FormGroup;
  loading = false;
  isEdit = false;
  routeId: number | null = null;
  formDirty = false;

  mapsReady = false;
  mapsConfigured: boolean;
  calculating = false;
  computedDistanceText = '';
  computedDurationText = '';
  computedBasePriceText = '';
  mapsError: string | null = null;
  mapTypeSatellite = false;
  optimizeMode: RouteOptimizeMode = 'balanced';
  mapMarkers: MapMarkerPoint[] = [];
  routePathLabels: string[] = [];
  trafficLevel: TrafficLevel = 'unknown';
  metricsUpdated = false;
  routeLineActive = false;
  draftBannerVisible = false;

  stops: string[] = [];
  readonly presets: RoutePreset[] = [
    { label: 'Karachi → Lahore', source: 'Karachi, Pakistan', destination: 'Lahore, Pakistan' },
    { label: 'Lahore → Islamabad', source: 'Lahore, Pakistan', destination: 'Islamabad, Pakistan' },
    { label: 'Islamabad → Peshawar', source: 'Islamabad, Pakistan', destination: 'Peshawar, Pakistan' },
    { label: 'Karachi → Hyderabad', source: 'Karachi, Pakistan', destination: 'Hyderabad, Pakistan' },
    { label: 'Multan → Faisalabad', source: 'Multan, Pakistan', destination: 'Faisalabad, Pakistan' }
  ];

  readonly optimizeModes: { id: RouteOptimizeMode; label: string; icon: string; hint: string }[] = [
    { id: 'balanced', label: 'Balanced', icon: 'route', hint: 'Best overall driving route' },
    { id: 'fastest', label: 'Fastest', icon: 'bolt', hint: 'Prefer lowest ETA with live traffic' },
    { id: 'efficient', label: 'Shortest', icon: 'eco', hint: 'Prefer lowest distance among alternatives' },
    { id: 'no_tolls', label: 'Avoid tolls', icon: 'toll', hint: 'Exclude toll roads when possible' }
  ];

  mapCenter: google.maps.LatLngLiteral = { lat: 30.3753, lng: 69.3451 };
  mapZoom = 6;
  mapOptions: google.maps.MapOptions = {
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: false,
    zoomControl: true,
    styles: LIGHT_MAP_STYLES,
    backgroundColor: '#f1f5f9'
  };
  directionsResult: google.maps.DirectionsResult | null = null;

  get rendererOptions(): google.maps.DirectionsRendererOptions {
    const arrow =
      typeof google !== 'undefined' ? google.maps.SymbolPath.FORWARD_CLOSED_ARROW : 0;
    return {
      suppressMarkers: true,
      preserveViewport: true,
      polylineOptions: {
        strokeColor: '#0f766e',
        strokeWeight: 6,
        strokeOpacity: 0.9,
        icons: [
          {
            icon: {
              path: arrow,
              scale: 3,
              fillColor: '#14b8a6',
              fillOpacity: 1,
              strokeColor: '#0f766e',
              strokeWeight: 1
            },
            offset: '0',
            repeat: '100px'
          }
        ]
      }
    };
  }

  private originAutocomplete: google.maps.places.Autocomplete | null = null;
  private destinationAutocomplete: google.maps.places.Autocomplete | null = null;
  private stopAutocompletes: google.maps.places.Autocomplete[] = [];
  private placesListeners: google.maps.MapsEventListener[] = [];
  private stopListeners: google.maps.MapsEventListener[] = [];
  private recomputeTimer: ReturnType<typeof setTimeout> | null = null;
  private directionsSub: Subscription | null = null;
  private routeParamSub: Subscription | null = null;
  private formSub: Subscription | null = null;
  private stopInputsSub: Subscription | null = null;
  private allowNavigate = false;

  constructor(
    private fb: FormBuilder,
    private routeService: RouteService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: UiToastService,
    private mapsLoader: GoogleMapsLoaderService,
    private directionsService: MapDirectionsService,
    private zone: NgZone,
    private cdr: ChangeDetectorRef
  ) {
    this.mapsConfigured = this.mapsLoader.isConfigured;
    this.form = this.fb.group(
      {
        name: ['', [Validators.maxLength(200)]],
        source: ['', [Validators.required, Validators.maxLength(200)]],
        destination: ['', [Validators.required, Validators.maxLength(200)]],
        distance: [null as number | null, [Validators.required, Validators.min(0.1)]],
        estimatedMinutes: [null as number | null, [Validators.min(1)]],
        basePrice: [0, [Validators.required, Validators.min(0)]],
        isActive: [true]
      },
      { validators: differentEndpointsValidator }
    );
  }

  get hasRoutePreview(): boolean {
    return !!this.directionsResult && !!this.computedDistanceText;
  }

  get showMapEmpty(): boolean {
    return (
      this.mapsReady &&
      !this.calculating &&
      !this.hasRoutePreview &&
      !this.mapMarkers.length &&
      !this.mapsError
    );
  }

  get trafficLabel(): string {
    const labels: Record<TrafficLevel, string> = {
      clear: 'Clear roads',
      moderate: 'Moderate traffic',
      heavy: 'Heavy traffic',
      unknown: 'Traffic n/a'
    };
    return labels[this.trafficLevel];
  }

  get trafficIcon(): string {
    const icons: Record<TrafficLevel, string> = {
      clear: 'traffic',
      moderate: 'warning',
      heavy: 'report',
      unknown: 'help_outline'
    };
    return icons[this.trafficLevel];
  }

  get fuelEstimate(): number {
    const km = Number(this.form.get('distance')?.value) || 0;
    if (!km) return 0;
    return Math.round((km / 11) * 280);
  }

  get tollEstimate(): number {
    const km = Number(this.form.get('distance')?.value) || 0;
    if (!km || this.optimizeMode === 'no_tolls') return 0;
    return Math.round(km * 2.5);
  }

  get activeStopCount(): number {
    return this.stops.map(s => s.trim()).filter(Boolean).length;
  }

  get formattedDuration(): string {
    const min = Number(this.form.get('estimatedMinutes')?.value);
    if (!min) return '—';
    const h = Math.floor(min / 60);
    const m = min % 60;
    return h > 0 ? `${h}h ${m}m` : `${m} min`;
  }

  get sameEndpointsError(): boolean {
    return !!this.form.hasError('sameEndpoints') &&
      !!(this.form.get('source')?.touched || this.form.get('destination')?.touched);
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.formDirty && !this.allowNavigate) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  ngOnInit(): void {
    this.formSub = this.form.valueChanges.subscribe(() => {
      this.formDirty = true;
    });

    this.routeParamSub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.isEdit = true;
        this.routeId = +id;
        this.draftBannerVisible = false;
        this.loadRoute(this.routeId);
      } else {
        this.resetForm();
        this.promptDraftRestore();
      }
    });
  }

  private resetForm(): void {
    this.isEdit = false;
    this.routeId = null;
    this.form.reset({
      name: '',
      source: '',
      destination: '',
      distance: null,
      estimatedMinutes: null,
      basePrice: 0,
      isActive: true
    });
    this.stops = [];
    this.optimizeMode = 'balanced';
    this.directionsResult = null;
    this.mapMarkers = [];
    this.routePathLabels = [];
    this.routeLineActive = false;
    this.computedDistanceText = '';
    this.computedDurationText = '';
    this.computedBasePriceText = '';
    this.mapsError = null;
    this.trafficLevel = 'unknown';
    this.formDirty = false;
  }

  private promptDraftRestore(): void {
    try {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) return;
      this.draftBannerVisible = true;
    } catch {
      /* ignore */
    }
  }

  restoreDraft(): void {
    try {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) {
        this.draftBannerVisible = false;
        return;
      }
      const { form, stops, optimizeMode } = JSON.parse(raw);
      if (form) this.form.patchValue(form);
      if (Array.isArray(stops)) this.stops = stops;
      if (optimizeMode) this.optimizeMode = optimizeMode;
      this.draftBannerVisible = false;
      this.formDirty = true;
      this.scheduleRecompute();
      setTimeout(() => this.attachStopAutocompletes(), 0);
      this.toast.success('Draft restored.');
    } catch {
      this.toast.error('Could not restore draft.');
      this.discardDraft();
    }
  }

  discardDraft(): void {
    localStorage.removeItem(DRAFT_KEY);
    this.draftBannerVisible = false;
  }

  private loadRoute(id: number): void {
    this.routeService.getById(id).subscribe({
      next: r => {
        this.form.patchValue({
          name: r.name ?? '',
          source: r.source,
          destination: r.destination,
          distance: r.distance,
          estimatedMinutes: r.estimatedMinutes ?? null,
          basePrice: r.basePrice ?? 0,
          isActive: r.isActive
        });
        this.stops = parseRouteWaypoints(r.waypointsJson);
        this.optimizeMode = (r.optimizeMode as RouteOptimizeMode) || 'balanced';
        this.computedDistanceText = r.distance ? `${r.distance} km` : '';
        this.computedDurationText = r.estimatedMinutes ? `${r.estimatedMinutes} min` : '';
        this.computedBasePriceText = r.basePrice ? `PKR ${r.basePrice.toLocaleString('en-PK')}` : '';
        this.formDirty = false;
        if (this.mapsReady && r.source && r.destination) {
          this.scheduleRecompute();
        }
        setTimeout(() => this.attachStopAutocompletes(), 0);
      },
      error: () => this.toast.error('Failed to load route.')
    });
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.mapsConfigured) return;

    const loaded = await this.mapsLoader.load();
    if (!loaded) {
      this.mapsError = 'Could not load Google Maps. Check your API key configuration.';
      this.cdr.markForCheck();
      return;
    }

    this.zone.run(() => {
      this.mapsReady = true;
      this.cdr.markForCheck();
    });

    try {
      await this.mapsLoader.importLibrary('places');
      await this.mapsLoader.importLibrary('routes');
      this.zone.run(() => {
        this.attachAutocomplete();
        this.attachStopAutocompletes();
        this.stopInputsSub = this.stopInputs?.changes.subscribe(() => {
          this.attachStopAutocompletes();
        }) ?? null;
        const source = this.form.get('source')?.value;
        const destination = this.form.get('destination')?.value;
        if (source && destination) {
          this.scheduleRecompute();
        }
      });
    } catch {
      this.mapsError = 'Could not load Google Maps libraries.';
      this.cdr.markForCheck();
    }
  }

  ngOnDestroy(): void {
    if (this.recomputeTimer) clearTimeout(this.recomputeTimer);
    this.placesListeners.forEach(l => l.remove());
    this.stopListeners.forEach(l => l.remove());
    this.placesListeners = [];
    this.stopListeners = [];
    this.directionsSub?.unsubscribe();
    this.routeParamSub?.unsubscribe();
    this.formSub?.unsubscribe();
    this.stopInputsSub?.unsubscribe();
  }

  applyPreset(p: RoutePreset): void {
    this.form.patchValue({
      name: p.label,
      source: p.source,
      destination: p.destination
    });
    this.scheduleRecompute();
  }

  setOptimizeMode(mode: RouteOptimizeMode): void {
    this.optimizeMode = mode;
    this.formDirty = true;
    this.scheduleRecompute();
  }

  addStop(): void {
    this.stops.push('');
    this.formDirty = true;
    this.cdr.markForCheck();
    setTimeout(() => this.attachStopAutocompletes(), 0);
  }

  removeStop(index: number): void {
    this.stops.splice(index, 1);
    this.formDirty = true;
    this.scheduleRecompute();
    setTimeout(() => this.attachStopAutocompletes(), 0);
  }

  onStopBlur(): void {
    this.formDirty = true;
    this.scheduleRecompute();
  }

  saveDraft(): void {
    const payload = {
      form: this.form.getRawValue(),
      stops: this.stops,
      optimizeMode: this.optimizeMode
    };
    localStorage.setItem(DRAFT_KEY, JSON.stringify(payload));
    this.draftBannerVisible = false;
    this.toast.success('Draft saved locally.');
  }

  previewRoute(): void {
    if (!this.form.get('source')?.value || !this.form.get('destination')?.value) {
      this.toast.warning('Enter origin and destination first.');
      return;
    }
    if (this.form.hasError('sameEndpoints')) {
      this.toast.warning('Origin and destination must be different.');
      return;
    }
    this.computeRoute();
  }

  toggleMapType(): void {
    this.mapTypeSatellite = !this.mapTypeSatellite;
    const map = this.googleMap?.googleMap;
    if (map) {
      map.setMapTypeId(this.mapTypeSatellite ? 'hybrid' : 'roadmap');
      map.setOptions({ styles: this.mapTypeSatellite ? [] : LIGHT_MAP_STYLES });
    }
  }

  resetMapView(): void {
    if (this.directionsResult) {
      this.fitMapToRoute(this.directionsResult);
    } else {
      this.mapZoom = 6;
      this.mapCenter = { lat: 30.3753, lng: 69.3451 };
    }
  }

  toggleMapFullscreen(): void {
    const el = document.querySelector('.route-map-panel');
    if (!el) return;
    if (!document.fullscreenElement) {
      el.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
  }

  confirmLeave(target: string[] = ['/routes']): void {
    if (!this.formDirty || this.allowNavigate) {
      void this.router.navigate(target);
      return;
    }
    if (window.confirm('You have unsaved changes. Leave this page?')) {
      this.allowNavigate = true;
      void this.router.navigate(target);
    }
  }

  private attachAutocomplete(): void {
    if (!this.originInput || !this.destinationInput) return;
    if (typeof google === 'undefined' || !google.maps?.places?.Autocomplete) return;

    const options: google.maps.places.AutocompleteOptions = {
      fields: ['formatted_address', 'name', 'geometry'],
      types: ['geocode'],
      componentRestrictions: { country: 'pk' }
    };

    this.originAutocomplete = new google.maps.places.Autocomplete(
      this.originInput.nativeElement,
      options
    );
    this.destinationAutocomplete = new google.maps.places.Autocomplete(
      this.destinationInput.nativeElement,
      options
    );

    this.placesListeners.push(
      this.originAutocomplete.addListener('place_changed', () =>
        this.onPlaceSelected('source', this.originAutocomplete!)
      )
    );
    this.placesListeners.push(
      this.destinationAutocomplete.addListener('place_changed', () =>
        this.onPlaceSelected('destination', this.destinationAutocomplete!)
      )
    );
  }

  private attachStopAutocompletes(): void {
    if (!this.stopInputs?.length) return;
    if (typeof google === 'undefined' || !google.maps?.places?.Autocomplete) return;

    this.stopListeners.forEach(l => l.remove());
    this.stopListeners = [];
    this.stopAutocompletes = [];

    const options: google.maps.places.AutocompleteOptions = {
      fields: ['formatted_address', 'name', 'geometry'],
      types: ['geocode'],
      componentRestrictions: { country: 'pk' }
    };

    this.stopInputs.forEach((ref, index) => {
      const ac = new google.maps.places.Autocomplete(ref.nativeElement, options);
      this.stopAutocompletes.push(ac);
      this.stopListeners.push(
        ac.addListener('place_changed', () => {
          const place = ac.getPlace();
          const label = place?.formatted_address || place?.name || '';
          this.zone.run(() => {
            this.stops[index] = label;
            this.formDirty = true;
            this.scheduleRecompute();
            this.cdr.markForCheck();
          });
        })
      );
    });
  }

  private onPlaceSelected(field: 'source' | 'destination', ac: google.maps.places.Autocomplete): void {
    const place = ac.getPlace();
    const label = place?.formatted_address || place?.name || '';
    const loc = place?.geometry?.location;
    this.zone.run(() => {
      this.form.get(field)!.setValue(label);
      if (loc) {
        this.upsertInterimMarker(field, {
          lat: loc.lat(),
          lng: loc.lng()
        });
      }
      this.scheduleRecompute();
    });
  }

  private upsertInterimMarker(field: 'source' | 'destination', position: google.maps.LatLngLiteral): void {
    const role: MapPinRole = field === 'source' ? 'origin' : 'destination';
    const title = field === 'source' ? 'Origin' : 'Destination';
    const label = field === 'source' ? 'A' : 'B';
    this.mapMarkers = this.mapMarkers.filter(m => m.role !== role);
    this.mapMarkers = [...this.mapMarkers, { position, title, label, role }];
    this.mapCenter = position;
    this.mapZoom = 10;
    this.cdr.markForCheck();
  }

  markerOptions(role: MapPinRole): google.maps.MarkerOptions {
    const colors: Record<MapPinRole, string> = {
      origin: '#10B981',
      stop: '#F59E0B',
      destination: '#EF4444'
    };
    return {
      animation: google.maps.Animation?.DROP,
      label: undefined,
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 11,
        fillColor: colors[role],
        fillOpacity: 1,
        strokeColor: '#ffffff',
        strokeWeight: 2.5
      },
      zIndex: role === 'destination' ? 3 : role === 'origin' ? 2 : 1
    };
  }

  scheduleRecompute(): void {
    if (this.recomputeTimer) clearTimeout(this.recomputeTimer);
    this.recomputeTimer = setTimeout(() => this.computeRoute(), 450);
  }

  private computeRoute(): void {
    if (!this.mapsReady) return;

    const source = (this.form.get('source')?.value || '').trim();
    const destination = (this.form.get('destination')?.value || '').trim();
    if (!source || !destination) {
      this.directionsResult = null;
      this.routeLineActive = false;
      this.cdr.markForCheck();
      return;
    }
    if (source.toLowerCase() === destination.toLowerCase()) {
      this.mapsError = 'Origin and destination must be different.';
      this.directionsResult = null;
      this.routeLineActive = false;
      this.cdr.markForCheck();
      return;
    }

    this.calculating = true;
    this.mapsError = null;
    this.cdr.markForCheck();

    this.directionsSub?.unsubscribe();

    const waypoints = this.stops
      .map(s => s.trim())
      .filter(Boolean)
      .map(location => ({ location, stopover: true }));

    const request: google.maps.DirectionsRequest = {
      origin: source,
      destination,
      waypoints: waypoints.length ? waypoints : undefined,
      optimizeWaypoints: waypoints.length > 1,
      travelMode: google.maps.TravelMode.DRIVING,
      region: 'PK',
      provideRouteAlternatives: this.optimizeMode === 'fastest' || this.optimizeMode === 'efficient',
      drivingOptions: {
        departureTime: new Date(),
        trafficModel: google.maps.TrafficModel.BEST_GUESS
      }
    };

    if (this.optimizeMode === 'no_tolls') {
      request.avoidTolls = true;
    }

    this.directionsSub = this.directionsService.route(request).subscribe({
      next: ({ status, result }) => {
        this.calculating = false;

        if (status !== 'OK' || !result?.routes?.length) {
          this.mapsError = `Could not find a driving route (${status}). Check locations.`;
          this.directionsResult = null;
          this.routeLineActive = false;
          this.cdr.markForCheck();
          return;
        }

        const selected = this.pickRoute(result);
        const selectedResult: google.maps.DirectionsResult = {
          ...result,
          routes: [selected]
        };

        this.directionsResult = selectedResult;
        this.updateMarkersFromDirections(selectedResult);
        this.buildRoutePathLabels(source, destination, waypoints.length);
        this.fitMapToRoute(selectedResult);
        this.routeLineActive = true;

        let totalMeters = 0;
        let totalSeconds = 0;
        let trafficSeconds = 0;
        let hasTraffic = false;
        selected.legs.forEach(leg => {
          totalMeters += leg.distance?.value ?? 0;
          totalSeconds += leg.duration?.value ?? 0;
          if (leg.duration_in_traffic?.value != null) {
            hasTraffic = true;
            trafficSeconds += leg.duration_in_traffic.value;
          } else {
            trafficSeconds += leg.duration?.value ?? 0;
          }
        });

        const km = Math.round(totalMeters / 100) / 10;
        const minutes = Math.max(1, Math.round((hasTraffic ? trafficSeconds : totalSeconds) / 60));
        const basePrice = this.calculateBasePrice(km);
        this.trafficLevel = hasTraffic
          ? this.deriveTrafficLevel(totalSeconds, trafficSeconds)
          : 'unknown';

        this.computedDistanceText = `${km} km`;
        this.computedDurationText = `${minutes} min`;
        this.computedBasePriceText = `PKR ${basePrice.toLocaleString('en-PK')}`;

        if (!this.form.get('name')?.value?.trim()) {
          this.form.patchValue({ name: this.routePathLabels.join(' → ') }, { emitEvent: false });
        }

        this.form.patchValue(
          { distance: km, estimatedMinutes: minutes, basePrice },
          { emitEvent: false }
        );
        this.flashMetricsUpdated();
        this.cdr.markForCheck();
      },
      error: () => {
        this.calculating = false;
        this.mapsError = 'Could not compute the route.';
        this.cdr.markForCheck();
      }
    });
  }

  private pickRoute(result: google.maps.DirectionsResult): google.maps.DirectionsRoute {
    const routes = result.routes;
    if (routes.length === 1) return routes[0];

    const score = (route: google.maps.DirectionsRoute) => {
      let meters = 0;
      let seconds = 0;
      route.legs.forEach(leg => {
        meters += leg.distance?.value ?? 0;
        seconds += leg.duration_in_traffic?.value ?? leg.duration?.value ?? 0;
      });
      return { meters, seconds };
    };

    if (this.optimizeMode === 'fastest') {
      return [...routes].sort((a, b) => score(a).seconds - score(b).seconds)[0];
    }
    if (this.optimizeMode === 'efficient') {
      return [...routes].sort((a, b) => score(a).meters - score(b).meters)[0];
    }
    return routes[0];
  }

  private updateMarkersFromDirections(result: google.maps.DirectionsResult): void {
    const markers: MapMarkerPoint[] = [];
    const legs = result.routes[0]?.legs ?? [];
    legs.forEach((leg, index) => {
      if (index === 0) {
        markers.push({
          position: leg.start_location.toJSON(),
          title: this.form.get('source')?.value || 'Origin',
          label: 'A',
          role: 'origin'
        });
      }
      const isLast = index === legs.length - 1;
      markers.push({
        position: leg.end_location.toJSON(),
        title: isLast
          ? (this.form.get('destination')?.value || 'Destination')
          : `Stop ${index + 1}`,
        label: isLast ? 'B' : String(index + 1),
        role: isLast ? 'destination' : 'stop'
      });
    });
    this.mapMarkers = markers;
  }

  private buildRoutePathLabels(source: string, destination: string, stopCount: number): void {
    const origin = source.split(',')[0].trim();
    const dest = destination.split(',')[0].trim();
    if (stopCount > 0) {
      this.routePathLabels = [
        origin,
        ...this.stops.filter(s => s.trim()).map((s, i) => s.split(',')[0] || `Stop ${i + 1}`),
        dest
      ];
    } else {
      this.routePathLabels = [origin, dest];
    }
  }

  private deriveTrafficLevel(baseSec: number, trafficSec: number): TrafficLevel {
    if (baseSec <= 0) return 'unknown';
    const delayRatio = trafficSec / baseSec;
    if (delayRatio > 1.25) return 'heavy';
    if (delayRatio > 1.08) return 'moderate';
    return 'clear';
  }

  private flashMetricsUpdated(): void {
    this.metricsUpdated = true;
    setTimeout(() => {
      this.metricsUpdated = false;
      this.cdr.markForCheck();
    }, 1200);
  }

  private fitMapToRoute(result: google.maps.DirectionsResult): void {
    setTimeout(() => {
      const map = this.googleMap?.googleMap;
      if (!map || !result.routes?.[0]) return;
      const bounds = new google.maps.LatLngBounds();
      result.routes[0].legs.forEach(leg => {
        bounds.extend(leg.start_location);
        bounds.extend(leg.end_location);
      });
      map.fitBounds(bounds, { top: 72, right: 56, bottom: 56, left: 56 });
    }, 200);
  }

  private calculateBasePrice(distanceKm: number): number {
    if (!distanceKm || distanceKm <= 0) return 0;
    const flagfall = 500;
    const tier1Cap = 100;
    const tier2Cap = 500;
    let price = flagfall;
    price += Math.min(distanceKm, tier1Cap) * 35;
    if (distanceKm > tier1Cap) {
      price += Math.min(distanceKm - tier1Cap, tier2Cap - tier1Cap) * 20;
    }
    if (distanceKm > tier2Cap) {
      price += (distanceKm - tier2Cap) * 15;
    }
    if (this.optimizeMode === 'efficient') {
      price = Math.round(price * 0.97);
    }
    return Math.ceil(price / 100) * 100;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.form.hasError('sameEndpoints')) {
        this.toast.warning('Origin and destination must be different.');
      } else if (this.form.get('distance')?.invalid) {
        this.toast.warning('Enter distance or preview the route on the map first.');
      }
      return;
    }
    this.loading = true;
    const f = this.form.value;
    const waypointsJson = serializeRouteWaypoints(this.stops);

    const dto: CreateRouteDto = {
      name: f.name?.trim() || null,
      source: f.source.trim(),
      destination: f.destination.trim(),
      distance: Number(f.distance),
      estimatedMinutes: f.estimatedMinutes != null ? Number(f.estimatedMinutes) : null,
      basePrice: Number(f.basePrice ?? 0),
      waypointsJson,
      optimizeMode: this.optimizeMode
    };

    const obs: Observable<unknown> = this.isEdit
      ? this.routeService.update({
          id: this.routeId!,
          route: { ...dto, isActive: !!f.isActive } as UpdateRouteDto
        } as UpdateRouteRequest)
      : this.routeService.create({ route: dto } as CreateRouteRequest);

    obs.subscribe({
      next: () => {
        localStorage.removeItem(DRAFT_KEY);
        this.allowNavigate = true;
        this.formDirty = false;
        this.toast.success(`Route ${this.isEdit ? 'updated' : 'created'}.`);
        this.router.navigate(['/routes']);
      },
      error: err => {
        this.loading = false;
        this.toast.error(this.extractError(err));
      }
    });
  }

  private extractError(err: unknown): string {
    const body = (err as { error?: { errors?: Record<string, unknown>; message?: string; title?: string } })
      ?.error;
    if (body?.errors) {
      const messages: string[] = [];
      for (const key of Object.keys(body.errors)) {
        const val = body.errors[key];
        if (Array.isArray(val)) messages.push(...(val as string[]));
        else if (typeof val === 'string') messages.push(val);
      }
      if (messages.length) return messages.join(' ');
    }
    return body?.message || body?.title || 'Operation failed';
  }
}
