import { Component, Input } from '@angular/core';
import { GpsTrip, TripAnalyticsSummary, TripEvent } from '../../../../core/models/gps-tracking.model';
import {
  TripEventView,
  computeSafetyScore,
  mapTripEvent,
  safetyMessage
} from '../../utils/trip-event.util';
import { SharedModule } from '../../../../shared/shared.module';

@Component({
    selector: 'app-trip-route-analysis-panel',
    templateUrl: './trip-route-analysis-panel.component.html',
    styleUrls: ['./trip-route-analysis-panel.component.scss'],
    standalone: true,
    imports: [SharedModule]
})
export class TripRouteAnalysisPanelComponent {
  @Input() summary: TripAnalyticsSummary | null = null;
  @Input() events: TripEvent[] = [];
  @Input() selectedTrip: GpsTrip | null = null;
  @Input() loading = false;

  get statusLabel(): string {
    return this.selectedTrip ? 'COMPLETED' : 'READY';
  }

  get displayEvents(): TripEventView[] {
    const list = this.selectedTrip
      ? this.events.filter(e => {
          const t = new Date(e.time).getTime();
          return t >= new Date(this.selectedTrip!.startTime).getTime()
            && t <= new Date(this.selectedTrip!.endTime).getTime();
        })
      : this.events;
    return list.slice(0, 8).map(mapTripEvent);
  }

  get safetyScore(): number {
    if (!this.summary) return 0;
    return computeSafetyScore(
      this.summary.overspeedCount,
      this.summary.harshBrakeCount,
      this.summary.harshAccelCount
    );
  }

  get safetyText(): string {
    return safetyMessage(this.safetyScore);
  }

  get scoreStroke(): number {
    const circumference = 2 * Math.PI * 42;
    return circumference * (1 - this.safetyScore / 100);
  }
}
