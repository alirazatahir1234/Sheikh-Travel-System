/// <reference types="google.maps" />
import { DecimalPipe } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  OnChanges,
  signal,
  ViewChild
} from '@angular/core';
import { GoogleMapsLoaderService } from '../../core/services/google-maps-loader.service';

export interface TripMapPoint {
  lat: number;
  lng: number;
  label: string;
}

@Component({
  selector: 'app-trip-map',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-2">
      @if (!maps.hasApiKey()) {
        <p class="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-950">
          Add <code class="font-mono">googleMapsApiKey</code> to preview the route on the map.
        </p>
      } @else if (loadError()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">{{ loadError() }}</p>
      }
      <div #mapEl class="h-56 w-full overflow-hidden rounded-2xl border border-stroke bg-surface-alt md:h-64"></div>
      @if (distanceKm() != null) {
        <p class="text-xs text-slate-600">
          <strong>{{ distanceKm() | number : '1.1-1' }} km</strong>
          @if (durationMin() != null) {
            · ~{{ durationMin() }} min drive
          }
        </p>
      }
    </div>
  `
})
export class TripMapComponent implements AfterViewInit, OnChanges {
  readonly maps = inject(GoogleMapsLoaderService);
  readonly pickup = input<TripMapPoint | null>(null);
  readonly dropoff = input<TripMapPoint | null>(null);

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  readonly loadError = signal<string | null>(null);
  readonly distanceKm = signal<number | null>(null);
  readonly durationMin = signal<number | null>(null);

  private map?: google.maps.Map;
  private directionsRenderer?: google.maps.DirectionsRenderer;

  ngAfterViewInit(): void {
    void this.render();
  }

  ngOnChanges(): void {
    void this.render();
  }

  private async render(): Promise<void> {
    const p = this.pickup();
    const d = this.dropoff();
    if (!p || !d || !this.mapEl) return;

    this.loadError.set(null);
    try {
      const g = await this.maps.load();
      if (!this.map) {
        this.map = new g.maps.Map(this.mapEl.nativeElement, {
          center: { lat: p.lat, lng: p.lng },
          zoom: 8,
          disableDefaultUI: true
        });
        this.directionsRenderer = new g.maps.DirectionsRenderer({ suppressMarkers: false });
        this.directionsRenderer.setMap(this.map);
      }

      const service = new g.maps.DirectionsService();
      service.route(
        {
          origin: { lat: p.lat, lng: p.lng },
          destination: { lat: d.lat, lng: d.lng },
          travelMode: g.maps.TravelMode.DRIVING
        },
        (result, status) => {
          if (status !== 'OK' || !result) {
            this.loadError.set('Could not draw route on the map.');
            return;
          }
          this.directionsRenderer?.setDirections(result);
          const leg = result.routes[0]?.legs[0];
          if (leg?.distance?.value) this.distanceKm.set(leg.distance.value / 1000);
          if (leg?.duration?.value) this.durationMin.set(Math.round(leg.duration.value / 60));
        }
      );
    } catch {
      this.loadError.set('Map failed to load.');
    }
  }
}
