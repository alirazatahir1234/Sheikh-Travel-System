import { Component, OnInit, OnDestroy, AfterViewInit } from '@angular/core';
import * as L from 'leaflet';
import { TrackingService } from '../../core/services/tracking.service';
import { VehicleLocation } from '../../core/models/tracking.model';

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

  constructor(private trackingService: TrackingService) {}

  ngOnInit(): void {}

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

  private loadLocations(): void {
    this.trackingService.getAllVehicleLocations().subscribe({
      next: locs => {
        this.locations = locs;
        this.loading = false;
        this.updateMarkers(locs);
      },
      error: () => {
        this.loading = false;
        // Demo markers around Pakistan
        const demoLocs: VehicleLocation[] = [
          { vehicleId: 1, vehicleName: 'Coaster #1', registrationNumber: 'KHI-001', latitude: 24.8607, longitude: 67.0011, lastUpdated: new Date().toISOString() },
          { vehicleId: 2, vehicleName: 'Hiace #2',   registrationNumber: 'LHR-002', latitude: 31.5204, longitude: 74.3587, lastUpdated: new Date().toISOString() },
          { vehicleId: 3, vehicleName: 'Bus #3',     registrationNumber: 'ISB-003', latitude: 33.6844, longitude: 73.0479, lastUpdated: new Date().toISOString() }
        ];
        this.locations = demoLocs;
        this.updateMarkers(demoLocs);
      }
    });
  }

  private updateMarkers(locs: VehicleLocation[]): void {
    locs.forEach(loc => {
      const popup = `<b>${loc.vehicleName}</b><br>${loc.registrationNumber}<br>Last: ${new Date(loc.lastUpdated).toLocaleString()}`;
      if (this.markers.has(loc.vehicleId)) {
        this.markers.get(loc.vehicleId)!
          .setLatLng([loc.latitude, loc.longitude])
          .setPopupContent(popup);
      } else {
        const marker = L.marker([loc.latitude, loc.longitude])
          .bindPopup(popup)
          .addTo(this.map);
        this.markers.set(loc.vehicleId, marker);
      }
    });
  }

  focusVehicle(loc: VehicleLocation): void {
    this.map.setView([loc.latitude, loc.longitude], 12);
    this.markers.get(loc.vehicleId)?.openPopup();
  }
}
