import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  IssueTravelSupplierCredit, RecordTravelSupplierPayment, RecordTravelSupplierRefund, SaveTravelExchangeRate, SaveTravelSupplierBill,
  SaveTravelSupplierCommissionRule, SaveTravelSupplierRate, TravelAllocationInput, TravelBillableCost, TravelExchangeRate, TravelPayablesSummary,
  TravelPaymentStatus, TravelRateQuote, TravelRateQuoteRequest, TravelSupplierBalance, TravelSupplierBill, TravelSupplierBillListItem,
  TravelSupplierBillStatus, TravelSupplierCommissionRule, TravelSupplierCredit, TravelSupplierPayment, TravelSupplierPaymentListItem, TravelSupplierRate,
  TravelSupplierRefund, TravelSupplierStatement
} from '../models/travel.models';

export interface TravelSupplierBillQuery {
  page: number; pageSize: number; search?: string; status?: TravelSupplierBillStatus | ''; supplierId?: number | null; currencyCode?: string;
  billFrom?: string; billTo?: string; overdue?: boolean;
}
export interface TravelSupplierPaymentQuery {
  page: number; pageSize: number; search?: string; status?: TravelPaymentStatus | ''; supplierId?: number | null; paidFrom?: string; paidTo?: string;
  reconciled?: boolean | null;
}

type Params = Record<string, string | number | boolean | null | undefined>;

