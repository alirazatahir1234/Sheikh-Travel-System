import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePortalBookingPayload,
  CreatePortalPaymentPayload,
  PortalBookingCardDto,
  PortalBookingCreatedDto,
  PortalBookingDetailDto,
  PortalRouteDto,
  PortalVehicleDto,
  PriceBreakdown
} from '../models/portal.models';

type ApiEnvelope<T> = {
  success: boolean;
  message?: string;
  data: T;
  errors?: unknown;
};

@Injectable({ providedIn: 'root' })
export class PortalApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiRoot}/customer-portal`;

  getRoutes(): Observable<PortalRouteDto[]> {
    return this.http
      .get<ApiEnvelope<PortalRouteDto[]>>(`${this.base}/routes`)
      .pipe(map((res) => res.data ?? []));
  }

  getVehicles(): Observable<PortalVehicleDto[]> {
    return this.http
      .get<ApiEnvelope<PortalVehicleDto[]>>(`${this.base}/vehicles`)
      .pipe(map((res) => res.data ?? []));
  }

  estimatePrice(routeId: number, vehicleId: number, isRoundTrip: boolean): Observable<PriceBreakdown> {
    return this.http
      .post<ApiEnvelope<PriceBreakdown>>(`${this.base}/price-estimate`, {
        routeId,
        vehicleId,
        isRoundTrip
      })
      .pipe(map((res) => res.data));
  }

  createBooking(body: CreatePortalBookingPayload): Observable<PortalBookingCreatedDto> {
    return this.http
      .post<ApiEnvelope<PortalBookingCreatedDto>>(`${this.base}/bookings`, body)
      .pipe(map((res) => res.data));
  }

  getMyBookings(phone: string): Observable<PortalBookingCardDto[]> {
    const params = new HttpParams().set('phone', phone);
    return this.http
      .get<ApiEnvelope<PortalBookingCardDto[]>>(`${this.base}/my-bookings`, { params })
      .pipe(map((res) => res.data ?? []));
  }

  getBooking(id: number, phone: string): Observable<PortalBookingDetailDto> {
    const params = new HttpParams().set('phone', phone);
    return this.http
      .get<ApiEnvelope<PortalBookingDetailDto>>(`${this.base}/bookings/${id}`, { params })
      .pipe(map((res) => res.data));
  }

  createPayment(bookingId: number, body: CreatePortalPaymentPayload): Observable<number> {
    return this.http
      .post<ApiEnvelope<number>>(`${this.base}/bookings/${bookingId}/payments`, body)
      .pipe(map((res) => res.data));
  }
}
