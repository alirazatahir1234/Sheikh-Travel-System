import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Vehicle, CreateVehicleRequest, UpdateVehicleRequest } from '../models/vehicle.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class VehicleService {
  private readonly base = `${environment.apiUrl}/vehicles`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<Vehicle>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Vehicle>>(this.base, { params });
  }

  getById(id: number): Observable<Vehicle> {
    return this.http.get<Vehicle>(`${this.base}/${id}`);
  }

  create(request: CreateVehicleRequest): Observable<Vehicle> {
    return this.http.post<Vehicle>(this.base, request);
  }

  update(request: UpdateVehicleRequest): Observable<Vehicle> {
    return this.http.put<Vehicle>(`${this.base}/${request.id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  toggleStatus(id: number): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/toggle-status`, {});
  }

  getDocuments(vehicleId: number): Observable<VehicleDocument[]> {
    return this.http.get<VehicleDocument[]>(`${this.base}/${vehicleId}/documents`);
  }

  addDocument(
    vehicleId: number,
    body: { documentType: string; fileUrl?: string; expiryDate?: string; notes?: string }
  ): Observable<number> {
    return this.http.post<number>(`${this.base}/${vehicleId}/documents`, body);
  }
}

export interface VehicleDocument {
  id: number;
  vehicleId: number;
  documentType: string;
  fileUrl?: string;
  expiryDate?: string;
  notes?: string;
}
