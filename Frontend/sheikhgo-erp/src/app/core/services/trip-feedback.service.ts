import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/common.model';

export interface TripClientRating {
  id: number;
  tripId: number;
  tripNumber?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  score: number;
  politeness?: number | null;
  onTimeDelivery?: number | null;
  vehicleCondition?: number | null;
  comment?: string | null;
  source: string;
  isHidden: boolean;
  createdAt: string;
}

export interface DriverClientRatingSummary {
  averageRating?: number | null;
  reviewCount: number;
  avgPoliteness?: number | null;
  avgOnTime?: number | null;
  avgVehicle?: number | null;
}

export interface PublicDriverRating {
  displayName: string;
  averageRating: number;
  reviewCount: number;
  avgPoliteness?: number | null;
  avgOnTime?: number | null;
  avgVehicle?: number | null;
}

export interface TripFeedbackForm {
  tripNumber: string;
  driverFirstName?: string | null;
  alreadySubmitted: boolean;
  expiresAt: string;
}

function unwrap<T>(res: ApiResponse<T>): T {
  if (!res.success || res.data == null) {
    throw new Error(res.message || 'Request failed');
  }
  return res.data;
}

function normalizeRating(row: Record<string, unknown>): TripClientRating {
  return {
    id: Number(row['id'] ?? row['Id']),
    tripId: Number(row['tripId'] ?? row['TripId']),
    tripNumber: (row['tripNumber'] ?? row['TripNumber']) as string | null,
    driverId: (row['driverId'] ?? row['DriverId']) as number | null,
    driverName: (row['driverName'] ?? row['DriverName']) as string | null,
    score: Number(row['score'] ?? row['Score']),
    politeness: (row['politeness'] ?? row['Politeness']) as number | null,
    onTimeDelivery: (row['onTimeDelivery'] ?? row['OnTimeDelivery']) as number | null,
    vehicleCondition: (row['vehicleCondition'] ?? row['VehicleCondition']) as number | null,
    comment: (row['comment'] ?? row['Comment']) as string | null,
    source: String(row['source'] ?? row['Source'] ?? ''),
    isHidden: Boolean(row['isHidden'] ?? row['IsHidden']),
    createdAt: String(row['createdAt'] ?? row['CreatedAt'] ?? ''),
  };
}

@Injectable({ providedIn: 'root' })
export class TripFeedbackService {
  private readonly adminBase = `${environment.apiUrl}/trip-feedback`;
  private readonly publicBase = `${environment.apiUrl}/public/trip-feedback`;
  private readonly websiteBase = `${environment.apiUrl}/website/public`;

  constructor(private http: HttpClient) {}

  list(page = 1, pageSize = 25, driverId?: number, includeHidden = false): Observable<PagedResult<TripClientRating>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize).set('includeHidden', includeHidden);
    if (driverId != null) params = params.set('driverId', driverId);
    return this.http.get<ApiResponse<PagedResult<TripClientRating>>>(this.adminBase, { params }).pipe(
      map(res => {
        const data = unwrap(res);
        return {
          ...data,
          items: (data.items ?? []).map(i => normalizeRating(i as unknown as Record<string, unknown>)),
        };
      })
    );
  }

  getDriverSummary(driverId: number): Observable<DriverClientRatingSummary> {
    return this.http
      .get<ApiResponse<DriverClientRatingSummary>>(`${this.adminBase}/drivers/${driverId}/summary`)
      .pipe(map(unwrap));
  }

  setVisibility(ratingId: number, hidden: boolean): Observable<void> {
    return this.http
      .patch<ApiResponse<boolean>>(`${this.adminBase}/${ratingId}/visibility`, { hidden })
      .pipe(map(() => undefined));
  }

  setDriverPublicOptOut(driverId: number, optOut: boolean): Observable<void> {
    return this.http
      .patch<ApiResponse<boolean>>(`${this.adminBase}/drivers/${driverId}/public-profile`, { optOut })
      .pipe(map(() => undefined));
  }

  getPublicForm(token: string): Observable<TripFeedbackForm> {
    return this.http.get<ApiResponse<TripFeedbackForm>>(`${this.publicBase}/${encodeURIComponent(token)}`).pipe(map(unwrap));
  }

  submitPublic(token: string, body: { politeness: number; onTimeDelivery: number; vehicleCondition: number; comment?: string }): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${this.publicBase}/${encodeURIComponent(token)}`, body).pipe(map(() => undefined));
  }

  getPublicDriverRatings(take = 12): Observable<PublicDriverRating[]> {
    const params = new HttpParams().set('take', take);
    return this.http
      .get<ApiResponse<PublicDriverRating[]>>(`${this.websiteBase}/driver-ratings`, { params })
      .pipe(map(unwrap));
  }
}
