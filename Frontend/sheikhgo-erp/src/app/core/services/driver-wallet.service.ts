import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  DriverCashOutPreview,
  DriverCashOutRequest,
  DriverWallet,
  DriverWalletLedgerEntry,
  DriverWalletPolicy
} from '../models/driver-wallet.model';
import { normalizePagedResult, PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DriverWalletService {
  private readonly adminBase = `${environment.apiUrl}/driver-wallets`;
  private readonly driverBase = `${environment.apiUrl}/driver-app/wallet`;

  constructor(private http: HttpClient) {}

  getPolicy(): Observable<DriverWalletPolicy> {
    return this.http.get<DriverWalletPolicy>(`${this.adminBase}/policy`);
  }

  getDriverPolicy(): Observable<DriverWalletPolicy> {
    return this.http.get<DriverWalletPolicy>(`${environment.apiUrl}/driver-app/wallet/policy`);
  }

  previewCashOut(grossAmount: number, asDriver = false): Observable<DriverCashOutPreview> {
    const base = asDriver ? this.driverBase : this.adminBase;
    const params = new HttpParams().set('grossAmount', grossAmount);
    const path = asDriver ? `${base}/preview` : `${base}/preview`;
    return this.http.get<DriverCashOutPreview>(path, { params });
  }

  getMyWallet(): Observable<DriverWallet> {
    return this.http.get<DriverWallet>(this.driverBase);
  }

  updateMyPayoutDetails(body: Record<string, unknown>): Observable<boolean> {
    return this.http.put<boolean>(`${this.driverBase}/payout-details`, body);
  }

  getMyLedger(page = 1, pageSize = 50): Observable<PagedResult<DriverWalletLedgerEntry>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<PagedResult<DriverWalletLedgerEntry>>(`${this.driverBase}/ledger`, { params })
      .pipe(map(r => normalizePagedResult(r)));
  }

  getMyCashOutRequests(page = 1, pageSize = 50): Observable<PagedResult<DriverCashOutRequest>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<PagedResult<DriverCashOutRequest>>(`${this.driverBase}/cash-out-requests`, { params })
      .pipe(map(r => normalizePagedResult(r)));
  }

  createMyCashOutRequest(body: { grossAmount: number; method: string; notes?: string | null }): Observable<DriverCashOutRequest> {
    return this.http.post<DriverCashOutRequest>(`${this.driverBase}/cash-out-requests`, body);
  }

  getAdminWallets(page = 1, pageSize = 20, driverId?: number | null): Observable<PagedResult<DriverWallet>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (driverId) params = params.set('driverId', driverId);
    return this.http.get<PagedResult<DriverWallet>>(this.adminBase, { params }).pipe(map(r => normalizePagedResult(r)));
  }

  getAdminCashOutRequests(
    page = 1,
    pageSize = 50,
    driverId?: number | null,
    status?: string | null
  ): Observable<PagedResult<DriverCashOutRequest>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (driverId) params = params.set('driverId', driverId);
    if (status) params = params.set('status', status);
    return this.http
      .get<PagedResult<DriverCashOutRequest>>(`${this.adminBase}/cash-out-requests`, { params })
      .pipe(map(r => normalizePagedResult(r)));
  }

  bulkApprove(ids: number[]): Observable<number> {
    return this.http.post<number>(`${this.adminBase}/cash-out-requests/bulk-approve`, { ids });
  }

  reject(id: number, reason: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.adminBase}/cash-out-requests/${id}/reject`, { reason });
  }

  getAdminLedger(page = 1, pageSize = 50, driverId?: number | null): Observable<PagedResult<DriverWalletLedgerEntry>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (driverId) params = params.set('driverId', driverId);
    return this.http
      .get<PagedResult<DriverWalletLedgerEntry>>(`${this.adminBase}/ledger`, { params })
      .pipe(map(r => normalizePagedResult(r)));
  }
}
