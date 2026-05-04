import { Component, OnInit, OnDestroy, AfterViewInit } from '@angular/core';
import { Router } from '@angular/router';
import * as L from 'leaflet';
import { TrackingService } from '../../core/services/tracking.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { VehicleLocation, TrackingDto } from '../../core/models/tracking.model';
import { Vehicle } from '../../core/models/vehicle.model';

// Fix Leaflet default icon path issue with Angular
const iconDefault = L.icon({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34]
});
L.Marker.prototype.options.icon = iconDefault;

@Component({
  selector: 'app-tracking',
  templateUrl: './tracking.component.html',
  styleUrls: ['./tracking.component.scss']
})
export class TrackingComponent implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  private markers = new Map<number, L.Marker>();
  locations: VehicleLocation[] = [];
  loading = true;
  private refreshInterval?: ReturnType<typeof setInterval>;

  // History panel
  vehicles: Vehicle[] = [];
  selectedVehicleId: number | null = null;
  historyFrom = new Date(new Date().setHours(0, 0, 0, 0)).toISOString().slice(0, 16);
  historyTo = new Date().toISOString().slice(0, 16);
  historyRows: TrackingDto[] = [];
  loadingHistory = false;
  historyError = '';
  historyCols = ['timestamp', 'latitude', 'longitude', 'speed'];
  showHistory = false;

  constructor(
    private trackingService: TrackingService,
    private vehicleService: VehicleService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; },
      error: () => {}
    });
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.loadLocations();
    this.refreshInterval = setInterval(() => this.loadLocations(), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    if (this.map) this.map.remove();
  }

  private initMap(): void {
    this.map = L.map('tracking-map', {
      center: [30.3753, 69.3451], // Pakistan center
      zoom: 6
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap contributors'
    }).addTo(this.map);
  }

  error: string | null = null;

  private loadLocations(): void {
    this.trackingService.getAllVehicleLocations().subscribe({
      next: locs => {
        this.locations = locs;
        this.loading = false;
        this.error = null;
        this.updateMarkers(locs);
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load tracking data. No vehicles are currently being tracked.';
        this.locations = [];
        this.updateMarkers([]);
      }
    });
  }

  private updateMarkers(locs: VehicleLocation[]): void {
    const currentIds = new Set(locs.map(l => l.vehicleId));

    this.markers.forEach((marker, vehicleId) => {
      if (!currentIds.has(vehicleId)) {
        marker.remove();
        this.markers.delete(vehicleId);
      }
    });

    locs.forEach(loc => {
      const popupContent = `
        <div style="min-width:150px">
          <b>${loc.vehicleName}</b><br>
          ${loc.registrationNumber}<br>
          <small>Last: ${new Date(loc.lastUpdated).toLocaleString()}</small><br>
          <a href="javascript:void(0)" class="vehicle-profile-link" data-vehicle-id="${loc.vehicleId}" style="color:#3B82F6;font-weight:500;text-decoration:none">
            View Profile →
          </a>
        </div>
      `;
      if (this.markers.has(loc.vehicleId)) {
        this.markers.get(loc.vehicleId)!
          .setLatLng([loc.latitude, loc.longitude])
          .setPopupContent(popupContent);
      } else {
        const marker = L.marker([loc.latitude, loc.longitude])
          .bindPopup(popupContent)
          .addTo(this.map);
        
        marker.on('popupopen', () => {
          const link = document.querySelector(`a[data-vehicle-id="${loc.vehicleId}"]`);
          if (link) {
            link.addEventListener('click', () => this.goToVehicleProfile(loc.vehicleId));
          }
        });
        
        this.markers.set(loc.vehicleId, marker);
      }
    });
  }

  focusVehicle(loc: VehicleLocation): void {
    this.map.setView([loc.latitude, loc.longitude], 12);
    this.markers.get(loc.vehicleId)?.openPopup();
  }

  goToVehicleProfile(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  loadHistory(): void {
    if (!this.selectedVehicleId) return;
    this.loadingHistory = true;
    this.historyError = '';
    this.historyRows = [];
    this.showHistory = true;
    this.trackingService.getHistory(
      this.selectedVehicleId,
      new Date(this.historyFrom),
      new Date(this.historyTo)
    ).subscribe({
      next: rows => { this.historyRows = rows; this.loadingHistory = false; },
      error: () => { this.historyError = 'Could not load tracking history.'; this.loadingHistory = false; }
    });
  }
}
