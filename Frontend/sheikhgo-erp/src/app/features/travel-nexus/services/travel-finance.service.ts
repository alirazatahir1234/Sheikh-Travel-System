import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  RecordTravelPayment, RecordTravelRefund, SaveTravelInvoice, TravelAllocationInput, TravelBillableItem, TravelChangedInvoiceLine, TravelCreditNote,
  TravelCustomerBalance, TravelFinanceSummary, TravelGroupFinanceRow, TravelInvoice, TravelInvoiceListItem, TravelInvoicePaymentStatus,
  TravelInvoiceStatus, TravelPayment, TravelPaymentListItem, TravelPaymentMethod, TravelPaymentStatus, TravelRefund, TravelStatement
} from '../models/travel.models';

export interface TravelInvoiceQuery {
  page: number; pageSize: number; search?: string; status?: TravelInvoiceStatus | ''; paymentStatus?: TravelInvoicePaymentStatus | '';
  customerId?: number | null; currencyCode?: string; issueFrom?: string; issueTo?: string; dueFrom?: string; dueTo?: string; overdue?: boolean;
}
export interface TravelPaymentQuery {
  page: number; pageSize: number; search?: string; status?: TravelPaymentStatus | ''; method?: TravelPaymentMethod | ''; customerId?: number | null;
  receivedFrom?: string; receivedTo?: string; reconciled?: boolean | null;
}

/** New client request key for an idempotent money write (one per dialog, reused on retry). */
export const newRequestKey = (): string => crypto.randomUUID();

/** Travel customer finance (`api/travel/finance`). */
@Injectable({ providedIn: 'root' })
export class TravelFinanceService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/finance`;

  private params(values: Record<string, string | number | boolean | null | undefined>): HttpParams {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(values)) {
      if (v === null || v === undefined || v === '') continue;
      p = p.set(k, typeof v === 'string' ? v.trim() : v);
    }
    return p;
  }

  // Invoices
  getInvoices(q: TravelInvoiceQuery): Observable<PagedResult<TravelInvoiceListItem>> {
    const params = this.params({ ...q, overdue: q.overdue ? true : null, search: q.search?.trim() || null });
    return this.http.get<PagedResult<TravelInvoiceListItem>>(`${this.base}/invoices`, { params }).pipe(map(r => normalizePagedResult(r)));
  }
  getInvoice(id: number): Observable<TravelInvoice> { return this.http.get<TravelInvoice>(`${this.base}/invoices/${id}`); }
  getBillable(customerId: number, currencyCode: string | null, invoiceId: number | null): Observable<TravelBillableItem[]> {
    return this.http.get<TravelBillableItem[]>(`${this.base}/billable`, { params: this.params({ customerId, currencyCode, invoiceId }) });
  }
  getOpenInvoices(customerId: number): Observable<TravelInvoiceListItem[]> {
    return this.http.get<TravelInvoiceListItem[]>(`${this.base}/customers/${customerId}/open-invoices`);
  }
  createInvoice(dto: SaveTravelInvoice & { customerId: number; currencyCode: string }): Observable<number> {
    return this.http.post<number>(`${this.base}/invoices`, dto);
  }
  updateInvoice(id: number, dto: SaveTravelInvoice): Observable<boolean> { return this.http.put<boolean>(`${this.base}/invoices/${id}`, dto); }
  issueInvoice(id: number, issueDate: string | null, dueDate: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/invoices/${id}/issue`, { issueDate, dueDate });
  }
  voidInvoice(id: number, reason: string): Observable<boolean> { return this.http.post<boolean>(`${this.base}/invoices/${id}/void`, { reason }); }
  deleteInvoice(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/invoices/${id}`); }

  // Payments
  getPayments(q: TravelPaymentQuery): Observable<PagedResult<TravelPaymentListItem>> {
    return this.http.get<PagedResult<TravelPaymentListItem>>(`${this.base}/payments`, { params: this.params({ ...q }) }).pipe(map(r => normalizePagedResult(r)));
  }
  getPayment(id: number): Observable<TravelPayment> { return this.http.get<TravelPayment>(`${this.base}/payments/${id}`); }
  recordPayment(dto: RecordTravelPayment): Observable<number> { return this.http.post<number>(`${this.base}/payments`, dto); }
  allocate(paymentId: number, allocations: TravelAllocationInput[]): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/allocations`, { allocations });
  }
  unallocate(paymentId: number, invoiceId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/payments/${paymentId}/allocations/${invoiceId}`);
  }
  changePaymentStatus(paymentId: number, status: 'Completed' | 'Failed', note: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/status`, { status, note });
  }
  reconcile(paymentId: number, bankReference: string, note: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/payments/${paymentId}/reconcile`, { bankReference, note });
  }

  // Credit notes and refunds
  getCreditNotes(page: number, pageSize: number, customerId?: number | null, from?: string, to?: string): Observable<PagedResult<TravelCreditNote>> {
    return this.http.get<PagedResult<TravelCreditNote>>(`${this.base}/credit-notes`, { params: this.params({ page, pageSize, customerId, from, to }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  issueCreditNote(dto: { idempotencyKey: string; invoiceId: number; amount: number; reason: string; cancellationId: number | null; issuedOn: string | null }): Observable<number> {
    return this.http.post<number>(`${this.base}/credit-notes`, dto);
  }
  voidCreditNote(id: number, reason: string): Observable<boolean> { return this.http.post<boolean>(`${this.base}/credit-notes/${id}/void`, { reason }); }
  getRefunds(page: number, pageSize: number, customerId?: number | null, from?: string, to?: string): Observable<PagedResult<TravelRefund>> {
    return this.http.get<PagedResult<TravelRefund>>(`${this.base}/refunds`, { params: this.params({ page, pageSize, customerId, from, to }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  recordRefund(dto: RecordTravelRefund): Observable<number> { return this.http.post<number>(`${this.base}/refunds`, dto); }

  // Reports
  getSummary(): Observable<TravelFinanceSummary> { return this.http.get<TravelFinanceSummary>(`${this.base}/summary`); }
  getBalances(customerId?: number | null, openOnly = true): Observable<TravelCustomerBalance[]> {
    return this.http.get<TravelCustomerBalance[]>(`${this.base}/balances`, { params: this.params({ customerId, openOnly }) });
  }
  getStatement(customerId: number, currencyCode: string | null, from: string | null, to: string | null): Observable<TravelStatement> {
    return this.http.get<TravelStatement>(`${this.base}/customers/${customerId}/statement`, { params: this.params({ currencyCode, from, to }) });
  }
  getUninvoiced(): Observable<TravelBillableItem[]> { return this.http.get<TravelBillableItem[]>(`${this.base}/reconciliation/uninvoiced`); }
  getChangedLines(): Observable<TravelChangedInvoiceLine[]> { return this.http.get<TravelChangedInvoiceLine[]>(`${this.base}/reconciliation/changed-lines`); }
  getGroupFinance(groupId: number): Observable<TravelGroupFinanceRow[]> { return this.http.get<TravelGroupFinanceRow[]>(`${this.base}/groups/${groupId}`); }
}
