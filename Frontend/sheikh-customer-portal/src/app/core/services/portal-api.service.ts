import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePortalBookingPayload,
  CreatePortalPaymentPayload,
  PortalAuthResultDto,
  PortalBookingCardDto,
  PortalBookingCreatedDto,
  PortalBookingDetailDto,
  PortalBookingTrackingDto,
  PortalCustomerNotificationDto,
  PortalLoyaltyDto,
  PortalNotificationPreferencesDto,
  PortalOtpSentDto,
  PortalPaymentGatewayInfoDto,
  PortalPointToPointQuotePayload,
  PortalPromoResultDto,
  PortalQuoteResultDto,
  PortalRouteDto,
  PortalSavedAddressDto,
  PortalSeatLayoutDto,
  PortalVehicleDto,
  PortalWalletDto,
  PriceBreakdown
} from '../models/portal.models';

@Injectable({ providedIn: 'root' })
export class PortalApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiRoot}/customer-portal`;

  getRoutes(): Observable<PortalRouteDto[]> {
    return this.http.get<PortalRouteDto[]>(`${this.base}/routes`);
  }

  getVehicles(): Observable<PortalVehicleDto[]> {
    return this.http.get<PortalVehicleDto[]>(`${this.base}/vehicles`);
  }

  estimatePrice(routeId: number, vehicleId: number, isRoundTrip: boolean): Observable<PriceBreakdown> {
    return this.http.post<PriceBreakdown>(`${this.base}/price-estimate`, {
      routeId,
      vehicleId,
      isRoundTrip
    });
  }

  quotePointToPoint(body: PortalPointToPointQuotePayload): Observable<PortalQuoteResultDto> {
    return this.http.post<PortalQuoteResultDto>(`${this.base}/quote`, body);
  }

  validatePromo(code: string, quoteTotal: number): Observable<PortalPromoResultDto> {
    return this.http.post<PortalPromoResultDto>(`${this.base}/promo/validate`, { code, quoteTotal });
  }

  getSavedAddresses(): Observable<PortalSavedAddressDto[]> {
    return this.http.get<PortalSavedAddressDto[]>(`${this.base}/addresses`);
  }

  saveAddress(body: { label: string; addressLine: string; latitude: number; longitude: number }): Observable<number> {
    return this.http.post<number>(`${this.base}/addresses`, body);
  }

  getNotifications(unreadOnly = false): Observable<PortalCustomerNotificationDto[]> {
    const params = new HttpParams().set('unreadOnly', unreadOnly ? 'true' : 'false');
    return this.http.get<PortalCustomerNotificationDto[]>(`${this.base}/notifications`, { params });
  }

  getVehicleSeats(vehicleId: number, pickupTimeIso: string): Observable<PortalSeatLayoutDto[]> {
    const params = new HttpParams().set('pickupTime', pickupTimeIso);
    return this.http.get<PortalSeatLayoutDto[]>(`${this.base}/vehicles/${vehicleId}/seats`, { params });
  }

  getLoyalty(): Observable<PortalLoyaltyDto> {
    return this.http.get<PortalLoyaltyDto>(`${this.base}/loyalty`);
  }

  getWallet(): Observable<PortalWalletDto> {
    return this.http.get<PortalWalletDto>(`${this.base}/wallet`);
  }

  createBooking(body: CreatePortalBookingPayload): Observable<PortalBookingCreatedDto> {
    return this.http.post<PortalBookingCreatedDto>(`${this.base}/bookings`, body);
  }

  sendOtp(phone: string): Observable<PortalOtpSentDto> {
    return this.http.post<PortalOtpSentDto>(`${this.base}/auth/send-otp`, { phone });
  }

  verifyOtp(phone: string, code: string, fullName: string): Observable<PortalAuthResultDto> {
    return this.http.post<PortalAuthResultDto>(`${this.base}/auth/verify-otp`, { phone, code, fullName });
  }

  getMyBookings(): Observable<PortalBookingCardDto[]> {
    return this.http.get<PortalBookingCardDto[]>(`${this.base}/my-bookings`);
  }

  getBooking(id: number): Observable<PortalBookingDetailDto> {
    return this.http.get<PortalBookingDetailDto>(`${this.base}/bookings/${id}`);
  }

  getBookingTracking(id: number): Observable<PortalBookingTrackingDto> {
    return this.http.get<PortalBookingTrackingDto>(`${this.base}/bookings/${id}/tracking`);
  }

  downloadInvoice(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/bookings/${id}/invoice`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  cancelBooking(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/bookings/${id}/cancel`, {});
  }

  createPayment(bookingId: number, body: CreatePortalPaymentPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/bookings/${bookingId}/payments`, body);
  }

  getNotificationPreferences(): Observable<PortalNotificationPreferencesDto> {
    return this.http.get<PortalNotificationPreferencesDto>(`${this.base}/notifications/preferences`);
  }

  updateNotificationPreferences(
    body: PortalNotificationPreferencesDto
  ): Observable<PortalNotificationPreferencesDto> {
    return this.http.put<PortalNotificationPreferencesDto>(`${this.base}/notifications/preferences`, body);
  }

  getPaymentGatewayInfo(): Observable<PortalPaymentGatewayInfoDto> {
    return this.http.get<PortalPaymentGatewayInfoDto>(`${this.base}/payment-gateway`);
  }
}
