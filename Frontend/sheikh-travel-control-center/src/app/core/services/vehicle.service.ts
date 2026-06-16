import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Vehicle,
  VehicleListItem,
  VehicleStatus,
  CreateVehicleRequest,
  CreateVehicleDto,
  UpdateVehicleRequest,
  VehicleDocument,
  VehicleMaintenance,
  VehicleFuelSummary,
  VehicleGps,
  ChangeVehicleStatusRequest,
  AssignVehicleDriverRequest,
  AssignVehicleGpsRequest,
  UploadVehicleDocumentResult,
  sanitizeCreateVehicleDto,
  sanitizeUpdateVehicleDto,
  normalizeVehicle,
  normalizeVehicleStatus
} from '../models/vehicle.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class VehicleService {
  private readonly base = `${environment.apiUrl}/vehicles`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<VehicleListItem>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<VehicleListItem>>(this.base, { params });
  }

  getById(id: number): Observable<Vehicle> {
    return this.http.get<Vehicle>(`${this.base}/${id}`).pipe(
      map(vehicle => normalizeVehicle(vehicle))
    );
  }

  create(request: CreateVehicleRequest): Observable<number> {
    return this.http.post<number>(this.base, {
      ...request,
      vehicle: sanitizeCreateVehicleDto(request.vehicle)
    });
  }

  createDraft(vehicle: CreateVehicleDto): Observable<number> {
    return this.http.post<number>(this.base, {
      vehicle: sanitizeCreateVehicleDto(vehicle),
      saveAsDraft: true
    });
  }

  publish(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/publish`, {});
  }

  uploadDocument(
    vehicleId: number,
    file: File,
    documentType: string,
    expiryDate?: string,
    notes?: string
  ): Observable<UploadVehicleDocumentResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    if (expiryDate) formData.append('expiryDate', expiryDate);
    if (notes) formData.append('notes', notes);
    return this.http.post<UploadVehicleDocumentResult>(
      `${this.base}/${vehicleId}/documents/upload`,
      formData
    );
  }

  update(request: UpdateVehicleRequest): Observable<boolean> {
    const body: UpdateVehicleRequest = {
      id: request.id,
      vehicle: sanitizeUpdateVehicleDto(request.vehicle)
    };
    return this.http.put<boolean>(`${this.base}/${request.id}`, body);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  toggleStatus(id: number): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/toggle-status`, {});
  }

  changeStatus(id: number, body: ChangeVehicleStatusRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/change-status`, {
      ...body,
      status: normalizeVehicleStatus(body.status)
    });
  }

  assignDriver(id: number, body: AssignVehicleDriverRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/assign-driver`, body);
  }

  assignGps(id: number, body: AssignVehicleGpsRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/assign-gps`, body);
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

  getMaintenance(vehicleId: number, page = 1, pageSize = 20): Observable<PagedResult<VehicleMaintenance>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<VehicleMaintenance>>(`${this.base}/${vehicleId}/maintenance`, { params });
  }

  getFuel(vehicleId: number, page = 1, pageSize = 20): Observable<VehicleFuelSummary> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<VehicleFuelSummary>(`${this.base}/${vehicleId}/fuel`, { params });
  }

  getGps(vehicleId: number): Observable<VehicleGps> {
    return this.http.get<VehicleGps>(`${this.base}/${vehicleId}/gps`);
  }
}
