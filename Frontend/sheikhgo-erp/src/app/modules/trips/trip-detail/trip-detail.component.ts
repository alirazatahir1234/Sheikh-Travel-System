import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { TripService } from '../../../core/services/trip.service';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  BOARDING_STATUSES,
  DROP_STATUSES,
  TRIP_DOCUMENT_TYPES,
  TRIP_EXPENSE_TYPES,
  TripDetail,
  TripPassenger,
  TripRouteSummary,
  TripStatus,
  TRIP_STATUSES
} from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-detail',
  templateUrl: './trip-detail.component.html',
  styleUrls: ['./trip-detail.component.scss']
})
export class TripDetailComponent implements OnInit, OnDestroy {
  trip: TripDetail | null = null;
  routeSummary: TripRouteSummary | null = null;
  loading = true;
  optimizing = false;
  nextStatus: TripStatus | '' = '';
  cancelReason = '';
  statusNote = '';
  driverId: number | null = null;
  vehicleId: number | null = null;
  drivers: Array<{ id: number; fullName: string }> = [];
  vehicles: Array<{ id: number; name: string }> = [];
  readonly statuses = TRIP_STATUSES;
  readonly expenseTypes = TRIP_EXPENSE_TYPES;
  readonly documentTypes = TRIP_DOCUMENT_TYPES;
  readonly boardingStatuses = BOARDING_STATUSES;
  readonly dropStatuses = DROP_STATUSES;

  expenseType = 'Fuel';
  expenseAmount: number | null = null;
  expenseDescription = '';
  passengerName = '';
  passengerPhone = '';
  documentType = 'TripSheet';
  selectedFile: File | null = null;
  private routePoll: ReturnType<typeof setInterval> | null = null;

  get totalExpenses(): number {
    return (this.trip?.expenses || []).reduce((sum, e) => sum + (e.amount || 0), 0);
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private trips: TripService,
    private driversApi: DriverService,
    private vehiclesApi: VehicleService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    const id = +(this.route.snapshot.paramMap.get('id') || 0);
    forkJoin({
      drivers: this.driversApi.getAll(1, 500),
      vehicles: this.vehiclesApi.getAll(1, 500)
    }).subscribe({
      next: res => {
        this.drivers = res.drivers.items.map(d => ({ id: d.id, fullName: d.fullName }));
        this.vehicles = res.vehicles.items.map(v => ({ id: v.id, name: v.name }));
      }
    });
    this.load(id);
    this.routePoll = setInterval(() => {
      if (this.trip?.id && this.isLiveStatus(this.trip.status)) {
        this.loadRouteSummary(this.trip.id, true);
      }
    }, 30000);
  }

  ngOnDestroy(): void {
    if (this.routePoll) clearInterval(this.routePoll);
  }

  load(id: number): void {
    this.loading = true;
    this.trips.getById(id).subscribe({
      next: trip => {
        this.trip = {
          ...trip,
          expenses: trip.expenses || [],
          documents: trip.documents || [],
          passengers: trip.passengers || [],
          openAlertCount: trip.openAlertCount || 0
        };
        this.driverId = trip.driverId ?? null;
        this.vehicleId = trip.vehicleId ?? null;
        this.loading = false;
        this.loadRouteSummary(id);
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load trip.'));
      }
    });
  }

  loadRouteSummary(id: number, silent = false): void {
    this.trips.getRouteSummary(id).subscribe({
      next: summary => { this.routeSummary = summary; },
      error: err => {
        if (!silent) this.toast.error(apiErrorMessage(err, 'Failed to load route summary.'));
      }
    });
  }

  optimizeRoute(): void {
    if (!this.trip) return;
    this.optimizing = true;
    this.trips.optimizeRoute(this.trip.id).subscribe({
      next: summary => {
        this.routeSummary = summary;
        this.optimizing = false;
        this.toast.success('Route optimized.');
        this.load(this.trip!.id);
      },
      error: err => {
        this.optimizing = false;
        this.toast.error(apiErrorMessage(err, 'Optimize failed.'));
      }
    });
  }

  openGoogleMaps(): void {
    const url = this.routeSummary?.googleDirectionsUrl || this.routeSummary?.googleMapsUrl;
    if (!url) {
      this.toast.warning('Add pickup and destination to open Google Maps.');
      return;
    }
    window.open(url, '_blank', 'noopener');
  }

