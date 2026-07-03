import { ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../../../core/services/auth.service';
import { FleetTrackStatus, VehicleLocation } from '../../../../../core/models/gps-tracking.model';
import { SharedModule } from '../../../../../shared/shared.module';

interface DetailField {
  icon: string;
  label: string;
  value: string;
}

interface DetailSection {
  title: string;
  fields: DetailField[];
}

@Component({
  selector: 'app-vehicle-detail-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule],
  templateUrl: './vehicle-detail-panel.component.html'
})
export class VehicleDetailPanelComponent {
  private readonly auth = inject(AuthService);

  @Input() location: VehicleLocation | null = null;

  @Output() track = new EventEmitter<void>();
  @Output() replay = new EventEmitter<void>();
  @Output() history = new EventEmitter<void>();
  @Output() trips = new EventEmitter<void>();
  @Output() commands = new EventEmitter<void>();
  @Output() profile = new EventEmitter<void>();

  get canSendCommands(): boolean {
    return this.auth.hasPermission('Gps.CommandSend');
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

  speedLabel(loc: VehicleLocation): string {
    if (!loc.hasGps) return '—';
    if (loc.speed > 0) return `${Math.round(loc.speed)} km/h`;
    return loc.status === 'idle' ? '0 km/h · idle' : 'Stationary';
  }

  formatLastPing(loc: VehicleLocation): string {
    if (!loc.hasGps || !loc.lastUpdated) return 'No live GPS';
    const sec = Math.floor((Date.now() - new Date(loc.lastUpdated).getTime()) / 1000);
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}m ago`;
    return `${Math.floor(min / 60)}h ago`;
  }

  get sections(): DetailSection[] {
    const loc = this.location;
    if (!loc) return [];

    const sections: DetailSection[] = [];

    const identity: DetailField[] = [];
    if (loc.driverName) identity.push({ icon: 'person', label: 'Driver', value: loc.driverName });
    if (loc.trackerName) identity.push({ icon: 'router', label: 'Tracker', value: loc.trackerName });
    if (loc.imei) identity.push({ icon: 'sim_card', label: 'IMEI', value: loc.imei });
    if (identity.length) sections.push({ title: 'Identity', fields: identity });

    sections.push({
      title: 'Motion',
      fields: [
        { icon: 'speed', label: 'Speed', value: this.speedLabel(loc) },
        ...(loc.ignition !== undefined
          ? [{ icon: 'power_settings_new', label: 'Ignition', value: loc.ignition ? 'On' : 'Off' }]
          : []),
        ...(loc.totalDistanceKm != null
          ? [{ icon: 'timeline', label: 'Odometer', value: `${Math.round(loc.totalDistanceKm)} km` }]
          : [])
      ]
    });

    const power: DetailField[] = [];
    if (loc.fuelLevel != null) {
      power.push({ icon: 'local_gas_station', label: 'Fuel', value: `${Math.round(loc.fuelLevel)}%` });
    }
    if (loc.batteryLevel != null) {
      power.push({ icon: 'battery_std', label: 'Battery', value: `${Math.round(loc.batteryLevel)}%` });
    }
    if (loc.gsmSignal != null) {
      power.push({ icon: 'signal_cellular_alt', label: 'GSM', value: `${loc.gsmSignal} dBm` });
    }
    if (loc.relayOutput) {
      power.push({ icon: 'electrical_services', label: 'Relay', value: loc.relayOutput });
    }
    if (power.length) sections.push({ title: 'Fuel & Power', fields: power });

    const locationFields: DetailField[] = [];
    if (loc.address) {
      locationFields.push({ icon: 'place', label: 'Address', value: loc.address });
    } else {
      locationFields.push({
        icon: 'place',
        label: 'Coordinates',
        value: `${loc.latitude.toFixed(4)}, ${loc.longitude.toFixed(4)}`
      });
    }
    if (loc.routeHint) {
      locationFields.push({ icon: 'route', label: 'Route', value: loc.routeHint });
    }
    locationFields.push({ icon: 'schedule', label: 'Last update', value: this.formatLastPing(loc) });
    sections.push({ title: 'Location', fields: locationFields });

    return sections;
  }
}
