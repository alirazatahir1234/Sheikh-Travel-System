import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  RecordTravelCommissionPayout, SaveTravelCommissionRule, SaveTravelSubAgent, TravelBeneficiaryType, TravelCommissionBalance, TravelCommissionLine,
  TravelCommissionPayout, TravelCommissionRule, TravelCommissionStatus, TravelSubAgent
} from '../models/travel.models';
import { TravelSupplierFinanceService } from './travel-supplier-finance.service';

export interface TravelCommissionLineQuery {
  beneficiaryType?: TravelBeneficiaryType | ''; beneficiaryId?: number | null; status?: TravelCommissionStatus | ''; issuedFrom?: string; issuedTo?: string;
}

/** Agent commission (`api/travel/commissions`): sub-agents, rules, derived lines, balances, payouts, invoice attribution. */
@Injectable({ providedIn: 'root' })
export class TravelCommissionService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/commissions`;

  getSubAgents(isActive?: boolean): Observable<TravelSubAgent[]> {
    return this.http.get<TravelSubAgent[]>(`${this.base}/sub-agents`, { params: TravelSupplierFinanceService.params({ isActive }) });
  }
  saveSubAgent(id: number | null, dto: SaveTravelSubAgent): Observable<number> {
    return id ? this.http.put<number>(`${this.base}/sub-agents/${id}`, dto) : this.http.post<number>(`${this.base}/sub-agents`, dto);
  }
  deleteSubAgent(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/sub-agents/${id}`); }

  getRules(): Observable<TravelCommissionRule[]> { return this.http.get<TravelCommissionRule[]>(`${this.base}/rules`); }
  saveRule(id: number | null, dto: SaveTravelCommissionRule): Observable<number> {
    return id ? this.http.put<number>(`${this.base}/rules/${id}`, dto) : this.http.post<number>(`${this.base}/rules`, dto);
  }
  deleteRule(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/rules/${id}`); }

  getLines(q: TravelCommissionLineQuery): Observable<TravelCommissionLine[]> {
    return this.http.get<TravelCommissionLine[]>(`${this.base}/lines`, { params: TravelSupplierFinanceService.params({ ...q }) });
  }
  getBalances(beneficiaryType?: TravelBeneficiaryType | null, beneficiaryId?: number | null): Observable<TravelCommissionBalance[]> {
    return this.http.get<TravelCommissionBalance[]>(`${this.base}/balances`, { params: TravelSupplierFinanceService.params({ beneficiaryType, beneficiaryId }) });
  }
  getPayouts(beneficiaryType?: TravelBeneficiaryType | null, beneficiaryId?: number | null): Observable<TravelCommissionPayout[]> {
    return this.http.get<TravelCommissionPayout[]>(`${this.base}/payouts`, { params: TravelSupplierFinanceService.params({ beneficiaryType, beneficiaryId }) });
  }
  recordPayout(dto: RecordTravelCommissionPayout): Observable<number> { return this.http.post<number>(`${this.base}/payouts`, dto); }
  updateAttribution(invoiceId: number, agentUserId: number | null, subAgentId: number | null): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/invoices/${invoiceId}/attribution`, { agentUserId, subAgentId });
  }
}
