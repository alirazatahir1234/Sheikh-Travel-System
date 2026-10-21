import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TripService } from '../../../core/services/trip.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import {
  TripDashboard,
  TripListItem,
  TripStatus,
  formatTripStatusLabel
} from '../../../core/models/trip.model';
import { VehicleLocation } from '../../../core/models/gps-tracking.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface MapTripMarker {
  trip: TripListItem;
  position: google.maps.LatLngLiteral;
  title: string;
  options: google.maps.MarkerOptions;
}

@Component({
  standalone: false,
  selector: 'app-trip-dashboard',
  templateUrl: './trip-dashboard.component.html',
  styleUrls: ['./trip-dashboard.component.scss']
})
export class TripDashboardComponent implements OnInit, OnDestroy {
  loading = true;
  mapLoading = true;
  error: string | null = null;
  mapError: string | null = null;
  stats: TripDashboard | null = null;
  liveTrips: TripListItem[] = [];
  mapMarkers: MapTripMarker[] = [];
  selectedTripId: number | null = null;
  mapsReady = false;
  mapsConfigured = false;

  mapCenter: google.maps.LatLngLiteral = { lat: 24.8607, lng: 67.0011 };
  mapZoom = 11;
  readonly mapOptions: google.maps.MapOptions = {
    disableDefaultUI: false,
    zoomControl: true,
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: true,
    gestureHandling: 'greedy'
  };

