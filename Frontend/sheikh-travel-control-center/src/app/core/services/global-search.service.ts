import { Injectable } from '@angular/core';
import { forkJoin, Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { CustomerService } from './customer.service';
import { BookingService } from './booking.service';
import { RouteService } from './route.service';
import { PaymentService } from './payment.service';
import { FuelLogService } from './fuel-log.service';
import { MaintenanceService } from './maintenance.service';

export type SearchResultType = 'booking' | 'vehicle' | 'driver' | 'customer' | 'route' | 'payment' | 'fuel_log' | 'maintenance';

export interface SearchResult {
  type: SearchResultType;
  id: number;
  title: string;
  subtitle: string;
  icon: string;
  route: string;
}

@Injectable({ providedIn: 'root' })
export class GlobalSearchService {
  constructor(
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private customerService: CustomerService,
    private bookingService: BookingService,
    private routeService: RouteService,
    private paymentService: PaymentService,
    private fuelLogService: FuelLogService,
    private maintenanceService: MaintenanceService
  ) {}

  search(query: string): Observable<SearchResult[]> {
    if (!query || query.trim().length < 2) {
      return of([]);
    }

    const q = query.trim().toLowerCase();

    return forkJoin({
      vehicles: this.vehicleService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      drivers: this.driverService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      customers: this.customerService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      bookings: this.bookingService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      routes: this.routeService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      payments: this.paymentService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      fuelLogs: this.fuelLogService.getAll(1, 100).pipe(catchError(() => of({ items: [] }))),
      maintenance: this.maintenanceService.getAll(1, 100).pipe(catchError(() => of({ items: [] })))
    }).pipe(
      map(({ vehicles, drivers, customers, bookings, routes, payments, fuelLogs, maintenance }) => {
        const results: SearchResult[] = [];

        // Search vehicles
        vehicles.items
          .filter(v =>
            v.name?.toLowerCase().includes(q) ||
            v.registrationNumber?.toLowerCase().includes(q) ||
            v.model?.toLowerCase().includes(q)
          )
          .slice(0, 4)
          .forEach(v => {
            results.push({
              type: 'vehicle',
              id: v.id,
              title: v.name || 'Unnamed Vehicle',
              subtitle: v.registrationNumber || '',
              icon: 'directions_bus',
              route: `/vehicles/${v.id}`
            });
          });

        // Search drivers
        drivers.items
          .filter(d =>
            d.fullName?.toLowerCase().includes(q) ||
            d.licenseNumber?.toLowerCase().includes(q) ||
            d.phone?.toLowerCase().includes(q)
          )
          .slice(0, 4)
          .forEach(d => {
            results.push({
              type: 'driver',
              id: d.id,
              title: d.fullName || 'Unnamed Driver',
              subtitle: d.licenseNumber || d.phone || '',
              icon: 'badge',
              route: `/drivers/${d.id}`
            });
          });

        // Search customers
        customers.items
          .filter(c =>
            c.fullName?.toLowerCase().includes(q) ||
            c.email?.toLowerCase().includes(q) ||
            c.phone?.toLowerCase().includes(q)
          )
          .slice(0, 4)
          .forEach(c => {
            results.push({
              type: 'customer',
              id: c.id,
              title: c.fullName || 'Unnamed Customer',
              subtitle: c.email || c.phone || '',
              icon: 'person',
              route: `/customers/${c.id}`
            });
          });

        // Search bookings
        bookings.items
          .filter(b =>
            b.id?.toString().includes(q) ||
            b.bookingNumber?.toLowerCase().includes(q) ||
            b.customerName?.toLowerCase().includes(q) ||
            b.routeName?.toLowerCase().includes(q)
          )
          .slice(0, 4)
          .forEach(b => {
            results.push({
              type: 'booking',
              id: b.id,
              title: b.bookingNumber || `Booking #${b.id}`,
              subtitle: b.customerName || b.routeName || '',
              icon: 'confirmation_number',
              route: `/bookings/${b.id}`
            });
          });

        // Search routes
        routes.items
          .filter(r =>
            r.name?.toLowerCase().includes(q) ||
            r.source?.toLowerCase().includes(q) ||
            r.destination?.toLowerCase().includes(q)
          )
          .slice(0, 3)
          .forEach(r => {
            results.push({
              type: 'route',
              id: r.id,
              title: r.name || 'Unnamed Route',
              subtitle: `${r.source || ''} → ${r.destination || ''}`,
              icon: 'alt_route',
              route: `/routes/${r.id}/edit`
            });
          });

        // Search payments
        payments.items
          .filter(p =>
            p.id?.toString().includes(q) ||
            p.bookingId?.toString().includes(q) ||
            p.transactionReference?.toLowerCase().includes(q)
          )
          .slice(0, 3)
          .forEach(p => {
            results.push({
              type: 'payment',
              id: p.id,
              title: `Payment #${p.id}`,
              subtitle: `PKR ${p.amount?.toLocaleString() || '0'} - Booking #${p.bookingId}`,
              icon: 'payment',
              route: `/payments`
            });
          });

        // Search fuel logs
        fuelLogs.items
          .filter(f =>
            f.vehicleName?.toLowerCase().includes(q) ||
            f.driverName?.toLowerCase().includes(q)
          )
          .slice(0, 3)
          .forEach(f => {
            results.push({
              type: 'fuel_log',
              id: f.id,
              title: `Fuel: ${f.vehicleName || 'Vehicle'}`,
              subtitle: `${f.liters}L - PKR ${f.totalCost?.toLocaleString() || '0'}`,
              icon: 'local_gas_station',
              route: `/fuel-logs/${f.id}/edit`
            });
          });

        // Search maintenance
        maintenance.items
          .filter(m =>
            m.vehicleName?.toLowerCase().includes(q) ||
            m.description?.toLowerCase().includes(q) ||
            m.serviceProvider?.toLowerCase().includes(q)
          )
          .slice(0, 3)
          .forEach(m => {
            results.push({
              type: 'maintenance',
              id: m.id,
              title: `Maintenance: ${m.vehicleName || 'Vehicle'}`,
              subtitle: m.description || m.serviceProvider || '',
              icon: 'build',
              route: `/maintenance/${m.id}/edit`
            });
          });

        return results.slice(0, 20);
      })
    );
  }
}
