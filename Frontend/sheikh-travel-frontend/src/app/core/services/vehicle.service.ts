import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Vehicle, CreateVehicleRequest, UpdateVehicleRequest, FuelType, VehicleStatus } from '../models/vehicle.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class VehicleService {
  private readonly base = `${environment.apiUrl}/vehicles`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<Vehicle>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Vehicle>>(this.base, { params }).pipe(
      map(result => ({
        ...result,
        items: result.items.map(v => this.normalizeVehicle(v))
      }))
    );
  }

  getById(id: number): Observable<Vehicle> {
    return this.http.get<Vehicle>(`${this.base}/${id}`).pipe(
      map(v => this.normalizeVehicle(v))
    );
  }

  create(request: CreateVehicleRequest): Observable<Vehicle> {
    return this.http.post<Vehicle>(this.base, request).pipe(
      map(v => this.normalizeVehicle(v))
    );
  }

  update(request: UpdateVehicleRequest): Observable<Vehicle> {
    return this.http.put<Vehicle>(`${this.base}/${request.id}`, request).pipe(
      map(v => this.normalizeVehicle(v))
    );
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  toggleStatus(id: number): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/toggle-status`, {});
  }

  private normalizeVehicle(vehicle: Vehicle): Vehicle {
    return {
      ...vehicle,
      fuelType: this.toFuelType(vehicle.fuelType),
      status: this.toVehicleStatus(vehicle.status)
    };
  }

  private toFuelType(value: FuelType | string): FuelType {
    if (typeof value === 'number') {
      return value;
    }

    switch (value?.toLowerCase()) {
      case 'petrol':
        return FuelType.Petrol;
      case 'diesel':
        return FuelType.Diesel;
      case 'cng':
        return FuelType.CNG;
      default:
        return value as unknown as FuelType;
    }
  }

  private toVehicleStatus(value: VehicleStatus | string): VehicleStatus {
    if (typeof value === 'number') {
      return value;
    }

    switch (value?.toLowerCase()) {
      case 'available':
        return VehicleStatus.Available;
      case 'ontrip':
      case 'on trip':
        return VehicleStatus.OnTrip;
      case 'maintenance':
        return VehicleStatus.Maintenance;
      case 'retired':
        return VehicleStatus.Retired;
      default:
        return value as unknown as VehicleStatus;
    }
  }
}
