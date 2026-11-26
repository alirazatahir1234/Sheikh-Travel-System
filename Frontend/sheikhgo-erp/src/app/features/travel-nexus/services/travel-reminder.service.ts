import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  SaveTravelReminderRule, TravelReminderAudience, TravelReminderLogItem, TravelReminderRule, TravelReminderRuleType, TravelReminderRunResult, TravelReminderStatus
} from '../models/travel.models';
import { TravelSupplierFinanceService } from './travel-supplier-finance.service';

export interface TravelReminderLogQuery {
  page: number; pageSize: number; ruleType?: TravelReminderRuleType | ''; status?: TravelReminderStatus | ''; audience?: TravelReminderAudience | '';
  from?: string; to?: string;
}

/** Reminder rules and log (`api/travel/reminders`). */
@Injectable({ providedIn: 'root' })
export class TravelReminderService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/reminders`;

  getRules(): Observable<TravelReminderRule[]> { return this.http.get<TravelReminderRule[]>(`${this.base}/rules`); }
  saveRule(ruleType: TravelReminderRuleType, dto: SaveTravelReminderRule): Observable<TravelReminderRule> {
    return this.http.put<TravelReminderRule>(`${this.base}/rules/${ruleType}`, dto);
  }
  getLog(q: TravelReminderLogQuery): Observable<PagedResult<TravelReminderLogItem>> {
    return this.http.get<PagedResult<TravelReminderLogItem>>(`${this.base}/log`, { params: TravelSupplierFinanceService.params({ ...q }) })
      .pipe(map(r => normalizePagedResult(r)));
  }
  run(): Observable<TravelReminderRunResult> { return this.http.post<TravelReminderRunResult>(`${this.base}/run`, {}); }
}
