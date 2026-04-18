import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Vehicle, CreateVehicleRequest, UpdateVehicleRequest } from '../models/vehicle.model';
import { ApiResponse, PagedResult } from '../models/common.model';

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
    return this.http.post<ApiResponse<Vehicle>>(this.base, request).pipe(map(res => res.data));
  }

  update(request: UpdateVehicleRequest): Observable<Vehicle> {
    return this.http.put<ApiResponse<Vehicle>>(`${this.base}/${request.id}`, request).pipe(map(res => res.data));
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/${id}`).pipe(map(res => res.data));
  }

  toggleStatus(id: number): Observable<boolean> {
    return this.http.patch<ApiResponse<boolean>>(`${this.base}/${id}/toggle-status`, {}).pipe(map(res => res.data));
  }
}
