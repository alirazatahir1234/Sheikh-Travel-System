import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { VehicleService } from '../../../core/services/vehicle.service';
import { BookingService } from '../../../core/services/booking.service';
import { FuelLogService } from '../../../core/services/fuel-log.service';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { Vehicle, VehicleStatus, VehicleStatusLabels } from '../../../core/models/vehicle.model';
import { Booking } from '../../../core/models/booking.model';
import { FuelLog, FuelTypeLabels, FuelType } from '../../../core/models/fuel-log.model';
import { Maintenance, MaintenanceStatusLabels, MaintenanceStatus } from '../../../core/models/maintenance.model';

@Component({
  selector: 'app-vehicle-profile',
  templateUrl: './vehicle-profile.component.html',
  styleUrls: ['./vehicle-profile.component.scss']
})
export class VehicleProfileComponent implements OnInit {
  vehicle: Vehicle | null = null;
  loading = true;
  error: string | null = null;

  recentBookings: Booking[] = [];
  recentFuelLogs: FuelLog[] = [];
  recentMaintenance: Maintenance[] = [];

  totalFuelCost = 0;
  totalFuelLiters = 0;
  totalMaintenanceCost = 0;
  fuelEfficiency = 0;

  bookingColumns = ['bookingNumber', 'customerName', 'pickupTime', 'status'];
  fuelColumns = ['fuelDate', 'fuelType', 'liters', 'totalCost'];
  maintenanceColumns = ['maintenanceDate', 'description', 'status', 'cost'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private vehicleService: VehicleService,
    private bookingService: BookingService,
    private fuelLogService: FuelLogService,
    private maintenanceService: MaintenanceService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.loadProfile(id);
  }

  private loadProfile(id: number): void {
    forkJoin({
      vehicle: this.vehicleService.getById(id),
      bookings: this.bookingService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))),
      fuelLogs: this.fuelLogService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))),
      maintenance: this.maintenanceService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).subscribe({
      next: ({ vehicle, bookings, fuelLogs, maintenance }) => {
        this.vehicle = vehicle;

        this.recentBookings = bookings.items
          .filter(b => b.vehicleId === id)
          .slice(0, 10);

        this.recentFuelLogs = fuelLogs.items
          .filter(f => f.vehicleId === id)
          .slice(0, 10);
        
        const allFuelLogs = fuelLogs.items.filter(f => f.vehicleId === id);
        this.totalFuelLiters = allFuelLogs.reduce((sum, f) => sum + f.liters, 0);
        this.totalFuelCost = allFuelLogs.reduce((sum, f) => sum + f.totalCost, 0);

        this.recentMaintenance = maintenance.items
          .filter(m => m.vehicleId === id)
          .slice(0, 10);
        
        const allMaint = maintenance.items.filter(m => m.vehicleId === id);
        this.totalMaintenanceCost = allMaint.reduce((sum, m) => sum + m.cost, 0);

        if (allFuelLogs.length >= 2) {
          const sorted = [...allFuelLogs].sort((a, b) => 
            new Date(a.fuelDate).getTime() - new Date(b.fuelDate).getTime()
          );
          const first = sorted[0];
          const last = sorted[sorted.length - 1];
          const kmDriven = last.odometerReading - first.odometerReading;
          const fuelUsed = this.totalFuelLiters - first.liters;
          if (fuelUsed > 0 && kmDriven > 0) {
            this.fuelEfficiency = Math.round((kmDriven / fuelUsed) * 10) / 10;
          }
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load vehicle profile.';
      }
    });
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Pending: '#f57f17', Confirmed: '#1565c0', Started: '#00695c',
      InProgress: '#00695c', Completed: '#2e7d32', Cancelled: '#c62828'
    };
    return colors[status] ?? '#666';
  }

  fuelTypeLabel(type: FuelType): string {
    return FuelTypeLabels[type] ?? 'Unknown';
  }

  vehicleStatusLabel(v: Vehicle): string {
    return VehicleStatusLabels[v.status as VehicleStatus] ?? 'Unknown';
  }

  maintenanceStatusLabel(status: MaintenanceStatus): string {
    return MaintenanceStatusLabels[status] ?? 'Unknown';
  }

  getMaintenanceStatusColor(status: MaintenanceStatus): string {
    const colors: Record<number, string> = {
      1: '#f57f17',
      2: '#1565c0',
      3: '#2e7d32'
    };
    return colors[status] ?? '#666';
  }

  editVehicle(): void {
    this.router.navigate(['/vehicles', this.vehicle?.id, 'edit']);
  }
}
