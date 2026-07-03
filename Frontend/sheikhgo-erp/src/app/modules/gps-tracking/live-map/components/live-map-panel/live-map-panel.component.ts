import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import type * as LeafletTypes from 'leaflet';
import { MAP_TILE_STACKS } from '../../../../../core/leaflet/leaflet-map-tiles';
import {
  createMarkerClusterGroup,
  L,
  loadMarkerClusterPlugin
} from '../../../../../core/leaflet/leaflet-cluster';
import { FleetTrackStatus, VehicleLocation } from '../../../../../core/models/gps-tracking.model';
import { SharedModule } from '../../../../../shared/shared.module';

type StatusFilter = 'all' | FleetTrackStatus;

@Component({
  selector: 'app-live-map-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule],
  templateUrl: './live-map-panel.component.html',
  styleUrls: ['./live-map-panel.component.scss']
})
export class LiveMapPanelComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('mapContainer') mapContainer?: ElementRef<HTMLDivElement>;

  @Input() locations: VehicleLocation[] = [];
  @Input() selectedId: number | null = null;
  @Input() statusFilter: StatusFilter = 'all';
  @Input() batteryLowOnly = false;

  @Output() selectVehicle = new EventEmitter<VehicleLocation>();
  @Output() mapReady = new EventEmitter<void>();
  @Output() statusFilterChange = new EventEmitter<StatusFilter>();
  @Output() batteryLowOnlyChange = new EventEmitter<boolean>();

  readonly statusFilters: { id: StatusFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'moving', label: 'Moving' },
    { id: 'parked', label: 'Parked' },
    { id: 'idle', label: 'Idle' },
    { id: 'offline', label: 'Offline' },
    { id: 'sos', label: 'SOS' }
  ];

  private static readonly BATTERY_LOW_THRESHOLD = 20;

  mapTilesLoading = true;

  private map: LeafletTypes.Map | null = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private markerCluster: any = null;
  private markers = new Map<number, LeafletTypes.Marker>();
  private mapInitialized = false;
  private pendingLocations: VehicleLocation[] | null = null;

  ngAfterViewInit(): void {
    setTimeout(() => void this.bootstrapMap(), 50);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['locations'] || changes['selectedId'] || changes['statusFilter'] || changes['batteryLowOnly']) && this.mapInitialized) {
      this.updateMarkers(this.filteredLocations());
    }
    if (changes['selectedId'] && this.selectedId != null && this.mapInitialized) {
      this.focusSelected();
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = null;
  }

  invalidateSize(): void {
    if (!this.map) return;
    const resize = () => this.map?.invalidateSize(true);
    resize();
    requestAnimationFrame(resize);
    setTimeout(resize, 150);
  }

  centerFleet(): void {
    if (!this.map) return;
    const gps = this.filteredLocations().filter(l => this.isValidCoord(l.latitude, l.longitude));
    if (!gps.length) {
      this.map.setView([31.52, 74.35], 11);
      return;
    }
    const bounds = L.latLngBounds(gps.map(l => [l.latitude, l.longitude] as [number, number]));
    this.map.fitBounds(bounds, { padding: [48, 48], maxZoom: 12 });
  }

  focusVehicle(loc: VehicleLocation): void {
    if (!this.map || !this.isValidCoord(loc.latitude, loc.longitude)) return;
    this.map.setView([loc.latitude, loc.longitude], 14, { animate: true });
    this.markers.get(loc.vehicleId)?.openPopup();
  }

  private async bootstrapMap(): Promise<void> {
    if (this.mapInitialized || !this.mapContainer?.nativeElement) return;

    try {
      this.mapTilesLoading = true;
      await loadMarkerClusterPlugin();

      const host = this.mapContainer.nativeElement;
      if ((host as HTMLElement & { _leaflet_id?: number })._leaflet_id != null) return;

      const tiles = MAP_TILE_STACKS['light'][0];
      this.map = L.map(host, { zoomControl: true }).setView([31.52, 74.35], 11);
      L.tileLayer(tiles.url, {
        attribution: tiles.attribution,
        subdomains: (tiles.subdomains ?? 'abc') as string,
        maxZoom: tiles.maxZoom ?? 19
      }).addTo(this.map);

      this.markerCluster = createMarkerClusterGroup({
        maxClusterRadius: 55,
        disableClusteringAtZoom: 14,
        iconCreateFunction: (cluster: { getChildCount: () => number }) =>
          L.divIcon({
            html: `<div class="fleet-cluster"><span>${cluster.getChildCount()}</span></div>`,
            className: 'fleet-cluster-host',
            iconSize: [44, 44],
            iconAnchor: [22, 22]
          })
      });
      this.map.addLayer(this.markerCluster);

      this.map.whenReady(() => {
        this.mapTilesLoading = false;
        this.invalidateSize();
        this.mapReady.emit();
      });

      this.mapInitialized = true;
      const locs = this.pendingLocations ?? this.filteredLocations();
      this.pendingLocations = null;
      if (locs.length) this.updateMarkers(locs);
    } catch {
      this.mapTilesLoading = false;
    }
  }

  toggleBatteryLowOnly(): void {
    this.batteryLowOnlyChange.emit(!this.batteryLowOnly);
  }

  setStatusFilter(id: StatusFilter): void {
    this.statusFilterChange.emit(id);
  }

  private filteredLocations(): VehicleLocation[] {
    let locs = this.locations;
    if (this.statusFilter !== 'all') {
      locs = locs.filter(l => l.status === this.statusFilter);
    }
    if (this.batteryLowOnly) {
      locs = locs.filter(
        l => l.batteryLevel != null && l.batteryLevel < LiveMapPanelComponent.BATTERY_LOW_THRESHOLD
      );
    }
    return locs;
  }

  private updateMarkers(locs: VehicleLocation[]): void {
    if (!this.mapInitialized || !this.map || !this.markerCluster) {
      this.pendingLocations = locs;
      return;
    }

    const mappable = locs.filter(l => l.hasGps && this.isValidCoord(l.latitude, l.longitude));
    const currentIds = new Set(mappable.map(l => l.vehicleId));

    this.markers.forEach((marker, vehicleId) => {
      if (!currentIds.has(vehicleId)) {
        this.markerCluster.removeLayer(marker);
        marker.remove();
        this.markers.delete(vehicleId);
      }
    });

    mappable.forEach(loc => {
      const popupContent = `
        <div class="map-popup">
          <strong>${loc.vehicleName}</strong>
          <span class="map-popup-reg">${loc.registrationNumber}</span>
          <span>${loc.status} · ${Math.round(loc.speed)} km/h</span>
        </div>`;
      const icon = this.createMarkerIcon(loc.status);

      if (this.markers.has(loc.vehicleId)) {
        const marker = this.markers.get(loc.vehicleId)!;
        marker.setLatLng([loc.latitude, loc.longitude]);
        marker.setIcon(icon);
        marker.setPopupContent(popupContent);
      } else {
        const marker = L.marker([loc.latitude, loc.longitude], { icon })
          .bindPopup(popupContent)
          .on('click', () => this.selectVehicle.emit(loc));
        this.markerCluster.addLayer(marker);
        this.markers.set(loc.vehicleId, marker);
      }

      this.markers.get(loc.vehicleId)?.setZIndexOffset(this.selectedId === loc.vehicleId ? 1500 : 0);
    });

    if (typeof this.markerCluster.refreshClusters === 'function') {
      this.markerCluster.refreshClusters();
    }
    this.invalidateSize();
  }

  private focusSelected(): void {
    const loc = this.locations.find(l => l.vehicleId === this.selectedId);
    if (loc) this.focusVehicle(loc);
  }

  private createMarkerIcon(status: FleetTrackStatus): LeafletTypes.DivIcon {
    return L.divIcon({
      className: 'fleet-marker-host',
      html: `<div class="fleet-marker fleet-marker--${status}"><span class="fleet-marker-ring"></span></div>`,
      iconSize: [32, 32],
      iconAnchor: [16, 16]
    });
  }

  private isValidCoord(lat: number, lng: number): boolean {
    return Number.isFinite(lat) && Number.isFinite(lng) && !(lat === 0 && lng === 0);
  }
}
