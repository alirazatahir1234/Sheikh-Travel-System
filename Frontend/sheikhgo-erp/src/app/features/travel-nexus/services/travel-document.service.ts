import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import { TravelCaseDocuments, TravelMissingDocumentsItem, TravelPassportScanResult } from '../models/travel.models';

/**
 * Applicant documents API (`api/travel/documents`) and passport scan.
 * Files are never addressed by URL: they are fetched as authenticated blobs (`downloadFile`).
 */
@Injectable({ providedIn: 'root' })
export class TravelDocumentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/documents`;

  getCaseDocuments(caseId: number): Observable<TravelCaseDocuments> {
    return this.http.get<TravelCaseDocuments>(`${this.base}/visa-cases/${caseId}`);
  }

  getMissing(page: number, pageSize: number): Observable<PagedResult<TravelMissingDocumentsItem>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<TravelMissingDocumentsItem>>(`${this.base}/missing`, { params }).pipe(map(r => normalizePagedResult(r)));
  }

  /** Upload with progress events; the final response body is the new file id. */
  upload(requirementId: number, file: File): Observable<HttpEvent<number>> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<number>(`${this.base}/requirements/${requirementId}/files`, form, { reportProgress: true, observe: 'events' });
  }

  request(caseId: number, applicantId: number, documentTypeId: number, isMandatory: boolean, instructions: string | null): Observable<number> {
    return this.http.post<number>(`${this.base}/visa-cases/${caseId}/requests`, { applicantId, documentTypeId, isMandatory, instructions });
  }

  removeRequirement(requirementId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/requirements/${requirementId}`);
  }

  verify(requirementId: number, expiryDate: string | null, note: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/requirements/${requirementId}/verify`, { expiryDate, note });
  }

  reject(requirementId: number, reason: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/requirements/${requirementId}/reject`, { reason });
  }

  waive(requirementId: number, reason: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/requirements/${requirementId}/waive`, { reason });
  }

  /** Authenticated, audited download as a Blob (never an <a href> to a URL). */
  downloadFile(fileId: number): Observable<Blob> {
    return this.http.get(`${this.base}/files/${fileId}`, { responseType: 'blob' });
  }

  scanPassport(file: File): Observable<TravelPassportScanResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<TravelPassportScanResult>(`${environment.apiUrl}/travel/passport/scan`, form);
  }
}
