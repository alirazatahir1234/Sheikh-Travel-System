import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SharedModule } from '../../../../shared/shared.module';
import { UiDrawerComponent } from '../../../../shared/components/ui/drawer/ui-drawer.component';
import {
  GpsTrip,
  TripAnalyticsSummary,
  TripEvent,
  TripReplayPosition,
  TripStop
} from '../../../../core/models/gps-tracking.model';
import { TripReplayMapComponent } from '../../shared/trip-replay-map/trip-replay-map.component';
import { TripRouteAnalysisPanelComponent } from '../../shared/trip-route-analysis-panel/trip-route-analysis-panel.component';

/**
 * Lightweight wrapper around <ui-drawer> for a single selected trip's details. Unlike the heavier
 * multi-tab async-loading detail drawers elsewhere in the app, all data here is already fetched by
 * gps-trips.component.ts's selectTrip() before this opens — no API calls of its own.
 */
@Component({
  selector: 'app-trip-details-drawer',
  standalone: true,
  imports: [SharedModule, UiDrawerComponent, TripReplayMapComponent, TripRouteAnalysisPanelComponent],
  templateUrl: './trip-details-drawer.component.html',
  styleUrls: ['./trip-details-drawer.component.scss']
})
export class TripDetailsDrawerComponent {
  @Input() open = false;
  @Input() trip: GpsTrip | null = null;
  @Input() summary: TripAnalyticsSummary | null = null;
  @Input() events: TripEvent[] = [];
  @Input() replayRoute: TripReplayPosition[] = [];
  @Input() replayPlayback: TripReplayPosition[] = [];
  @Input() replayStops: TripStop[] = [];
  @Input() replayEvents: TripEvent[] = [];
  @Input() replayLoading = false;
  @Output() closed = new EventEmitter<void>();

  get title(): string {
    if (!this.trip) return 'Trip Details';
    const name = this.trip.vehicleName ?? `Vehicle #${this.trip.vehicleId}`;
    return this.trip.plateNumber ? `${name} (${this.trip.plateNumber})` : name;
  }

  get durationLabel(): string {
    if (!this.trip) return '—';
    const m = this.trip.durationMinutes;
    if (m < 60) return `${m} min`;
    const h = Math.floor(m / 60);
    const rem = m % 60;
    return rem ? `${h}h ${rem}m` : `${h}h`;
  }

  get statusLabel(): string {
    return this.trip?.status ?? 'Completed';
  }

  get statusClass(): string {
    return `status-pill--${this.statusLabel.toLowerCase()}`;
  }

  onClose(): void {
    this.closed.emit();
  }
}
