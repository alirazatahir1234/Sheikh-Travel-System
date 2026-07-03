import { Component, EventEmitter, Input, Output } from '@angular/core';
import {
  GpsTrip,
  TripAnalyticsSummary,
  TripEvent,
  TripReplayPosition,
  TripStop
} from '../../../../core/models/gps-tracking.model';
import { SharedModule } from '../../../../shared/shared.module';
import { UiDrawerComponent } from '../../../../shared/components/ui/drawer/ui-drawer.component';
import { TripReplayMapComponent } from '../trip-replay-map/trip-replay-map.component';
import { TripRouteAnalysisPanelComponent } from '../trip-route-analysis-panel/trip-route-analysis-panel.component';

@Component({
  selector: 'app-trip-details-drawer',
  templateUrl: './trip-details-drawer.component.html',
  styleUrls: ['./trip-details-drawer.component.scss'],
  standalone: true,
  imports: [SharedModule, UiDrawerComponent, TripReplayMapComponent, TripRouteAnalysisPanelComponent]
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

  get drawerTitle(): string {
    if (!this.trip) return 'Trip Details';
    const vehicle = this.trip.vehicleName ?? `Vehicle #${this.trip.vehicleId}`;
    return `${vehicle} — Trip Replay`;
  }

  onDrawerClosed(): void {
    this.closed.emit();
  }
}
