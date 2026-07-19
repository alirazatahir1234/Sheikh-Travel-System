import { Component, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { TripService } from '../../../core/services/trip.service';
import { CustomerService } from '../../../core/services/customer.service';
import { DriverService } from '../../../core/services/driver.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { RouteService } from '../../../core/services/route.service';
import { Customer } from '../../../core/models/customer.model';
import { Route } from '../../../core/models/route.model';
import {
  CreateTripDto,
  TRIP_PRIORITIES,
  TRIP_TYPES,
  TripPriority,
  TripType
} from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-form',
  templateUrl: './trip-form.component.html',
  styleUrls: ['./trip-form.component.scss']
})
export class TripFormComponent implements OnInit {
  form!: FormGroup;
  loading = false;
  saving = false;
  isEdit = false;
  tripId: number | null = null;

  customers: Customer[] = [];
  routes: Route[] = [];
  drivers: Array<{ id: number; fullName: string }> = [];
  vehicles: Array<{ id: number; name: string }> = [];

  readonly tripTypes = TRIP_TYPES;
  readonly priorities = TRIP_PRIORITIES;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private trips: TripService,
    private customersApi: CustomerService,
    private driversApi: DriverService,
    private vehiclesApi: VehicleService,
    private routesApi: RouteService,
    private toast: UiToastService
  ) {}

  get stops(): FormArray {
    return this.form.get('stops') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      tripName: ['', Validators.required],
      tripType: ['Transfer' as TripType, Validators.required],
      customerId: [null, Validators.required],
      routeId: [null],
      passengerCount: [1, [Validators.required, Validators.min(1)]],
      priority: ['Normal' as TripPriority, Validators.required],
      pickupAddress: [''],
      pickupLatitude: [null],
      pickupLongitude: [null],
      destinationAddress: [''],
      destinationLatitude: [null],
      destinationLongitude: [null],
      tripDate: ['', Validators.required],
      plannedStart: ['', Validators.required],
      plannedEnd: [''],
      estimatedDurationMinutes: [null],
      plannedDistanceKm: [null],
      driverNotes: [''],
      driverId: [null],
      assistantDriverId: [null],
      vehicleId: [null],
      stops: this.fb.array([])
    });

    this.loading = true;
    forkJoin({
      customers: this.customersApi.getAll(1, 500),
      routes: this.routesApi.getAll(1, 500),
      drivers: this.driversApi.getAll(1, 500),
      vehicles: this.vehiclesApi.getAll(1, 500)
    }).subscribe({
      next: res => {
        this.customers = res.customers.items;
        this.routes = res.routes.items;
        this.drivers = res.drivers.items.map(d => ({ id: d.id, fullName: d.fullName }));
        this.vehicles = res.vehicles.items.map(v => ({ id: v.id, name: v.name }));
        this.loading = false;

        const idParam = this.route.snapshot.paramMap.get('id');
        const isEditRoute = this.router.url.includes('/edit');
        if (idParam && isEditRoute) {
          this.isEdit = true;
          this.tripId = +idParam;
          this.loadTrip(+idParam);
        }
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load form data.'));
      }
    });
  }

  private loadTrip(id: number): void {
    this.trips.getById(id).subscribe({
      next: trip => {
        this.form.patchValue({
          tripName: trip.tripName,
          tripType: trip.tripType,
          customerId: trip.customerId,
          routeId: trip.routeId,
          passengerCount: trip.passengerCount,
          priority: trip.priority,
          pickupAddress: trip.pickupAddress,
          pickupLatitude: trip.pickupLatitude,
          pickupLongitude: trip.pickupLongitude,
          destinationAddress: trip.destinationAddress,
          destinationLatitude: trip.destinationLatitude,
          destinationLongitude: trip.destinationLongitude,
          tripDate: trip.tripDate?.substring(0, 10),
          plannedStart: this.toLocalInput(trip.plannedStart),
          plannedEnd: trip.plannedEnd ? this.toLocalInput(trip.plannedEnd) : '',
          estimatedDurationMinutes: trip.estimatedDurationMinutes,
          plannedDistanceKm: trip.plannedDistanceKm,
          driverNotes: trip.driverNotes,
          driverId: trip.driverId,
          assistantDriverId: trip.assistantDriverId,
          vehicleId: trip.vehicleId
        });
        this.stops.clear();
        for (const s of trip.stops || []) {
          this.stops.push(this.fb.group({
            sequence: [s.sequence],
            location: [s.location, Validators.required],
            latitude: [s.latitude],
            longitude: [s.longitude],
            eta: [s.eta ? this.toLocalInput(s.eta) : '']
          }));
        }
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load trip.'))
    });
  }

  addStop(): void {
    this.stops.push(this.fb.group({
      sequence: [this.stops.length + 1],
      location: ['', Validators.required],
      latitude: [null],
      longitude: [null],
      eta: ['']
    }));
  }

  removeStop(index: number): void {
    this.stops.removeAt(index);
    this.stops.controls.forEach((c, i) => c.patchValue({ sequence: i + 1 }));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    const payload: CreateTripDto = {
      tripName: v.tripName,
      tripType: v.tripType,
      customerId: +v.customerId,
      routeId: v.routeId ? +v.routeId : null,
      passengerCount: +v.passengerCount,
      priority: v.priority,
      pickupAddress: v.pickupAddress || null,
      pickupLatitude: v.pickupLatitude,
      pickupLongitude: v.pickupLongitude,
      destinationAddress: v.destinationAddress || null,
      destinationLatitude: v.destinationLatitude,
      destinationLongitude: v.destinationLongitude,
      tripDate: new Date(v.tripDate).toISOString(),
      plannedStart: new Date(v.plannedStart).toISOString(),
      plannedEnd: v.plannedEnd ? new Date(v.plannedEnd).toISOString() : null,
      estimatedDurationMinutes: v.estimatedDurationMinutes,
      plannedDistanceKm: v.plannedDistanceKm,
      driverNotes: v.driverNotes || null,
      driverId: v.driverId ? +v.driverId : null,
      assistantDriverId: v.assistantDriverId ? +v.assistantDriverId : null,
      vehicleId: v.vehicleId ? +v.vehicleId : null,
      stops: (v.stops || []).map((s: { sequence: number; location: string; latitude?: number; longitude?: number; eta?: string }) => ({
        sequence: s.sequence,
        location: s.location,
        latitude: s.latitude,
        longitude: s.longitude,
        eta: s.eta ? new Date(s.eta).toISOString() : null
      }))
    };

    this.saving = true;
    if (this.isEdit && this.tripId) {
      const { driverId, assistantDriverId, vehicleId, bookingId, ...update } = payload as CreateTripDto & { bookingId?: number };
      this.trips.update(this.tripId, update).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Trip updated.');
          this.router.navigate(['/trips', this.tripId]);
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Update failed.'));
        }
      });
      return;
    }

    this.trips.create(payload).subscribe({
      next: id => {
        this.saving = false;
        this.toast.success('Trip created.');
        this.router.navigate(['/trips', id]);
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Create failed.'));
      }
    });
  }

  private toLocalInput(iso: string): string {
    const d = new Date(iso);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
