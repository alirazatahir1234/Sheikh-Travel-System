import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, interval, switchMap, tap, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import { Notification } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly base = `${environment.apiUrl}/notifications`;

  private unreadCount$ = new BehaviorSubject<number>(0);
  private notifications$ = new BehaviorSubject<Notification[]>([]);

  readonly unreadCount = this.unreadCount$.asObservable();
  readonly notifications = this.notifications$.asObservable();

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Notification>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Notification>>(this.base, { params });
  }

  markAsRead(ids?: number[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/read`, ids ?? null).pipe(
      tap(() => this.refresh())
    );
  }

  markAllAsRead(): Observable<boolean> {
    return this.markAsRead();
  }

  refresh(): void {
    this.getAll(1, 50).pipe(
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 50 }))
    ).subscribe(res => {
      this.notifications$.next(res.items);
      this.unreadCount$.next(res.items.filter(n => !n.isRead).length);
    });
  }

  startPolling(intervalMs = 60000): void {
    this.refresh();
    interval(intervalMs).pipe(
      switchMap(() => this.getAll(1, 50)),
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 50 }))
    ).subscribe(res => {
      this.notifications$.next(res.items);
      this.unreadCount$.next(res.items.filter(n => !n.isRead).length);
    });
  }
}
