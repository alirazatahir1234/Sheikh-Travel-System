import { Injectable } from '@angular/core';
import { forkJoin, Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { CustomerService } from './customer.service';
import { BookingService } from './booking.service';

export interface SearchResult {
  type: 'booking' | 'vehicle' | 'driver' | 'customer';
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
    private bookingService: BookingService
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
      bookings: this.bookingService.getAll(1, 100).pipe(catchError(() => of({ items: [] })))
    }).pipe(
      map(({ vehicles, drivers, customers, bookings }) => {
        const results: SearchResult[] = [];

        // Search vehicles
        vehicles.items
          .filter(v =>
            v.name?.toLowerCase().includes(q) ||
            v.registrationNumber?.toLowerCase().includes(q) ||
            v.model?.toLowerCase().includes(q)
          )
          .slice(0, 5)
          .forEach(v => {
            results.push({
              type: 'vehicle',
              id: v.id,
              title: v.name || 'Unnamed Vehicle',
              subtitle: v.registrationNumber || '',
              icon: 'directions_bus',
              route: `/vehicles/${v.id}/edit`
            });
          });

        // Search drivers
        drivers.items
          .filter(d =>
            d.fullName?.toLowerCase().includes(q) ||
            d.licenseNumber?.toLowerCase().includes(q) ||
            d.phone?.toLowerCase().includes(q)
          )
          .slice(0, 5)
          .forEach(d => {
            results.push({
              type: 'driver',
              id: d.id,
              title: d.fullName || 'Unnamed Driver',
              subtitle: d.licenseNumber || d.phone || '',
              icon: 'badge',
              route: `/drivers/${d.id}/edit`
            });
          });

        // Search customers
        customers.items
          .filter(c =>
            c.fullName?.toLowerCase().includes(q) ||
            c.email?.toLowerCase().includes(q) ||
            c.phone?.toLowerCase().includes(q)
          )
          .slice(0, 5)
          .forEach(c => {
            results.push({
              type: 'customer',
              id: c.id,
              title: c.fullName || 'Unnamed Customer',
              subtitle: c.email || c.phone || '',
              icon: 'person',
              route: `/customers/${c.id}/edit`
            });
          });

        // Search bookings
        bookings.items
          .filter(b =>
            b.id?.toString().includes(q) ||
            b.customerName?.toLowerCase().includes(q) ||
            b.routeName?.toLowerCase().includes(q)
          )
          .slice(0, 5)
          .forEach(b => {
            results.push({
              type: 'booking',
              id: b.id,
              title: `Booking #${b.id}`,
              subtitle: b.customerName || b.routeName || '',
              icon: 'confirmation_number',
              route: `/bookings/${b.id}`
            });
          });

        return results.slice(0, 15);
      })
    );
  }
}
