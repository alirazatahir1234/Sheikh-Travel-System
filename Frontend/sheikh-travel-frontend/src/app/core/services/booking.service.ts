import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Booking, CreateBookingRequest, AssignDriverRequest,
  AssignVehicleRequest, UpdateBookingStatusRequest
} from '../models/booking.model';
import { PagedResult } from '../models/common.model';
import { PriceCalculationRequest, PriceBreakdown } from '../models/pricing.model';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly base = `${environment.apiUrl}/bookings`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string): Observable<PagedResult<Booking>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<Booking>>(this.base, { params });
  }

  getById(id: number): Observable<Booking> {
    return this.http.get<Booking>(`${this.base}/${id}`);
  }

  create(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.base, request);
  }

  assignDriver(request: AssignDriverRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/assign-driver`, request);
  }

  assignVehicle(request: AssignVehicleRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/assign-vehicle`, request);
  }

  updateStatus(request: UpdateBookingStatusRequest): Observable<boolean> {
    return this.http.patch<boolean>(`${this.base}/${request.bookingId}/status`, request);
  }

  calculatePrice(request: PriceCalculationRequest): Observable<PriceBreakdown> {
    return this.http.post<PriceBreakdown>(`${environment.apiUrl}/pricing/calculate`, request);
  }
}