  applyStatus(): void {
    if (!this.trip || !this.nextStatus) return;
    const reason = this.nextStatus === 'Cancelled' ? this.cancelReason : undefined;
    this.trips.updateStatus(this.trip.id, this.nextStatus, this.statusNote || undefined, reason).subscribe({
      next: () => {
        this.toast.success('Status updated.');
        this.nextStatus = '';
        this.cancelReason = '';
        this.statusNote = '';
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Status update failed.'))
    });
  }

  assignDriver(): void {
    if (!this.trip || !this.driverId) return;
    this.trips.assignDriver(this.trip.id, this.driverId).subscribe({
      next: () => {
        this.toast.success('Driver assigned.');
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Driver assign failed.'))
    });
  }

  assignVehicle(): void {
    if (!this.trip || !this.vehicleId) return;
    this.trips.assignVehicle(this.trip.id, this.vehicleId).subscribe({
      next: () => {
        this.toast.success('Vehicle assigned.');
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Vehicle assign failed.'))
    });
  }

  openLiveMap(): void {
    if (!this.trip?.vehicleId) {
      this.toast.warning('Assign a vehicle first to open live tracking.');
      return;
    }
    this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: this.trip.vehicleId } });
  }

  openAlerts(): void {
    if (!this.trip?.vehicleId) {
      this.toast.warning('Assign a vehicle to view GPS alerts.');
      return;
    }
    this.router.navigate(['/gps-tracking/alerts'], { queryParams: { vehicleId: this.trip.vehicleId } });
  }

  addExpense(): void {
    if (!this.trip || !this.expenseAmount || this.expenseAmount <= 0) return;
    this.trips.addExpense(this.trip.id, {
      expenseType: this.expenseType,
      amount: this.expenseAmount,
      description: this.expenseDescription || undefined
    }).subscribe({
      next: () => {
        this.toast.success('Expense added.');
        this.expenseAmount = null;
        this.expenseDescription = '';
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to add expense.'))
    });
  }

  deleteExpense(id: number): void {
    if (!this.trip || !confirm('Delete this expense?')) return;
    this.trips.deleteExpense(this.trip.id, id).subscribe({
      next: () => {
        this.toast.success('Expense deleted.');
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }

  addPassenger(): void {
    if (!this.trip || !this.passengerName.trim()) return;
    this.trips.addPassenger(this.trip.id, {
      fullName: this.passengerName.trim(),
      phone: this.passengerPhone || undefined
    }).subscribe({
      next: () => {
        this.toast.success('Passenger added.');
        this.passengerName = '';
        this.passengerPhone = '';
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to add passenger.'))
    });
  }

  updatePassengerStatus(p: TripPassenger): void {
    if (!this.trip) return;
    this.trips.updatePassenger(this.trip.id, p.id, {
      fullName: p.fullName,
      phone: p.phone || undefined,
      boardingStatus: p.boardingStatus,
      dropStatus: p.dropStatus,
      notes: p.notes || undefined
    }).subscribe({
      next: () => this.toast.success('Passenger updated.'),
      error: err => this.toast.error(apiErrorMessage(err, 'Update failed.'))
    });
  }

  deletePassenger(id: number): void {
    if (!this.trip || !confirm('Remove this passenger?')) return;
    this.trips.deletePassenger(this.trip.id, id).subscribe({
      next: () => {
        this.toast.success('Passenger removed.');
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] || null;
  }

  uploadDocument(): void {
    if (!this.trip || !this.selectedFile) return;
    this.trips.uploadDocument(this.trip.id, this.selectedFile, this.documentType).subscribe({
      next: () => {
        this.toast.success('Document uploaded.');
        this.selectedFile = null;
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Upload failed.'))
    });
  }

  deleteDocument(id: number): void {
    if (!this.trip || !confirm('Delete this document?')) return;
    this.trips.deleteDocument(this.trip.id, id).subscribe({
      next: () => {
        this.toast.success('Document deleted.');
        this.load(this.trip!.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }

  private isLiveStatus(status: TripStatus): boolean {
    return status === 'Started' || status === 'AtPickup' || status === 'Enroute' || status === 'Delayed';
  }
}
