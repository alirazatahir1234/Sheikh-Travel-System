import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AssignDriverVehicleRequest,
  CheckDriverAvailabilityParams,
  CreateDriverDto,
  CreateDriverRequest,
  Driver,
  DriverActiveDuty,
  DriverAvailability,
  DriverDocument,
  DriverListItem,
  DriverStats,
  DriverTimelineEvent,
  normalizeDriver,
  normalizeDriverListItem,
  sanitizeCreateDriverDto,
  sanitizeUpdateDriverDto,
  UpdateDriverRequest
} from '../models/driver.model';
import { PagedResult } from '../models/common.model';
import { DriverStatus } from '../models/driver.model';

export interface DriverListParams {
  page?: number;
  pageSize?: number;
  q?: string;
  status?: DriverStatus;
  branchId?: number;
  licenseExpiry?: 'VALID' | 'EXPIRING' | 'EXPIRED';
  verificationStatus?: string;
}

@Injectable({ providedIn: 'root' })
export class DriverService {
  private readonly base = `${environment.apiUrl}/drivers`;

  constructor(private http: HttpClient) {}

  getAll(paramsOrPage: DriverListParams | number = {}, pageSize?: number): Observable<PagedResult<DriverListItem>> {
    const params: DriverListParams =
      typeof paramsOrPage === 'number' ? { page: paramsOrPage, pageSize: pageSize ?? 25 } : paramsOrPage;
    let httpParams = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 25);
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.status != null) httpParams = httpParams.set('status', params.status);
    if (params.branchId != null) httpParams = httpParams.set('branchId', params.branchId);
    if (params.licenseExpiry) httpParams = httpParams.set('licenseExpiry', params.licenseExpiry);
    if (params.verificationStatus) httpParams = httpParams.set('verificationStatus', params.verificationStatus);

    return this.http.get<PagedResult<DriverListItem>>(this.base, { params: httpParams }).pipe(
      map(result => ({ ...result, items: result.items.map(normalizeDriverListItem) }))
    );
  }

  getStats(): Observable<DriverStats> {
    return this.http.get<DriverStats>(`${this.base}/stats`);
  }

  getById(id: number): Observable<Driver> {
    return this.http.get<Driver>(`${this.base}/${id}`).pipe(map(normalizeDriver));
  }

  checkAvailability(params: CheckDriverAvailabilityParams): Observable<DriverAvailability> {
    let httpParams = new HttpParams();
    if (params.phone) httpParams = httpParams.set('phone', params.phone);
    if (params.email) httpParams = httpParams.set('email', params.email);
    if (params.licenseNumber) httpParams = httpParams.set('licenseNumber', params.licenseNumber);
    if (params.excludeDriverId != null) httpParams = httpParams.set('excludeDriverId', params.excludeDriverId);
    return this.http.get<DriverAvailability>(`${this.base}/check-availability`, { params: httpParams });
  }

  create(request: CreateDriverRequest): Observable<number> {
    return this.http.post<number>(this.base, { driver: sanitizeCreateDriverDto(request.driver) });
  }

  update(request: UpdateDriverRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, {
      id: request.id,
      driver: sanitizeUpdateDriverDto(request.driver)
    });
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  getDocuments(id: number): Observable<DriverDocument[]> {
    return this.http.get<DriverDocument[]>(`${this.base}/${id}/documents`);
  }

  uploadDocument(id: number, documentType: string, file: File, expiryDate?: string): Observable<{ documentId: number; fileUrl: string; documentType: string }> {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', documentType);
    if (expiryDate) form.append('expiryDate', expiryDate);
    return this.http.post<{ documentId: number; fileUrl: string; documentType: string }>(`${this.base}/${id}/documents/upload`, form);
  }

  uploadPhoto(id: number, file: File): Observable<string> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<string>(`${this.base}/${id}/photo/upload`, form);
  }

  updateVerification(id: number, verificationStatus: string): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/verification`, { verificationStatus });
  }

  assignVehicle(id: number, body: AssignDriverVehicleRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/assign-vehicle`, body);
  }

  changeStatus(id: number, status: DriverStatus): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/status`, { status });
  }

  getTimeline(id: number): Observable<DriverTimelineEvent[]> {
    return this.http.get<DriverTimelineEvent[]>(`${this.base}/${id}/timeline`);
  }

  getActiveDuty(id: number): Observable<DriverActiveDuty> {
    return this.http.get<DriverActiveDuty>(`${this.base}/${id}/active-duty`);
  }
}
