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
import { NavItem, ResolvedMenu } from '../navigation/nav-models';

export type SearchResultType =
  | 'module'
  | 'booking'
  | 'vehicle'
  | 'driver'
  | 'customer'
  | 'route'
  | 'payment'
  | 'fuel_log'
  | 'maintenance';

export interface SearchResult {
  type: SearchResultType;
  id: number | string;
  title: string;
  subtitle: string;
  icon: string;
  route: string;
  queryParams?: Record<string, string>;
}

export interface GlobalSearchOptions {
  /** Flattened nav from the current shell menu — used for module/page jumps. */
  menu?: ResolvedMenu | null;
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

  search(query: string, options: GlobalSearchOptions = {}): Observable<SearchResult[]> {
    if (!query || query.trim().length < 2) {
      return of([]);
    }

    const q = query.trim().toLowerCase();
    const moduleResults = this.searchModules(q, options.menu);

    return forkJoin({
      vehicles: this.vehicleService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      drivers: this.driverService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      customers: this.customerService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      bookings: this.bookingService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      routes: this.routeService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      payments: this.paymentService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      fuelLogs: this.fuelLogService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] }))),
      maintenance: this.maintenanceService.getAll(1, 100).pipe(catchError(() => of({ items: [] as never[] })))
    }).pipe(
      map(({ vehicles, drivers, customers, bookings, routes, payments, fuelLogs, maintenance }) => {
        const results: SearchResult[] = [...moduleResults];

        const vehicleItems = asItems(vehicles);
        vehicleItems
          .filter(v =>
            includes(v.name, q) ||
            includes(v.registrationNumber, q) ||
            includes(v.vehicleCode, q) ||
            includes(v.model, q) ||
            includes(v.driverName, q) ||
            includes(v.gpsImei, q) ||
            includes(v.gpsSim, q) ||
            includes(v.vin, q)
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

        const driverItems = asItems(drivers);
        driverItems
          .filter(d =>
            includes(d.fullName, q) ||
            includes(d.licenseNumber, q) ||
            includes(d.phone, q)
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

        const customerItems = asItems(customers);
        customerItems
          .filter(c =>
            includes(c.fullName, q) ||
            includes(c.email, q) ||
            includes(c.phone, q)
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

        const bookingItems = asItems(bookings);
        bookingItems
          .filter(b =>
            b.id?.toString().includes(q) ||
            includes(b.bookingNumber, q) ||
            includes(b.customerName, q) ||
            includes(b.routeName, q)
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

        const routeItems = asItems(routes);
        routeItems
          .filter(r =>
            includes(r.name, q) ||
            includes(r.source, q) ||
            includes(r.destination, q)
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

        const paymentItems = asItems(payments);
        paymentItems
          .filter(p =>
            p.id?.toString().includes(q) ||
            p.bookingId?.toString().includes(q) ||
            includes(p.transactionReference, q)
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

        const fuelItems = asItems(fuelLogs);
        fuelItems
          .filter(f =>
            includes(f.vehicleName, q) ||
            includes(f.driverName, q)
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

        const maintenanceItems = asItems(maintenance);
        maintenanceItems
          .filter(m =>
            includes(m.vehicleName, q) ||
            includes(m.description, q) ||
            includes(m.serviceProvider, q)
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
      }),
      catchError(() => of(moduleResults))
    );
  }

  /** Instant module/page matches from the resolved sidebar menu. */
  searchModules(query: string, menu?: ResolvedMenu | null): SearchResult[] {
    const q = query.trim().toLowerCase();
    if (q.length < 2 || !menu) return [];

    const seen = new Set<string>();
    const results: SearchResult[] = [];

    const matchesQuery = (text: string): boolean => {
      const hay = text.toLowerCase();
      if (hay.includes(q)) return true;
      const words = q.split(/\s+/).filter(Boolean);
      return words.length > 1 && words.every(w => hay.includes(w));
    };

    const consider = (item: NavItem, groupLabel?: string) => {
      const haystack = [item.label, groupLabel, item.route, item.id, item.moduleKey]
        .filter(Boolean)
        .join(' ');
      if (!matchesQuery(haystack)) return;

      const key = `${item.route}|${JSON.stringify(item.queryParams ?? {})}`;
      if (seen.has(key)) return;
      seen.add(key);

      results.push({
        type: 'module',
        id: item.id,
        title: item.label,
        subtitle: groupLabel ? `${groupLabel} · Go to page` : 'Go to page',
        icon: item.icon || 'open_in_new',
        route: item.route,
        queryParams: item.queryParams
      });
    };

    for (const group of menu.groups ?? []) {
      // Typing a group name (e.g. "Fleet Management") surfaces its first page
      if (matchesQuery(group.label) && group.items.length) {
        consider(group.items[0], group.label);
      }
      for (const item of group.items ?? []) {
        consider(item, group.label);
      }
    }
    for (const item of menu.standaloneItems ?? []) {
      consider(item);
    }

    return results.slice(0, 8);
  }
}

function includes(value: string | null | undefined, q: string): boolean {
  return !!value && value.toLowerCase().includes(q);
}

function asItems<T>(page: { items?: T[] } | T[] | null | undefined): T[] {
  if (!page) return [];
  if (Array.isArray(page)) return page;
  return Array.isArray(page.items) ? page.items : [];
}
