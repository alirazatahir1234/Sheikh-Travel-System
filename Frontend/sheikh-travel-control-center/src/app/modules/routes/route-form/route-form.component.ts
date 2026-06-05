import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  ViewChild
} from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GoogleMap, MapDirectionsService } from '@angular/google-maps';
import { Observable, Subscription } from 'rxjs';

import { RouteService } from '../../../core/services/route.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import {
  CreateRouteDto,
  CreateRouteRequest,
  UpdateRouteDto,
  UpdateRouteRequest
} from '../../../core/models/route.model';

interface RoutePreset {
  label: string;
  source: string;
  destination: string;
}

type RouteOptimizeMode = 'balanced' | 'fastest' | 'efficient' | 'no_tolls';
type TrafficLevel = 'clear' | 'moderate' | 'heavy';
type MapPinRole = 'origin' | 'stop' | 'destination';

interface MapMarkerPoint {
  position: google.maps.LatLngLiteral;
  title: string;
  label: string;
  role: MapPinRole;
}

const DARK_MAP_STYLES: google.maps.MapTypeStyle[] = [
  { elementType: 'geometry', stylers: [{ color: '#1d2c4d' }] },
  { elementType: 'labels.text.fill', stylers: [{ color: '#8ec3b9' }] },
  { elementType: 'labels.text.stroke', stylers: [{ color: '#1a3646' }] },
  { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#304a7d' }] },
  { featureType: 'road.highway', elementType: 'geometry', stylers: [{ color: '#2c6675' }] },
  { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0e1626' }] },
  { featureType: 'poi', stylers: [{ visibility: 'off' }] }
];

@Component({
  selector: 'app-route-form',
  templateUrl: './route-form.component.html',
  styleUrls: ['./route-form.component.scss']
})
export class RouteFormComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('originInput') originInput?: ElementRef<HTMLInputElement>;
  @ViewChild('destinationInput') destinationInput?: ElementRef<HTMLInputElement>;
  @ViewChild(GoogleMap) googleMap?: GoogleMap;

  form: FormGroup;
  loading = false;
  isEdit = false;
  routeId: number | null = null;

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
  trafficLevel: TrafficLevel = 'clear';
  metricsUpdated = false;
  routeLineActive = false;

  stops: string[] = [];
  readonly presets: RoutePreset[] = [
    { label: 'Karachi → Lahore', source: 'Karachi, Pakistan', destination: 'Lahore, Pakistan' },
    { label: 'Lahore → Islamabad', source: 'Lahore, Pakistan', destination: 'Islamabad, Pakistan' },
    { label: 'Islamabad → Peshawar', source: 'Islamabad, Pakistan', destination: 'Peshawar, Pakistan' },
    { label: 'Karachi → Hyderabad', source: 'Karachi, Pakistan', destination: 'Hyderabad, Pakistan' },
    { label: 'Multan → Faisalabad', source: 'Multan, Pakistan', destination: 'Faisalabad, Pakistan' }
  ];

  readonly optimizeModes: { id: RouteOptimizeMode; label: string; icon: string }[] = [
    { id: 'balanced', label: 'Balanced', icon: 'route' },
    { id: 'fastest', label: 'Fastest', icon: 'bolt' },
    { id: 'efficient', label: 'Fuel efficient', icon: 'eco' },
    { id: 'no_tolls', label: 'Avoid tolls', icon: 'toll' }
  ];

  mapCenter: google.maps.LatLngLiteral = { lat: 30.3753, lng: 69.3451 };
  mapZoom = 6;
  mapOptions: google.maps.MapOptions = {
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: false,
    zoomControl: true,
    styles: DARK_MAP_STYLES,
    backgroundColor: '#0e1626'
  };
  directionsResult: google.maps.DirectionsResult | null = null;

  get rendererOptions(): google.maps.DirectionsRendererOptions {
    const arrow =
      typeof google !== 'undefined' ? google.maps.SymbolPath.FORWARD_CLOSED_ARROW : 0;
    return {
      suppressMarkers: true,
      preserveViewport: true,
      polylineOptions: {
        strokeColor: '#14B8A6',
        strokeWeight: 7,
        strokeOpacity: 0.92,
        icons: [
          {
            icon: {
              path: arrow,
              scale: 3.2,
              fillColor: '#99f6e4',
              fillOpacity: 1,
              strokeColor: '#0f766e',
              strokeWeight: 1
            },
            offset: '0',
            repeat: '90px'
          }
        ]
      }
    };
  }

  private originAutocomplete: google.maps.places.Autocomplete | null = null;
  private destinationAutocomplete: google.maps.places.Autocomplete | null = null;
  private placesListeners: google.maps.MapsEventListener[] = [];
  private recomputeTimer: ReturnType<typeof setTimeout> | null = null;
  private directionsSub: Subscription | null = null;
  private routeParamSub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private routeService: RouteService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar,
    private mapsLoader: GoogleMapsLoaderService,
    private directionsService: MapDirectionsService,
    private zone: NgZone,
    private cdr: ChangeDetectorRef
  ) {
    this.mapsConfigured = this.mapsLoader.isConfigured;
    this.form = this.fb.group({
      name: ['', [Validators.maxLength(200)]],
      source: ['', [Validators.required, Validators.maxLength(200)]],
      destination: ['', [Validators.required, Validators.maxLength(200)]],
      distance: [null, [Validators.required, Validators.min(0.1)]],
      estimatedMinutes: [null, [Validators.min(1)]],
      basePrice: [0, [Validators.required, Validators.min(0)]],
      isActive: [true]
    });
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
      heavy: 'Heavy traffic'
    };
    return labels[this.trafficLevel];
  }

  get trafficIcon(): string {
    const icons: Record<TrafficLevel, string> = {
      clear: 'traffic',
      moderate: 'warning',
      heavy: 'report'
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

  get formattedDuration(): string {
    const min = Number(this.form.get('estimatedMinutes')?.value);
    if (!min) return '—';
    const h = Math.floor(min / 60);
    const m = min % 60;
    return h > 0 ? `${h}h ${m}m` : `${m} min`;
  }

  ngOnInit(): void {
    this.routeParamSub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.isEdit = true;
        this.routeId = +id;
        this.loadRoute(this.routeId);
      } else {
        this.resetForm();
        this.loadDraft();
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
    this.directionsResult = null;
    this.mapMarkers = [];
    this.routePathLabels = [];
    this.routeLineActive = false;
    this.computedDistanceText = '';
    this.computedDurationText = '';
    this.computedBasePriceText = '';
    this.mapsError = null;
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
        this.computedDistanceText = r.distance ? `${r.distance} km` : '';
        this.computedDurationText = r.estimatedMinutes ? `${r.estimatedMinutes} min` : '';
        this.computedBasePriceText = r.basePrice ? `PKR ${r.basePrice.toLocaleString('en-PK')}` : '';
        if (this.mapsReady && r.source && r.destination) {
          this.scheduleRecompute();
        }
      },
      error: () => this.snackBar.open('Failed to load route.', 'Close', { duration: 3000 })
    });
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.mapsConfigured) return;

    const loaded = await this.mapsLoader.load();
    if (!loaded) {
      this.mapsError = 'Could not load Google Maps. Check your API key in environment.ts.';
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
    this.placesListeners = [];
    this.directionsSub?.unsubscribe();
    this.routeParamSub?.unsubscribe();
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
    this.scheduleRecompute();
  }

  addStop(): void {
    this.stops.push('');
    this.cdr.markForCheck();
  }

  removeStop(index: number): void {
    this.stops.splice(index, 1);
    this.scheduleRecompute();
  }

  onStopBlur(): void {
    this.scheduleRecompute();
  }

  saveDraft(): void {
    const payload = { form: this.form.getRawValue(), stops: this.stops };
    localStorage.setItem('stb-route-draft', JSON.stringify(payload));
    this.snackBar.open('Draft saved locally.', 'Close', { duration: 2000 });
  }

  private loadDraft(): void {
    try {
      const raw = localStorage.getItem('stb-route-draft');
      if (!raw) return;
      const { form, stops } = JSON.parse(raw);
      if (form) this.form.patchValue(form);
      if (Array.isArray(stops)) this.stops = stops;
    } catch {
      /* ignore */
    }
  }

  previewRoute(): void {
    if (!this.form.get('source')?.value || !this.form.get('destination')?.value) {
      this.snackBar.open('Enter origin and destination first.', 'Close', { duration: 2500 });
      return;
    }
    this.computeRoute();
  }

  toggleMapType(): void {
    this.mapTypeSatellite = !this.mapTypeSatellite;
    const map = this.googleMap?.googleMap;
    if (map) {
      map.setMapTypeId(this.mapTypeSatellite ? 'hybrid' : 'roadmap');
      if (!this.mapTypeSatellite) {
        map.setOptions({ styles: DARK_MAP_STYLES });
      } else {
        map.setOptions({ styles: [] });
      }
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

  private attachAutocomplete(): void {
    if (!this.originInput || !this.destinationInput) return;
    if (typeof google === 'undefined' || !google.maps?.places?.Autocomplete) return;

    const options: google.maps.places.AutocompleteOptions = {
      fields: ['formatted_address', 'name', 'geometry'],
      types: ['geocode']
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
      travelMode: 'DRIVING' as google.maps.TravelMode,
      region: 'PK',
      drivingOptions: {
        departureTime: new Date()
      }
    };

    if (this.optimizeMode === 'no_tolls') {
      request.avoidTolls = true;
    }
    if (this.optimizeMode === 'efficient') {
      request.avoidHighways = false;
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

        this.directionsResult = result;
        this.updateMarkersFromDirections(result);
        this.buildRoutePathLabels(source, destination, waypoints.length);
        this.fitMapToRoute(result);
        this.routeLineActive = true;

        let totalMeters = 0;
        let totalSeconds = 0;
        let trafficSeconds = 0;
        result.routes[0].legs.forEach(leg => {
          totalMeters += leg.distance?.value ?? 0;
          totalSeconds += leg.duration?.value ?? 0;
          trafficSeconds += leg.duration_in_traffic?.value ?? leg.duration?.value ?? 0;
        });

        const km = Math.round(totalMeters / 100) / 10;
        const minutes = Math.max(1, Math.round(totalSeconds / 60));
        const basePrice = this.calculateBasePrice(km);
        this.trafficLevel = this.deriveTrafficLevel(km, totalSeconds, trafficSeconds);

        this.computedDistanceText = `${km} km`;
        this.computedDurationText = `${minutes} min`;
        this.computedBasePriceText = `PKR ${basePrice.toLocaleString('en-PK')}`;

        if (!this.form.get('name')?.value?.trim()) {
          this.form.patchValue({ name: this.routePathLabels.join(' → ') });
        }

        this.form.patchValue({ distance: km, estimatedMinutes: minutes, basePrice });
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
      this.routePathLabels = [origin, ...this.stops.filter(s => s.trim()).map((s, i) => s.split(',')[0] || `Stop ${i + 1}`), dest];
    } else {
      this.routePathLabels = [origin, dest];
    }
  }

  private deriveTrafficLevel(km: number, baseSec: number, trafficSec: number): TrafficLevel {
    const hours = baseSec / 3600;
    if (hours <= 0 || km <= 0) return 'clear';
    const avgKmh = km / hours;
    const delayRatio = trafficSec > 0 ? trafficSec / baseSec : 1;
    if (delayRatio > 1.25 || avgKmh < 35) return 'heavy';
    if (delayRatio > 1.08 || avgKmh < 50) return 'moderate';
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
      map.fitBounds(bounds, { top: 80, right: 48, bottom: 120, left: 320 });
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
      return;
    }
    this.loading = true;
    const f = this.form.value;

    const dto: CreateRouteDto = {
      name: f.name?.trim() || null,
      source: f.source.trim(),
      destination: f.destination.trim(),
      distance: Number(f.distance),
      estimatedMinutes: f.estimatedMinutes != null ? Number(f.estimatedMinutes) : null,
      basePrice: Number(f.basePrice ?? 0)
    };

    const obs: Observable<unknown> = this.isEdit
      ? this.routeService.update({
          id: this.routeId!,
          route: { ...dto, isActive: !!f.isActive } as UpdateRouteDto
        } as UpdateRouteRequest)
      : this.routeService.create({ route: dto } as CreateRouteRequest);

    obs.subscribe({
      next: () => {
        localStorage.removeItem('stb-route-draft');
        this.snackBar.open(`Route ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/routes']);
      },
      error: err => {
        this.loading = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
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
