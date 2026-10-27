import { Component, OnInit } from '@angular/core';
import { TripService } from '../../../core/services/trip.service';
import { TripSmartAssignmentService } from '../../../core/services/trip-smart-assignment.service';
import { TripListItem } from '../../../core/models/trip.model';
import {
  TripAssignmentCandidate,
  TripSmartAssignmentDecision
} from '../../../core/models/trip-smart-assignment.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-trip-smart-assignment',
  templateUrl: './smart-assignment.component.html',
  styleUrls: ['./smart-assignment.component.scss']
})
export class TripSmartAssignmentComponent implements OnInit {
  loadingTrips = false;
  unassignedTrips: TripListItem[] = [];
  selectedTripId: number | null = null;
  selectedTripLabel = '';

  loadingSuggestions = false;
  candidates: TripAssignmentCandidate[] = [];
  routingNote: string | null = null;

  loadingRecent = false;
  recentDecisions: TripSmartAssignmentDecision[] = [];

  overrideDriverId: number | null = null;
  overrideReason = '';
  busy = false;
  error: string | null = null;

  constructor(
    private trips: TripService,
    private smart: TripSmartAssignmentService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.loadUnassigned();
    this.loadRecent();
  }

  loadUnassigned(): void {
    this.loadingTrips = true;
    this.trips.getAll(1, 50, { upcomingOnly: true, statuses: ['Scheduled'] }).subscribe({
      next: page => {
        this.unassignedTrips = (page.items ?? []).filter(t => !t.driverId);
        this.loadingTrips = false;
      },
      error: err => {
        this.error = apiErrorMessage(err);
        this.loadingTrips = false;
      }
    });
  }

  loadRecent(): void {
    this.loadingRecent = true;
    this.smart.getRecent(30).subscribe({
      next: rows => {
        this.recentDecisions = rows;
        this.loadingRecent = false;
      },
      error: () => {
        this.loadingRecent = false;
      }
    });
  }

  selectTrip(trip: TripListItem): void {
    this.selectedTripId = trip.id;
    this.selectedTripLabel = `${trip.tripNumber} — ${trip.pickupAddress ?? 'Pickup TBD'}`;
    this.overrideDriverId = null;
    this.loadSuggestions();
  }

  loadSuggestions(): void {
    if (!this.selectedTripId) return;
    this.loadingSuggestions = true;
    this.candidates = [];
    this.smart.getSuggestions(this.selectedTripId).subscribe({
      next: res => {
        this.candidates = res.candidates ?? [];
        this.routingNote = res.routingNote ?? null;
        this.loadingSuggestions = false;
      },
      error: err => {
        this.toast.error(apiErrorMessage(err));
        this.loadingSuggestions = false;
      }
    });
  }

  autoAssign(): void {
    if (!this.selectedTripId) return;
    this.busy = true;
    this.smart.autoAssign(this.selectedTripId).subscribe({
      next: res => {
        this.toast.success(`Assigned ${res.driverName} (score ${res.totalScore.toFixed(0)})`);
        this.busy = false;
        this.loadUnassigned();
        this.loadRecent();
        this.loadSuggestions();
      },
      error: err => {
        this.toast.error(apiErrorMessage(err));
        this.busy = false;
      }
    });
  }

  applyOverride(): void {
    if (!this.selectedTripId || !this.overrideDriverId) {
      this.toast.warning('Select a driver to override.');
      return;
    }
    this.busy = true;
    this.smart
      .override(this.selectedTripId, {
        driverId: this.overrideDriverId,
        reason: this.overrideReason.trim() || null
      })
      .subscribe({
        next: res => {
          this.toast.success(`Override applied: ${res.driverName}`);
          this.busy = false;
          this.loadUnassigned();
          this.loadRecent();
        },
        error: err => {
          this.toast.error(apiErrorMessage(err));
          this.busy = false;
        }
      });
  }

  trafficLabel(source: string): string {
    return source === 'GoogleRoutes' ? 'Google Routes (traffic-aware)' : 'Estimated (no live traffic API)';
  }
}
