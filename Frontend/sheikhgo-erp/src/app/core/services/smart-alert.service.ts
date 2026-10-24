import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, Subscription, catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SmartAlert, SmartAlertSummary } from '../models/smart-alert.model';
import { NotificationService } from './notification.service';
import { AuthService } from './auth.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';

@Injectable({ providedIn: 'root' })
export class SmartAlertService implements OnDestroy {
  private readonly base = `${environment.apiUrl}/smart-alerts`;
  private readonly summary$ = new BehaviorSubject<SmartAlertSummary>({ newCount: 0, activeCount: 0, items: [] });
  private readonly newCountSubject = new BehaviorSubject<number>(0);
  private hubSub: Subscription | null = null;

  readonly summary = this.summary$.asObservable();
  readonly newCount = this.newCountSubject.asObservable();

  constructor(
    private http: HttpClient,
    private auth: AuthService,
    private notifications: NotificationService,
    private toast: UiToastService
  ) {
    this.hubSub = this.notifications.smartAlertEvents.subscribe(payload => {
      this.toast.info(payload.message || 'New operations alert', `Ops: ${payload.ruleType || 'Alert'}`);
      this.refresh();
    });
  }

  ngOnDestroy(): void {
    this.hubSub?.unsubscribe();
  }

  refresh(): void {
    if (!this.auth.getToken()) {
      this.summary$.next({ newCount: 0, activeCount: 0, items: [] });
      this.newCountSubject.next(0);
      return;
    }
    this.getActive().subscribe();
  }

  getActive(ruleType?: string, limit = 50): Observable<SmartAlertSummary> {
    let params = new HttpParams().set('limit', limit);
    if (ruleType) params = params.set('ruleType', ruleType);

    return this.http.get<SmartAlertSummary>(this.base, { params }).pipe(
      tap(s => {
        const summary = s ?? { newCount: 0, activeCount: 0, items: [] };
        this.summary$.next(summary);
        this.newCountSubject.next(summary.newCount ?? 0);
      }),
      catchError(() => {
        const empty = { newCount: 0, activeCount: 0, items: [] as SmartAlert[] };
        this.summary$.next(empty);
        this.newCountSubject.next(0);
        return of(empty);
      })
    );
  }

  getNewCount(): Observable<number> {
    return this.http.get<number>(`${this.base}/new-count`).pipe(
      tap(c => this.newCountSubject.next(c ?? 0)),
      catchError(() => of(0))
    );
  }

  acknowledge(id: number): Observable<SmartAlert | null> {
    return this.http.post<SmartAlert>(`${this.base}/${id}/acknowledge`, {}).pipe(
      tap(() => this.refresh()),
      catchError(() => of(null))
    );
  }

  resolve(id: number): Observable<SmartAlert | null> {
    return this.http.post<SmartAlert>(`${this.base}/${id}/resolve`, {}).pipe(
      tap(() => this.refresh()),
      catchError(() => of(null))
    );
  }

  dismiss(id: number): Observable<SmartAlert | null> {
    return this.http.post<SmartAlert>(`${this.base}/${id}/dismiss`, {}).pipe(
      tap(() => this.refresh()),
      catchError(() => of(null))
    );
  }
}
