import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { MatIconModule } from '@angular/material/icon';
import { FleetTrackStatus, VehicleLocation } from '../../../../../core/models/gps-tracking.model';
import { SharedModule } from '../../../../../shared/shared.module';

export type VehicleListTab = 'all' | 'moving' | 'parked' | 'more';

@Component({
  selector: 'app-vehicle-list-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, ScrollingModule, MatIconModule],
  templateUrl: './vehicle-list-panel.component.html'
})
export class VehicleListPanelComponent {
  @Input() locations: VehicleLocation[] = [];
  @Input() selectedId: number | null = null;
  @Input() loading = false;
  @Input() listTab: VehicleListTab = 'all';
  @Input() searchInput = '';

  @Output() select = new EventEmitter<VehicleLocation>();
  @Output() searchChange = new EventEmitter<string>();
  @Output() tabChange = new EventEmitter<VehicleListTab>();

  readonly tabs: { id: VehicleListTab; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'moving', label: 'Moving' },
    { id: 'parked', label: 'Parked' },
    { id: 'more', label: 'More' }
  ];

  get filteredLocations(): VehicleLocation[] {
    const q = this.searchInput.trim().toLowerCase();
    let list = this.locations;

    if (this.listTab === 'moving') {
      list = list.filter(l => l.status === 'moving' || l.status === 'idle');
    } else if (this.listTab === 'parked') {
      list = list.filter(l => l.status === 'parked');
    } else if (this.listTab === 'more') {
      list = list.filter(l =>
        ['idle', 'offline', 'never_seen', 'sos', 'delayed', 'scheduled'].includes(l.status)
      );
    }

    if (!q) return list;

    return list.filter(l =>
      [l.vehicleName, l.registrationNumber, l.driverName, l.trackerName, l.imei, l.routeHint]
        .filter(Boolean)
        .some(v => String(v).toLowerCase().includes(q))
    );
  }

  statusLabel(status: FleetTrackStatus): string {
    return status.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }

  statusIcon(status: FleetTrackStatus): string {
    const icons: Record<FleetTrackStatus, string> = {
      moving: 'directions_bus',
      idle: 'local_shipping',
      parked: 'local_parking',
      offline: 'signal_wifi_off',
      never_seen: 'help_outline',
      sos: 'sos',
      scheduled: 'schedule',
      delayed: 'warning'
    };
    return icons[status];
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  speedLabel(loc: VehicleLocation): string {
    if (!loc.hasGps) return '—';
    if (loc.speed > 0) return `${Math.round(loc.speed)} km/h`;
    return loc.status === 'idle' ? '0 km/h' : 'Stationary';
  }

  formatLastPing(loc: VehicleLocation): string {
    if (!loc.hasGps || !loc.lastUpdated) return 'No live GPS';
    const sec = Math.floor((Date.now() - new Date(loc.lastUpdated).getTime()) / 1000);
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}m ago`;
    return `${Math.floor(min / 60)}h ago`;
  }
}
