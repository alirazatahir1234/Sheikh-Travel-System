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
import { MapDirectionsService } from '@angular/google-maps';
import { Observable, Subscription } from 'rxjs';

import { RouteService } from '../../../core/services/route.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import {
  CreateRouteDto,
  CreateRouteRequest,
  UpdateRouteDto,
  UpdateRouteRequest
} from '../../../core/models/route.model';

@Component({
  selector: 'app-route-form',
  templateUrl: './route-form.component.html',
  styleUrls: ['./route-form.component.scss']
})
export class RouteFormComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('originInput') originInput?: ElementRef<HTMLInputElement>;
  @ViewChild('destinationInput') destinationInput?: ElementRef<HTMLInputElement>;

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

  // <google-map> bindings
  mapCenter: google.maps.LatLngLiteral = { lat: 30.3753, lng: 69.3451 }; // Pakistan
  mapZoom = 5;
  mapOptions: google.maps.MapOptions = {
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: false
  };
  directionsResult: google.maps.DirectionsResult | null = null;
  rendererOptions: google.maps.DirectionsRendererOptions = {
    suppressMarkers: false,
    polylineOptions: { strokeColor: '#1B7F75', strokeWeight: 5 }
  };

  private originAutocomplete: google.maps.places.Autocomplete | null = null;
  private destinationAutocomplete: google.maps.places.Autocomplete | null = null;
  private placesListeners: google.maps.MapsEventListener[] = [];
  private recomputeTimer: ReturnType<typeof setTimeout> | null = null;
  private directionsSub: Subscription | null = null;

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

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.routeId = +id;
      this.routeService.getById(this.routeId).subscribe({
        next: (r) => this.form.patchValue({
          name: r.name ?? '',
          source: r.source,
          destination: r.destination,
          distance: r.distance,
          estimatedMinutes: r.estimatedMinutes ?? null,
          basePrice: r.basePrice ?? 0,
          isActive: r.isActive
        }),
        error: () => this.snackBar.open('Failed to load route.', 'Close', { duration: 3000 })
      });
    }
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.mapsConfigured) return;

    const loaded = await this.mapsLoader.load();
    if (!loaded) {
      this.mapsError = 'Could not load Google Maps. Check your API key and the enabled services in Google Cloud Console.';
      this.cdr.markForCheck();
      return;
    }

    this.zone.run(() => {
      this.mapsReady = true;
      this.cdr.markForCheck();
    });

    // Attach Places Autocomplete once the inputs and SDK are both ready.
    try {
      await this.mapsLoader.importLibrary('places');
      await this.mapsLoader.importLibrary('routes');
      this.zone.run(() => this.attachAutocomplete());
    } catch {
      this.mapsError = 'Could not load Google Maps libraries (places/routes).';
      this.cdr.markForCheck();
    }
  }

  ngOnDestroy(): void {
    if (this.recomputeTimer) clearTimeout(this.recomputeTimer);
    this.placesListeners.forEach((l) => l.remove());
    this.placesListeners = [];
    this.directionsSub?.unsubscribe();
  }

  private attachAutocomplete(): void {
    if (!this.originInput || !this.destinationInput) return;
    if (typeof google === 'undefined' || !google.maps?.places?.Autocomplete) return;

    const options: google.maps.places.AutocompleteOptions = {
      fields: ['formatted_address', 'name', 'geometry'],
      types: ['geocode']
    };

    this.originAutocomplete = new google.maps.places.Autocomplete(
      this.originInput.nativeElement, options
    );
    this.destinationAutocomplete = new google.maps.places.Autocomplete(
      this.destinationInput.nativeElement, options
    );

    this.placesListeners.push(
      this.originAutocomplete.addListener('place_changed',
        () => this.onPlaceSelected('source', this.originAutocomplete!))
    );
    this.placesListeners.push(
      this.destinationAutocomplete.addListener('place_changed',
        () => this.onPlaceSelected('destination', this.destinationAutocomplete!))
    );
  }

  private onPlaceSelected(field: 'source' | 'destination', ac: google.maps.places.Autocomplete): void {
    const place = ac.getPlace();
    const label = place?.formatted_address || place?.name || '';
    this.zone.run(() => {
      this.form.get(field)!.setValue(label);
      this.scheduleRecompute();
    });
  }

  /** Debounced distance/duration recompute so we don't spam the API while typing. */
  scheduleRecompute(): void {
    if (this.recomputeTimer) clearTimeout(this.recomputeTimer);
    this.recomputeTimer = setTimeout(() => this.computeRoute(), 400);
  }

  private computeRoute(): void {
    if (!this.mapsReady) return;

    const source = (this.form.get('source')?.value || '').trim();
    const destination = (this.form.get('destination')?.value || '').trim();
    if (!source || !destination) return;

    this.calculating = true;
    this.mapsError = null;
    this.cdr.markForCheck();

    this.directionsSub?.unsubscribe();
    const request: google.maps.DirectionsRequest = {
      origin: source,
      destination,
      // String literal is accepted by the Directions API and avoids depending on
      // the google.maps.TravelMode enum, which is only present after the
      // 'routes' library has been imported.
      travelMode: 'DRIVING' as google.maps.TravelMode,
      region: 'PK'
    };

    this.directionsSub = this.directionsService.route(request).subscribe({
      next: ({ status, result }) => {
        this.calculating = false;

        if (status !== 'OK' || !result?.routes?.length) {
          this.mapsError = `Could not find a driving route (${status}).`;
          this.cdr.markForCheck();
          return;
        }

        this.directionsResult = result;

        const leg = result.routes[0].legs[0];
        const km = Math.round((leg.distance?.value ?? 0) / 100) / 10;
        const minutes = Math.round((leg.duration?.value ?? 0) / 60);
        const basePrice = this.calculateBasePrice(km);

        this.computedDistanceText = leg.distance?.text ?? '';
        this.computedDurationText = leg.duration?.text ?? '';
        this.computedBasePriceText = `Suggested: PKR ${basePrice.toLocaleString('en-PK')} (auto-calculated from distance)`;

        this.form.patchValue({ distance: km, estimatedMinutes: minutes, basePrice });
        this.form.get('distance')?.markAsDirty();
        this.form.get('estimatedMinutes')?.markAsDirty();
        this.form.get('basePrice')?.markAsDirty();
        this.cdr.markForCheck();
      },
      error: () => {
        this.calculating = false;
        this.mapsError = 'Could not compute the route.';
        this.cdr.markForCheck();
      }
    });
  }

  /**
   * Auto-calculates a sensible base price (PKR) from the route distance.
   *
   * Pricing model:
   *   - 500 PKR flagfall (minimum starting charge)
   *   - Tapered per-km rate so longer routes get cheaper per-km (matches real-world
   *     intercity pricing and the values used by the seed data):
   *       first 100 km  → 35 PKR/km
   *       next  400 km  → 20 PKR/km
   *       beyond 500 km → 15 PKR/km
   *   - Final number rounded up to the nearest 100 PKR for clean quoting.
   *
   * The user can still override the result manually before submitting.
   */
  private calculateBasePrice(distanceKm: number): number {
    if (!distanceKm || distanceKm <= 0) return 0;

    const flagfall = 500;
    const tier1Cap = 100;  // km
    const tier2Cap = 500;  // km
    const tier1Rate = 35;  // PKR/km
    const tier2Rate = 20;  // PKR/km
    const tier3Rate = 15;  // PKR/km

    let price = flagfall;
    const tier1 = Math.min(distanceKm, tier1Cap);
    price += tier1 * tier1Rate;

    if (distanceKm > tier1Cap) {
      const tier2 = Math.min(distanceKm - tier1Cap, tier2Cap - tier1Cap);
      price += tier2 * tier2Rate;
    }
    if (distanceKm > tier2Cap) {
      const tier3 = distanceKm - tier2Cap;
      price += tier3 * tier3Rate;
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
        this.snackBar.open(`Route ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/routes']);
      },
      error: (err) => {
        this.loading = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
      }
    });
  }

  private extractError(err: unknown): string {
    const body = (err as { error?: { errors?: Record<string, unknown>; message?: string; title?: string } })?.error;
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
