/// <reference types="google.maps" />
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  Input,
  OnChanges,
  signal,
  ViewChild
} from '@angular/core';
import { GoogleMapsLoaderService } from '../../core/services/google-maps-loader.service';

@Component({
  selector: 'app-route-preview-map',
  standalone: true,
  template: `
    <div class="space-y-2">
      @if (!maps.hasApiKey()) {
        <p class="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-950">
          Add <code class="rounded bg-white/80 px-1 font-mono">googleMapsApiKey</code> in your environment file to
          preview this route on Google Maps (Directions).
        </p>
      } @else if (loadError()) {
        <p class="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">{{ loadError() }}</p>
      }
      <div
        #mapEl
        class="h-52 w-full overflow-hidden rounded-2xl border border-stroke bg-surface-alt md:h-64"
        [class.opacity-60]="busy()"
      ></div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoutePreviewMapComponent implements AfterViewInit, OnChanges {
  readonly maps = inject(GoogleMapsLoaderService);

  @Input() source = '';
  @Input() destination = '';

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  readonly busy = signal(false);
  readonly loadError = signal<string | null>(null);

  private map?: google.maps.Map;
  private directionsRenderer?: google.maps.DirectionsRenderer;

  ngAfterViewInit(): void {
    void this.render();
  }

  ngOnChanges(): void {
    void this.render();
  }

  private async render(): Promise<void> {
    if (!this.maps.hasApiKey()) {
      this.loadError.set(null);
      this.busy.set(false);
      return;
    }
    const s = this.source?.trim();
    const d = this.destination?.trim();
    if (!s || !d) {
      this.loadError.set(null);
      return;
    }
    const el = this.mapEl?.nativeElement;
    if (!el) {
      return;
    }
    this.busy.set(true);
    this.loadError.set(null);
    try {
      await this.maps.load();
      if (!this.map) {
        this.map = new google.maps.Map(el, {
          zoom: 7,
          center: { lat: 33.6844, lng: 73.0479 },
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: true
        });
        this.directionsRenderer = new google.maps.DirectionsRenderer({ map: this.map });
      }
      const directions = new google.maps.DirectionsService();
      directions.route(
        {
          origin: s,
          destination: d,
          travelMode: google.maps.TravelMode.DRIVING
        },
        (result: google.maps.DirectionsResult | null, status: string) => {
          this.busy.set(false);
          if (status !== 'OK' || !result) {
            this.loadError.set('Could not plot driving directions for this route.');
            return;
          }
          this.loadError.set(null);
          this.directionsRenderer?.setDirections(result);
        }
      );
    } catch (e) {
      this.busy.set(false);
      this.loadError.set(e instanceof Error ? e.message : 'Could not load Google Maps.');
    }
  }
}
