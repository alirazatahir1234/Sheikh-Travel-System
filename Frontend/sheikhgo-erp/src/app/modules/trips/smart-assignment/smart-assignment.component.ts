import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TripService } from '../../../core/services/trip.service';
import { TripSmartAssignmentService } from '../../../core/services/trip-smart-assignment.service';
import { TripListItem } from '../../../core/models/trip.model';
import {
  TripAssignmentCandidate,
  TripAssignmentExclusion,
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
  excluded: TripAssignmentExclusion[] = [];
  exclusionCounts: Record<string, number> = {};
  routingNote: string | null = null;
  mode = 'SuggestOnly';

  loadingRecent = false;
  recentDecisions: TripSmartAssignmentDecision[] = [];

  overrideDriverId: number | null = null;
  overrideReason = '';
  busy = false;
  error: string | null = null;
  showExcluded = false;

  constructor(
    private trips: TripService,
    private smart: TripSmartAssignmentService,
    private toast: UiToastService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.loadUnassigned();
    this.loadRecent();
    const tripIdParam = Number(this.route.snapshot.queryParamMap.get('tripId'));
    if (Number.isFinite(tripIdParam) && tripIdParam > 0) {
      this.selectedTripId = tripIdParam;
      this.selectedTripLabel = `Trip #${tripIdParam}`;
      this.loadSuggestions();
    }
  }

  get exclusionSummary(): string {
    const entries = Object.entries(this.exclusionCounts);
    if (!entries.length) return '';
    return entries.map(([reason, n]) => `${reason}: ${n}`).join(' · ');
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
    this.excluded = [];
    this.exclusionCounts = {};
    this.smart.getSuggestions(this.selectedTripId).subscribe({
      next: res => {
        this.candidates = res.candidates ?? [];
        this.excluded = res.excluded ?? [];
        this.exclusionCounts = res.exclusionCounts ?? {};
        this.routingNote = res.routingNote ?? null;
        this.mode = res.mode || 'SuggestOnly';
        this.loadingSuggestions = false;
      },
      error: err => {
        this.toast.error(apiErrorMessage(err));
        this.loadingSuggestions = false;
      }
    });
  }

  confirmCandidate(c: TripAssignmentCandidate): void {
    if (!this.selectedTripId) return;
    this.busy = true;
    this.smart
      .confirm(this.selectedTripId, {
        driverId: c.driverId,
        vehicleId: c.vehicleId ?? null
      })
      .subscribe({
        next: res => {
          this.toast.success(`Confirmed ${res.driverName} (score ${res.totalScore.toFixed(0)})`);
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

  reasonLabel(reason: string): string {
    const map: Record<string, string> = {
      eligibility: 'Eligibility / docs',
      on_trip: 'Already on trip',
      no_vehicle: 'No linked vehicle',
      capacity: 'Capacity',
      vehicle_type: 'Vehicle type',
      maintenance: 'Maintenance',
      shift_limit: 'Shift limit',
      schedule_conflict: 'Schedule conflict',
      ev_range: 'EV range'
    };
    return map[reason] || reason;
  }
}
