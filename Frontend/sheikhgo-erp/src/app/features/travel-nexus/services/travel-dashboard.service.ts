import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { TravelDashboard, TravelDashboardProfit, TravelUpcoming } from '../models/travel.models';
import { TravelSupplierFinanceService } from './travel-supplier-finance.service';

/** TravelNexus dashboard (`api/travel/dashboard`): one summary call per block, each stating its own window. */
@Injectable({ providedIn: 'root' })
export class TravelDashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/dashboard`;

  getDashboard(): Observable<TravelDashboard> { return this.http.get<TravelDashboard>(this.base); }
  getUpcoming(days: number): Observable<TravelUpcoming> {
    return this.http.get<TravelUpcoming>(`${this.base}/upcoming`, { params: TravelSupplierFinanceService.params({ days }) });
  }
  /** Needs Travel.Profitability.View — call only when the user holds it. */
  getProfit(): Observable<TravelDashboardProfit> { return this.http.get<TravelDashboardProfit>(`${this.base}/profit`); }
}