/** Supplier finance (`api/travel/payables`): bills, supplier payments, credits, refunds received, rate cards, commission rules, exchange rates. */
@Injectable({ providedIn: 'root' })
export class TravelSupplierFinanceService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/payables`;

  /** Query params without empty values (an empty date never reaches the API as an unfiltered request). */
  static params(values: Params): HttpParams {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(values)) {
      if (v === null || v === undefined || v === '') continue;
      p = p.set(k, typeof v === 'string' ? v.trim() : v);
    }
    return p;
  }

  // Bills
  getBills(q: TravelSupplierBillQuery): Observable<PagedResult<TravelSupplierBillListItem>> {
    const params = TravelSupplierFinanceService.params({ ...q, overdue: q.overdue ? true : null, search: q.search?.trim() || null });
    return this.http.get<PagedResult<TravelSupplierBillListItem>>(`${this.base}/bills`, { params }).pipe(map(r => normalizePagedResult(r)));
  }
  getBill(id: number): Observable<TravelSupplierBill> { return this.http.get<TravelSupplierBill>(`${this.base}/bills/${id}`); }
  getBillable(supplierId: number, currencyCode: string | null, billId: number | null, billDate: string | null): Observable<TravelBillableCost[]> {
    return this.http.get<TravelBillableCost[]>(`${this.base}/billable`, { params: TravelSupplierFinanceService.params({ supplierId, currencyCode, billId, billDate }) });
  }
  getOpenBills(supplierId: number): Observable<TravelSupplierBillListItem[]> {
    return this.http.get<TravelSupplierBillListItem[]>(`${this.base}/suppliers/${supplierId}/open-bills`);
  }
  createBill(dto: SaveTravelSupplierBill & { supplierId: number; currencyCode: string }): Observable<number> {
    return this.http.post<number>(`${this.base}/bills`, dto);
  }
  updateBill(id: number, dto: SaveTravelSupplierBill): Observable<boolean> { return this.http.put<boolean>(`${this.base}/bills/${id}`, dto); }
  postBill(id: number, billDate: string | null, dueDate: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/bills/${id}/post`, { billDate, dueDate });
  }
  voidBill(id: number, reason: string): Observable<boolean> { return this.http.post<boolean>(`${this.base}/bills/${id}/void`, { reason }); }
  deleteBill(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/bills/${id}`); }

  // Supplier payments
  getPayments(q: TravelSupplierPaymentQuery): Observable<PagedResult<TravelSupplierPaymentListItem>> {
    return this.http.get<PagedResult<TravelSupplierPaymentListItem>>(`${this.base}/payments`, { params: TravelSupplierFinanceService.params({ ...q }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  getPayment(id: number): Observable<TravelSupplierPayment> { return this.http.get<TravelSupplierPayment>(`${this.base}/payments/${id}`); }
  recordPayment(dto: RecordTravelSupplierPayment): Observable<number> { return this.http.post<number>(`${this.base}/payments`, dto); }
  allocate(paymentId: number, allocations: TravelAllocationInput[]): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/allocations`, { allocations });
  }
  unallocate(paymentId: number, billId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/payments/${paymentId}/allocations/${billId}`);
  }
  changePaymentStatus(paymentId: number, status: 'Completed' | 'Failed', note: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/status`, { status, note });
  }
  reconcile(paymentId: number, bankReference: string, note: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/reconcile`, { bankReference, note });
  }

  // Supplier credits and refunds received
  getCredits(page: number, pageSize: number, supplierId?: number | null): Observable<PagedResult<TravelSupplierCredit>> {
    return this.http.get<PagedResult<TravelSupplierCredit>>(`${this.base}/credits`, { params: TravelSupplierFinanceService.params({ page, pageSize, supplierId }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  issueCredit(dto: IssueTravelSupplierCredit): Observable<number> { return this.http.post<number>(`${this.base}/credits`, dto); }
  voidCredit(id: number, reason: string): Observable<boolean> { return this.http.post<boolean>(`${this.base}/credits/${id}/void`, { reason }); }
  getRefunds(page: number, pageSize: number, supplierId?: number | null): Observable<PagedResult<TravelSupplierRefund>> {
    return this.http.get<PagedResult<TravelSupplierRefund>>(`${this.base}/refunds`, { params: TravelSupplierFinanceService.params({ page, pageSize, supplierId }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  recordRefund(dto: RecordTravelSupplierRefund): Observable<number> { return this.http.post<number>(`${this.base}/refunds`, dto); }

  // Reports
  getSummary(): Observable<TravelPayablesSummary> { return this.http.get<TravelPayablesSummary>(`${this.base}/summary`); }
  getBalances(supplierId?: number | null, openOnly = true): Observable<TravelSupplierBalance[]> {
    return this.http.get<TravelSupplierBalance[]>(`${this.base}/balances`, { params: TravelSupplierFinanceService.params({ supplierId, openOnly }) });
  }
  getStatement(supplierId: number, currencyCode: string | null, from: string | null, to: string | null): Observable<TravelSupplierStatement> {
    return this.http.get<TravelSupplierStatement>(`${this.base}/suppliers/${supplierId}/statement`,
      { params: TravelSupplierFinanceService.params({ currencyCode, from, to }) });
  }

  // Exchange rates
  getExchangeRates(): Observable<TravelExchangeRate[]> { return this.http.get<TravelExchangeRate[]>(`${this.base}/exchange-rates`); }
  saveExchangeRate(id: number | null, dto: SaveTravelExchangeRate): Observable<number> {
    return id ? this.http.put<number>(`${this.base}/exchange-rates/${id}`, dto) : this.http.post<number>(`${this.base}/exchange-rates`, dto);
  }
  deleteExchangeRate(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/exchange-rates/${id}`); }

  // Rate cards and supplier commission rules
  getRates(supplierId: number): Observable<TravelSupplierRate[]> { return this.http.get<TravelSupplierRate[]>(`${this.base}/suppliers/${supplierId}/rates`); }
  saveRate(supplierId: number, id: number | null, dto: SaveTravelSupplierRate): Observable<number> {
    return id ? this.http.put<number>(`${this.base}/suppliers/${supplierId}/rates/${id}`, dto) : this.http.post<number>(`${this.base}/suppliers/${supplierId}/rates`, dto);
  }
  deleteRate(supplierId: number, id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/suppliers/${supplierId}/rates/${id}`); }
  quote(dto: TravelRateQuoteRequest): Observable<TravelRateQuote> { return this.http.post<TravelRateQuote>(`${this.base}/rates/quote`, dto); }
  getCommissionRules(supplierId: number): Observable<TravelSupplierCommissionRule[]> {
    return this.http.get<TravelSupplierCommissionRule[]>(`${this.base}/suppliers/${supplierId}/commission-rules`);
  }
  saveCommissionRule(supplierId: number, id: number | null, dto: SaveTravelSupplierCommissionRule): Observable<number> {
    return id ? this.http.put<number>(`${this.base}/suppliers/${supplierId}/commission-rules/${id}`, dto)
      : this.http.post<number>(`${this.base}/suppliers/${supplierId}/commission-rules`, dto);
  }
  deleteCommissionRule(supplierId: number, id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/suppliers/${supplierId}/commission-rules/${id}`);
  }
}
