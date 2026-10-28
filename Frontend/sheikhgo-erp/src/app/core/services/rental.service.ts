import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  CreateRentalBookingRequest,
  CreateRentalRateRequest,
  RentalAvailability,
  RentalBookingDetail,
  RentalBookingFilter,
  RentalBookingListItem,
  RentalPayment,
  RentalQuotation,
  RentalRate,
  UpdateRentalRateRequest
} from '../models/rental.model';

@Injectable({ providedIn: 'root' })
export class RentalService {
  private readonly base = `${environment.apiUrl}/rentals`;

  constructor(private http: HttpClient) {}

  getBookings(page = 1, pageSize = 20, filter?: RentalBookingFilter): Observable<PagedResult<RentalBookingListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.vehicleId) params = params.set('vehicleId', filter.vehicleId);
    if (filter?.customerId) params = params.set('customerId', filter.customerId);
    return this.http.get<PagedResult<RentalBookingListItem>>(`${this.base}/bookings`, { params });
  }

  getBooking(id: number): Observable<RentalBookingDetail> {
    return this.http.get<RentalBookingDetail>(`${this.base}/bookings/${id}`);
  }

  createBooking(body: CreateRentalBookingRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/bookings`, body);
  }

  confirm(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/confirm`, {});
  }

  cancel(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/cancel`, {});
  }

  pickup(id: number, body: { odometer?: number | null; fuelLevel?: string | null; notes?: string | null; holdDeposit?: boolean }): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/pickup`, body);
  }

  returnVehicle(id: number, body: {
    odometer?: number | null;
    fuelLevel?: string | null;
    notes?: string | null;
    damageCharges?: number;
    damageNotes?: string | null;
    depositRefund?: number | null;
  }): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/return`, body);
  }

  close(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/close`, {});
  }

  signAgreement(id: number, body: { signedBy: string; agreementText?: string | null }): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/agreement/sign`, body);
  }

  getPayments(id: number): Observable<RentalPayment[]> {
    return this.http.get<RentalPayment[]>(`${this.base}/bookings/${id}/payments`);
  }

  recordPayment(id: number, body: { amount: number; paymentMethod: string; transactionReference?: string | null; notes?: string | null }): Observable<number> {
    return this.http.post<number>(`${this.base}/bookings/${id}/payments`, body);
  }

  getRates(page = 1, pageSize = 50, vehicleId?: number | null, rateType?: string | null): Observable<PagedResult<RentalRate>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (vehicleId) params = params.set('vehicleId', vehicleId);
    if (rateType) params = params.set('rateType', rateType);
    return this.http.get<PagedResult<RentalRate>>(`${this.base}/rates`, { params });
  }

  createRate(body: CreateRentalRateRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/rates`, body);
  }

  updateRate(id: number, body: UpdateRentalRateRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/rates/${id}`, body);
  }

  deleteRate(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/rates/${id}`);
  }

  checkAvailability(vehicleId: number, from: string, to: string): Observable<RentalAvailability> {
    const params = new HttpParams().set('vehicleId', vehicleId).set('from', from).set('to', to);
    return this.http.get<RentalAvailability>(`${this.base}/availability`, { params });
  }

  quote(vehicleId: number, rateType: string, startAt: string, endAt: string, discountAmount?: number | null): Observable<RentalQuotation> {
    let params = new HttpParams()
      .set('vehicleId', vehicleId)
      .set('rateType', rateType)
      .set('startAt', startAt)
      .set('endAt', endAt);
    if (discountAmount != null) params = params.set('discountAmount', discountAmount);
    return this.http.get<RentalQuotation>(`${this.base}/quote`, { params });
  }
}
