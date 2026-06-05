import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, interval, switchMap, tap, catchError, of, Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import { Notification } from '../models/notification.model';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly base = `${environment.apiUrl}/notifications`;

  private unreadCount$ = new BehaviorSubject<number>(0);
  private notifications$ = new BehaviorSubject<Notification[]>([]);

  private pollSub: Subscription | null = null;

  readonly unreadCount = this.unreadCount$.asObservable();
  readonly notifications = this.notifications$.asObservable();

  constructor(
    private http: HttpClient,
    private auth: AuthService
  ) {}

  private emptyPaged(page = 1, pageSize = 50): PagedResult<Notification> {
    return { items: [], totalCount: 0, page, pageSize, totalPages: 0 };
  }

  private onUnauthorized(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 401) {
      this.stopPolling();
      this.notifications$.next([]);
      this.unreadCount$.next(0);
    }
  }

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Notification>> {
    if (!this.auth.getToken()) {
      return of(this.emptyPaged(page, pageSize));
    }
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Notification>>(this.base, { params }).pipe(
      catchError(err => {
        if (err instanceof HttpErrorResponse && err.status === 401) {
          this.onUnauthorized(err);
        }
        return of(this.emptyPaged(page, pageSize));
      })
    );
  }

  markAsRead(ids?: number[]): Observable<boolean> {
    if (!this.auth.getToken()) {
      return of(false);
    }
    return this.http.put<boolean>(`${this.base}/read`, ids ?? null).pipe(
      tap(() => this.refresh())
    );
  }

  markAllAsRead(): Observable<boolean> {
    return this.markAsRead();
  }

  /**
   * Fetch once and update subjects. No-op if not authenticated (avoids 401 noise).
   */
  refresh(): void {
    if (!this.auth.getToken()) {
      this.notifications$.next([]);
      this.unreadCount$.next(0);
      return;
    }
    this.getAll(1, 50).subscribe(res => {
      this.notifications$.next(res.items);
      this.unreadCount$.next(res.items.filter(n => !n.isRead).length);
    });
  }

  /**
   * Clear state and stop interval — call when session ends.
   */
  reset(): void {
    this.stopPolling();
    this.notifications$.next([]);
    this.unreadCount$.next(0);
  }

  startPolling(intervalMs = 60000): void {
    this.stopPolling();
    if (!this.auth.getToken()) {
      this.notifications$.next([]);
      this.unreadCount$.next(0);
      return;
    }

    this.refresh();

    this.pollSub = interval(intervalMs).pipe(
      switchMap(() => {
        if (!this.auth.getToken()) {
          return of(this.emptyPaged(1, 50));
        }
        return this.getAll(1, 50);
      })
    ).subscribe(res => {
      this.notifications$.next(res.items);
      this.unreadCount$.next(res.items.filter(n => !n.isRead).length);
    });
  }

  stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }
}
