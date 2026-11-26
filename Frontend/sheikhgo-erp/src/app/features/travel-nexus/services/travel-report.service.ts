import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { FleetReport } from '../../../core/models/fleet-report.model';
import { TravelReportDefinition, TravelReportGroup } from '../models/travel.models';
import { TravelSupplierFinanceService } from './travel-supplier-finance.service';

export interface TravelReportFilters {
  from?: string; to?: string; branchId?: number | null; agentUserId?: number | null; dimension?: 'Agent' | 'Branch' | ''; countryCode?: string;
}

/** Travel reports (`api/travel/reports`), one route group per permission; the response is the shared self-describing report shape. */
@Injectable({ providedIn: 'root' })
export class TravelReportService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/reports`;

  getCatalog(): Observable<TravelReportDefinition[]> { return this.http.get<TravelReportDefinition[]>(`${this.base}/catalog`); }

  getReport(group: TravelReportGroup, type: string, f: TravelReportFilters): Observable<FleetReport> {
    const prefix = group === 'Finance' ? 'finance/' : group === 'Profit' ? 'profit/' : '';
    return this.http.get<FleetReport>(`${this.base}/${prefix}${type}`, { params: TravelSupplierFinanceService.params({ ...f }) });
  }
}
