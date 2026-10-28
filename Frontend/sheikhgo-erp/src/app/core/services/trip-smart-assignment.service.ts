import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/common.model';
import {
  ConfirmTripSmartAssignmentRequest,
  OverrideTripSmartAssignmentRequest,
  TripSmartAssignmentApplyResult,
  TripSmartAssignmentDecision,
  TripSmartAssignmentSuggestions
} from '../models/trip-smart-assignment.model';

@Injectable({ providedIn: 'root' })
export class TripSmartAssignmentService {
  private readonly base = `${environment.apiUrl}/trips`;

  constructor(private http: HttpClient) {}

  getSuggestions(tripId: number): Observable<TripSmartAssignmentSuggestions> {
    return this.http
      .get<ApiResponse<TripSmartAssignmentSuggestions>>(`${this.base}/${tripId}/smart-assignment/suggestions`)
      .pipe(map(r => unwrap(r)));
  }

  /** Staff confirms a ranked suggestion (SuggestOnly default). */
  confirm(tripId: number, body: ConfirmTripSmartAssignmentRequest): Observable<TripSmartAssignmentApplyResult> {
    return this.http
      .post<ApiResponse<TripSmartAssignmentApplyResult>>(`${this.base}/${tripId}/smart-assignment/confirm`, body)
      .pipe(map(r => unwrap(r)));
  }

  /** Blocked while SuggestOnly — kept for future AutoApply tenants. */
  autoAssign(tripId: number): Observable<TripSmartAssignmentApplyResult> {
    return this.http
      .post<ApiResponse<TripSmartAssignmentApplyResult>>(`${this.base}/${tripId}/smart-assignment/auto`, {})
      .pipe(map(r => unwrap(r)));
  }

  override(tripId: number, body: OverrideTripSmartAssignmentRequest): Observable<TripSmartAssignmentApplyResult> {
    return this.http
      .post<ApiResponse<TripSmartAssignmentApplyResult>>(`${this.base}/${tripId}/smart-assignment/override`, body)
      .pipe(map(r => unwrap(r)));
  }

  getDecisions(tripId: number): Observable<TripSmartAssignmentDecision[]> {
    return this.http
      .get<ApiResponse<TripSmartAssignmentDecision[]>>(`${this.base}/${tripId}/smart-assignment/decisions`)
      .pipe(map(r => unwrap(r) ?? []));
  }

  getRecent(limit = 50): Observable<TripSmartAssignmentDecision[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http
      .get<ApiResponse<TripSmartAssignmentDecision[]>>(`${this.base}/smart-assignment/recent`, { params })
      .pipe(map(r => unwrap(r) ?? []));
  }
}

function unwrap<T>(r: ApiResponse<T>): T {
  if (!r?.success) {
    throw new Error(r?.message || 'Request failed');
  }
  return r.data as T;
}