  readonly cards: Array<{
    key: keyof TripDashboard;
    label: string;
    icon: string;
    filter?: string;
    statusGroup?: 'scheduled' | 'ongoing';
  }> = [
    { key: 'totalTrips', label: 'Total Trips', icon: 'map' },
    { key: 'scheduledTrips', label: 'Scheduled', icon: 'event', statusGroup: 'scheduled' },
    { key: 'ongoingTrips', label: 'Ongoing', icon: 'directions_car', statusGroup: 'ongoing' },
    { key: 'completedTrips', label: 'Completed', icon: 'check_circle', filter: 'Completed' },
    { key: 'cancelledTrips', label: 'Cancelled', icon: 'cancel', filter: 'Cancelled' },
    { key: 'delayedTrips', label: 'Delayed', icon: 'schedule', filter: 'Delayed' },
    { key: 'todaysTrips', label: "Today's Trips", icon: 'today' },
    { key: 'upcomingTrips', label: 'Upcoming', icon: 'upcoming' }
  ];

  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private trips: TripService,
    private gps: GpsTrackingService,
    private mapsLoader: GoogleMapsLoaderService,
    private router: Router,
    private toast: UiToastService
  ) {
    this.mapsConfigured = this.mapsLoader.isConfigured;
  }

  get mappedTripCount(): number {
    return this.mapMarkers.length;
  }

  ngOnInit(): void {
    void this.initMaps();
    this.load();
    this.refreshTimer = setInterval(() => this.loadMapData(true), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.trips.getDashboard().subscribe({
      next: stats => {
        this.stats = stats;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.stats = null;
        this.error = apiErrorMessage(err, 'Failed to load trip dashboard.');
        this.toast.error(this.error);
      }
    });
    this.loadMapData();
  }

  openCard(card: {
    filter?: string;
    statusGroup?: 'scheduled' | 'ongoing';
    key: keyof TripDashboard;
  }): void {
    if (card.key === 'todaysTrips') {
      this.router.navigate(['/trips/list'], { queryParams: { todayOnly: true } });
      return;
    }
    if (card.key === 'upcomingTrips') {
      this.router.navigate(['/trips/list'], { queryParams: { upcoming: true } });
      return;
    }
    if (card.statusGroup) {
      this.router.navigate(['/trips/list'], { queryParams: { statusGroup: card.statusGroup } });
      return;
    }
    this.router.navigate(['/trips/list'], {
      queryParams: card.filter ? { status: card.filter } : {}
    });
  }

  openFullLiveMap(): void {
    this.router.navigate(['/gps-tracking/live']);
  }

  openTrip(trip: TripListItem): void {
    this.router.navigate(['/trips', trip.id]);
  }

  openLive(trip: TripListItem): void {
    if (!trip.vehicleId) {
      this.toast.warning('Assign a vehicle before opening live GPS.');
      return;
    }
    this.router.navigate(['/gps-tracking/live'], {
      queryParams: { tripId: trip.id, vehicleId: trip.vehicleId }
    });
  }

  selectMarker(marker: MapTripMarker): void {
    this.selectedTripId = marker.trip.id;
    this.mapCenter = marker.position;
    this.mapZoom = Math.max(this.mapZoom, 13);
  }

  selectTrip(trip: TripListItem): void {
    this.selectedTripId = trip.id;
    const marker = this.mapMarkers.find(m => m.trip.id === trip.id);
    if (marker) {
      this.mapCenter = marker.position;
      this.mapZoom = Math.max(this.mapZoom, 13);
    }
  }

  formatStatus(status: TripStatus): string {
    return formatTripStatusLabel(status);
  }

  statusTone(status: TripStatus): string {
    const key = String(status || '').toLowerCase().replace(/\s+/g, '');
    if (status === 'Started' || status === 'AtPickup' || status === 'Enroute') {
      return 'ops-status--inprogress';
    }
    return `ops-status--${key}`;
  }

  private async initMaps(): Promise<void> {
    if (!this.mapsConfigured) {
      this.mapError = 'Google Maps is not configured.';
      this.mapLoading = false;
      return;
    }
    try {
      await this.mapsLoader.importLibrary('maps');
      this.mapsReady = !this.mapsLoader.authFailed;
      if (!this.mapsReady) {
        this.mapError = this.mapsLoader.failureMessage || 'Google Maps failed to load.';
      } else {
        this.mapError = null;
      }
    } catch {
      this.mapsReady = false;
      this.mapError = this.mapsLoader.failureMessage || 'Google Maps failed to load.';
    }
  }

  private loadMapData(silent = false): void {
    if (!silent) this.mapLoading = true;
    this.mapError = this.mapsConfigured ? this.mapError : 'Google Maps is not configured.';

    forkJoin({
      trips: this.trips.getLive(false).pipe(catchError(() => of([] as TripListItem[]))),
      locations: this.gps.getAllVehicleLocations().pipe(catchError(() => of([] as VehicleLocation[])))
    }).subscribe({
      next: ({ trips, locations }) => {
        this.liveTrips = trips;
        this.buildMarkers(trips, locations);
        this.mapLoading = false;
        if (!silent && !trips.length) {
          /* empty map is fine */
        }
      },
      error: err => {
        this.mapLoading = false;
        if (!silent) {
          this.mapError = apiErrorMessage(err, 'Failed to load live trip map.');
        }
      }
    });
  }

  private buildMarkers(trips: TripListItem[], locations: VehicleLocation[]): void {
    const byVehicle = new Map(locations.map(l => [l.vehicleId, l]));
    const markers: MapTripMarker[] = [];

    for (const trip of trips) {
      if (!trip.vehicleId) continue;
      const loc = byVehicle.get(trip.vehicleId);
      if (!loc || !Number.isFinite(loc.latitude) || !Number.isFinite(loc.longitude)) continue;
      if (Math.abs(loc.latitude) < 0.01 && Math.abs(loc.longitude) < 0.01) continue;

      const position = { lat: loc.latitude, lng: loc.longitude };
      const title = `${trip.tripNumber} · ${trip.vehicleName || 'Vehicle'} · ${formatTripStatusLabel(trip.status)}`;
      const options: google.maps.MarkerOptions = {
        title,
        label: {
          text: (trip.tripNumber || 'T').slice(-3),
          color: '#0f172a',
          fontWeight: '700',
          fontSize: '10px'
        }
      };
      if (typeof google !== 'undefined' && google.maps?.SymbolPath) {
        options.icon = {
          path: google.maps.SymbolPath.CIRCLE,
          scale: 12,
          fillColor: trip.status === 'Delayed' ? '#c2410c' : '#0f766e',
          fillOpacity: 1,
          strokeColor: '#fff',
          strokeWeight: 2
        };
        options.label = {
          text: trip.status === 'Delayed' ? '!' : '•',
          color: '#fff',
          fontWeight: '700',
          fontSize: '11px'
        };
      }
      markers.push({ trip, position, title, options });
    }

    this.mapMarkers = markers;
    if (markers.length) {
      const avgLat = markers.reduce((s, m) => s + m.position.lat, 0) / markers.length;
      const avgLng = markers.reduce((s, m) => s + m.position.lng, 0) / markers.length;
      this.mapCenter = { lat: avgLat, lng: avgLng };
      this.mapZoom = markers.length === 1 ? 13 : 11;
      if (this.selectedTripId != null && !markers.some(m => m.trip.id === this.selectedTripId)) {
        this.selectedTripId = null;
      }
    }
  }
}
