import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { BookingService } from '../../../core/services/booking.service';
import { RouteService } from '../../../core/services/route.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { Route } from '../../../core/models/route.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';
import { PriceBreakdown } from '../../../core/models/pricing.model';
import { Booking } from '../../../core/models/booking.model';

@Component({
  selector: 'app-booking-wizard',
  templateUrl: './booking-wizard.component.html',
  styleUrls: ['./booking-wizard.component.scss']
})
export class BookingWizardComponent implements OnInit {
  step1Form: FormGroup;
  step2Form: FormGroup;

  routes: Route[] = [];
  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];
  priceBreakdown: PriceBreakdown | null = null;
  createdBooking: Booking | null = null;
  loading = false;
  calculating = false;
  loadingData = true;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private bookingService: BookingService,
    private routeService: RouteService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.step1Form = this.fb.group({
      customerId: [null, [Validators.required, Validators.min(1)]],
      routeId: [null, Validators.required],
      vehicleId: [null, Validators.required],
      pickupTime: [null, Validators.required],
      passengerCount: [1, [Validators.required, Validators.min(1)]],
      fuelPricePerLiter: [270, [Validators.required, Validators.min(0)]],
      driverAllowance: [0],
      tollCharges: [0],
      otherCharges: [0],
      notes: ['']
    });

    this.step2Form = this.fb.group({
      driverId: [null]
    });
  }

  ngOnInit(): void {
    forkJoin({
      routes: this.routeService.getAll(1, 100),
      vehicles: this.vehicleService.getAll(1, 100),
      drivers: this.driverService.getAll(1, 100)
    }).subscribe({
      next: ({ routes, vehicles, drivers }) => {
        this.routes = routes.items;
        this.vehicles = vehicles.items.filter(v => v.isActive);
        this.drivers = drivers.items.filter(d => d.isActive);
        this.loadingData = false;
      },
      error: () => {
        this.loadingData = false;
        this.error = 'Failed to load booking data. Please try again.';
      }
    });
  }

  trackById(_index: number, item: { id: number }): number {
    return item.id;
  }

  calculatePrice(): void {
    const f = this.step1Form.value;
    if (!f.routeId || !f.vehicleId) return;
    this.calculating = true;
    this.bookingService.calculatePrice({
      routeId: f.routeId,
      vehicleId: f.vehicleId,
      passengerCount: f.passengerCount,
      fuelPricePerLiter: f.fuelPricePerLiter,
      driverAllowance: f.driverAllowance,
      tollCharges: f.tollCharges,
      otherCharges: f.otherCharges
    }).subscribe({
      next: p => { this.priceBreakdown = p; this.calculating = false; },
      error: () => { this.calculating = false; }
    });
  }

  createBooking(): void {
    if (this.step1Form.invalid) return;
    this.loading = true;
    const f = this.step1Form.value;
    this.bookingService.create({
      customerId: f.customerId,
      routeId: f.routeId,
      pickupTime: new Date(f.pickupTime).toISOString(),
      passengerCount: f.passengerCount,
      totalAmount: this.priceBreakdown?.totalAmount ?? 0,
      notes: f.notes
    }).subscribe({
      next: booking => {
        this.createdBooking = booking;
        this.loading = false;
        this.bookingService.assignVehicle({ bookingId: booking.id, vehicleId: f.vehicleId }).subscribe();
      },
      error: () => { this.loading = false; this.snackBar.open('Failed to create booking', 'Close', { duration: 3000 }); }
    });
  }

  assignAndFinish(): void {
    if (!this.createdBooking) return;
    const driverId = this.step2Form.value.driverId;
    if (driverId) {
      this.bookingService.assignDriver({ bookingId: this.createdBooking.id, driverId }).subscribe();
    }
    this.snackBar.open('Booking created successfully!', 'Close', { duration: 3000 });
    this.router.navigate(['/bookings']);
  }

  getVehicleName(id: number): string {
    return this.vehicles.find(v => v.id === id)?.name ?? '';
  }

  getRouteName(id: number): string {
    return this.routes.find(r => r.id === id)?.name ?? '';
  }
}
