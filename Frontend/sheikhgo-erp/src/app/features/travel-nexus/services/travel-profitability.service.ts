import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { TravelContextProfitDetail, TravelContextType, TravelProfitDimension, TravelProfitabilityReport } from '../models/travel.models';
import { TravelSupplierFinanceService } from './travel-supplier-finance.service';

/** Margins in the reporting currency (`api/travel/profitability`). Both travel dates are required by the API. */
@Injectable({ providedIn: 'root' })
export class TravelProfitabilityService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/profitability`;

  getReport(dimension: TravelProfitDimension, travelFrom: string, travelTo: string, branchId?: number | null, agentUserId?: number | null): Observable<TravelProfitabilityReport> {
    return this.http.get<TravelProfitabilityReport>(this.base,
      { params: TravelSupplierFinanceService.params({ dimension, travelFrom, travelTo, branchId, agentUserId }) });
  }
  getContext(contextType: TravelContextType, contextId: number): Observable<TravelContextProfitDetail> {
    return this.http.get<TravelContextProfitDetail>(`${this.base}/${contextType}/${contextId}`);
  }
}
