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
  DriverAssignment,
  DriverAttendance,
  DriverAvailability,
  DriverDocument,
  DriverDocumentDetailed,
  DriverListItem,
  DriverLocation,
  DriverPerformanceSummary,
  DriverStats,
  DriverTimelineEvent,
  DriverViolation,
  DriversAvailabilitySummary,
  TransferDriverVehicleRequest,
  UpdateDocumentStatusRequest,
  AddReviewNoteRequest,
  VerificationReviewNote,
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
  availability?: string;
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
    if (params.availability) httpParams = httpParams.set('availability', params.availability);

    return this.http.get<PagedResult<DriverListItem>>(this.base, { params: httpParams }).pipe(
      map(result => ({ ...result, items: result.items.map(normalizeDriverListItem) }))
    );
  }

  getStats(): Observable<DriverStats> {
    return this.http.get<DriverStats>(`${this.base}/stats`);
  }

  getAvailabilitySummary(branchId?: number): Observable<DriversAvailabilitySummary> {
    let params = new HttpParams();
    if (branchId != null) params = params.set('branchId', branchId);
    return this.http.get<DriversAvailabilitySummary>(`${this.base}/availability`, { params });
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

  getDocumentsDetailed(id: number): Observable<DriverDocumentDetailed[]> {
    return this.http.get<DriverDocumentDetailed[]>(`${this.base}/${id}/documents`);
  }

  startReview(id: number): Observable<boolean> {
    return this.updateVerification(id, 'UnderReview');
  }

  updateDocumentStatus(id: number, docId: number, body: UpdateDocumentStatusRequest): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/documents/${docId}/status`, body);
  }

  getReviewNotes(id: number): Observable<VerificationReviewNote[]> {
    return this.http.get<VerificationReviewNote[]>(`${this.base}/${id}/verification/review-notes`);
  }

  addReviewNote(id: number, body: AddReviewNoteRequest): Observable<VerificationReviewNote> {
    return this.http.post<VerificationReviewNote>(`${this.base}/${id}/verification/review-notes`, body);
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

  unassignVehicle(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/unassign-vehicle`, {});
  }

  transferVehicle(id: number, body: TransferDriverVehicleRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/transfer-vehicle`, body);
  }

  getAssignments(id: number, page = 1, pageSize = 20): Observable<PagedResult<DriverAssignment>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<DriverAssignment>>(`${this.base}/${id}/assignments`, { params });
  }

  changeStatus(id: number, status: DriverStatus): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/status`, { status });
  }

  toggleActive(id: number): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/toggle-active`, {});
  }

  getTimeline(id: number): Observable<DriverTimelineEvent[]> {
    return this.http.get<DriverTimelineEvent[]>(`${this.base}/${id}/timeline`);
  }

  getActiveDuty(id: number): Observable<DriverActiveDuty> {
    return this.http.get<DriverActiveDuty>(`${this.base}/${id}/active-duty`);
  }

  getPerformanceSummary(id: number, fromDate?: string, toDate?: string): Observable<DriverPerformanceSummary> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<DriverPerformanceSummary>(`${this.base}/${id}/performance/summary`, { params });
  }

  updateRating(id: number, rating: number): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${id}/rating`, { rating });
  }

  getViolations(id: number, page = 1, pageSize = 20): Observable<PagedResult<DriverViolation>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<DriverViolation>>(`${this.base}/${id}/violations`, { params });
  }

  createViolation(id: number, body: {
    violationType: string;
    severity: string;
    occurredAt: string;
    description?: string | null;
    bookingId?: number | null;
  }): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/violations`, body);
  }

  getAttendance(id: number, fromDate?: string, toDate?: string): Observable<DriverAttendance[]> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<DriverAttendance[]>(`${this.base}/${id}/attendance`, { params });
  }

  createAttendance(id: number, body: {
    attendanceDate: string;
    status: string;
    checkInAt?: string | null;
    checkOutAt?: string | null;
    notes?: string | null;
  }): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/attendance`, body);
  }

  getLocation(id: number): Observable<DriverLocation> {
    return this.http.get<DriverLocation>(`${this.base}/${id}/location`);
  }

  getLocationHistory(id: number, from: string, to: string): Observable<DriverLocation[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<DriverLocation[]>(`${this.base}/${id}/location/history`, { params });
  }
}
