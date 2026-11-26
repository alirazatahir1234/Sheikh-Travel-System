import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, Subscription, catchError, finalize, of, tap } from 'rxjs';
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
      // A new alert must be picked up even if a refresh is already running.
      this.refresh(true);
    });
  }

  ngOnDestroy(): void {
    this.hubSub?.unsubscribe();
  }

  private refreshInFlight = false;
  private refreshQueued = false;

  /**
   * Reloads the active alerts. Calls made while a reload is running are collapsed: the session
   * start used to fire two identical requests. `afterCurrent` (hub events) runs one more reload
   * when the current one finishes, so an alert raised mid-request is not missed.
   */
  refresh(afterCurrent = false): void {
    if (!this.auth.isLoggedIn()) {
      this.summary$.next({ newCount: 0, activeCount: 0, items: [] });
      this.newCountSubject.next(0);
      return;
    }
    if (this.refreshInFlight) {
      this.refreshQueued ||= afterCurrent;
      return;
    }
    this.refreshInFlight = true;
    this.getActive().pipe(
      finalize(() => {
        this.refreshInFlight = false;
        if (this.refreshQueued) {
          this.refreshQueued = false;
          this.refresh();
        }
      })
    ).subscribe();
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
