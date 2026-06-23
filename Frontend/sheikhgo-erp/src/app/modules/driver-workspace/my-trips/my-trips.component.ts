import { Component, OnInit } from '@angular/core';
import { DriverAppService } from '../../../core/services/driver-app.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { DriverTrip } from '../../../core/models/driver-trip.model';

@Component({
  selector: 'app-my-trips',
  templateUrl: './my-trips.component.html',
  styleUrls: ['./my-trips.component.scss']
})
export class MyTripsComponent implements OnInit {
  trips: DriverTrip[] = [];
  loading = true;
  actionId: number | null = null;

  constructor(
    private driverApp: DriverAppService,
    private toast: UiToastService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.driverApp.getTrips().subscribe({
      next: trips => {
        this.trips = trips;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(err?.error?.message || 'Could not load trips');
      }
    });
  }

  startTrip(trip: DriverTrip): void {
    this.runAction(trip.id, () => this.driverApp.startTrip(trip.id), 'Trip started');
  }

  completeTrip(trip: DriverTrip): void {
    this.runAction(trip.id, () => this.driverApp.completeTrip(trip.id), 'Trip completed');
  }

  rejectTrip(trip: DriverTrip): void {
    const reason = window.prompt('Reason for declining this trip?');
    if (!reason?.trim()) return;
    this.runAction(trip.id, () => this.driverApp.rejectTrip(trip.id, reason.trim()), 'Trip declined');
  }

  isStarted(trip: DriverTrip): boolean {
    return trip.status === 3 || trip.statusName === 'Started';
  }

  isConfirmed(trip: DriverTrip): boolean {
    return trip.status === 2 || trip.statusName === 'Confirmed';
  }

  formatWhen(value: string): string {
    return new Date(value).toLocaleString();
  }

  private runAction(id: number, call: () => ReturnType<DriverAppService['startTrip']>, success: string): void {
    this.actionId = id;
    call().subscribe({
      next: () => {
        this.actionId = null;
        this.toast.success(success);
        this.load();
      },
      error: err => {
        this.actionId = null;
        this.toast.error(err?.error?.message || 'Action failed');
      }
    });
  }
}
