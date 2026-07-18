import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, interval, switchMap, tap, catchError, of, Subscription, Subject, map } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import type {
  Notification as AppNotification,
  NotificationDeliveryLog,
  NotificationFilter,
  NotificationPreferences,
  NotificationRetentionEstimate,
  NotificationRetentionPolicy,
  NotificationStats,
  NotificationTemplate
} from '../models/notification.model';
import { AuthService } from './auth.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';

@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private readonly base = `${environment.apiUrl}/notifications`;

  private unreadCount$ = new BehaviorSubject<number>(0);
  private notifications$ = new BehaviorSubject<AppNotification[]>([]);
  private readonly realtime$ = new Subject<AppNotification>();

  private pollSub: Subscription | null = null;
  private hub?: signalR.HubConnection;

  readonly unreadCount = this.unreadCount$.asObservable();
  readonly notifications = this.notifications$.asObservable();
  readonly realtimeNotifications = this.realtime$.asObservable();

  constructor(
    private http: HttpClient,
    private auth: AuthService,
    private toast: UiToastService
  ) {}

  private emptyPaged(page = 1, pageSize = 50): PagedResult<AppNotification> {
    return { items: [], totalCount: 0, page, pageSize, totalPages: 0 };
  }

  private onUnauthorized(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 401) {
      this.stopPolling();
      void this.disconnectHub();
      this.notifications$.next([]);
      this.unreadCount$.next(0);
    }
  }

  getAll(filter: NotificationFilter = {}): Observable<PagedResult<AppNotification>> {
    if (!this.auth.getToken()) {
      return of(this.emptyPaged(filter.page ?? 1, filter.pageSize ?? 20));
    }

    let params = new HttpParams()
      .set('page', filter.page ?? 1)
      .set('pageSize', filter.pageSize ?? 20);

    if (filter.unreadOnly != null) params = params.set('unreadOnly', filter.unreadOnly);
    if (filter.isSent != null) params = params.set('isSent', filter.isSent);
    if (filter.channel) params = params.set('channel', filter.channel);
    if (filter.priority != null) params = params.set('priority', filter.priority);
    if (filter.search) params = params.set('search', filter.search);
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);
    if (filter.module) params = params.set('module', filter.module);
    if (filter.archived) params = params.set('archived', true);
    if (filter.trash) params = params.set('trash', true);
    if (filter.datePreset) params = params.set('datePreset', filter.datePreset);

    return this.http.get<PagedResult<AppNotification>>(this.base, { params }).pipe(
      catchError(err => {
        if (err instanceof HttpErrorResponse && err.status === 401) this.onUnauthorized(err);
        return of(this.emptyPaged(filter.page ?? 1, filter.pageSize ?? 20));
      })
    );
  }

  getUnreadCount(): Observable<number> {
    if (!this.auth.getToken()) return of(0);
    return this.http.get<number>(`${this.base}/unread-count`).pipe(
      catchError(err => {
        if (err instanceof HttpErrorResponse && err.status === 401) this.onUnauthorized(err);
        return of(0);
      })
    );
  }

  getStats(): Observable<NotificationStats> {
    return this.http.get<NotificationStats>(`${this.base}/stats`).pipe(
      catchError(() => of({
        unread: 0, total: 0, email: 0, sms: 0, push: 0, browser: 0, whatsApp: 0, failed: 0
      }))
    );
  }

  /** Lightweight user list for compose (no UsersView permission required). */
  getRecipients(search?: string): Observable<{ id: number; fullName: string; email: string }[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<{ id: number; fullName: string; email: string }[] | { data?: unknown }>(
      `${this.base}/recipients`,
      { params }
    ).pipe(
      // Envelope interceptor usually unwraps; keep a safe fallback.
      map(res => {
        if (Array.isArray(res)) return res;
        const data = (res as { data?: unknown })?.data;
        return Array.isArray(data) ? data as { id: number; fullName: string; email: string }[] : [];
      })
    );
  }

  getPreferences(): Observable<NotificationPreferences> {
    return this.http.get<NotificationPreferences>(`${this.base}/preferences`).pipe(
      catchError(() => of({
        emailEnabled: true,
        smsEnabled: true,
        pushEnabled: true,
        browserEnabled: true,
        whatsAppEnabled: false
      }))
    );
  }

  savePreferences(prefs: NotificationPreferences): Observable<NotificationPreferences> {
    return this.http.put<NotificationPreferences>(`${this.base}/preferences`, prefs);
  }

  getTemplates(channel?: string): Observable<NotificationTemplate[]> {
    let params = new HttpParams();
    if (channel) params = params.set('channel', channel);
    return this.http.get<NotificationTemplate[]>(`${this.base}/templates`, { params }).pipe(
      catchError(() => of([]))
    );
  }

  upsertTemplate(payload: Partial<NotificationTemplate> & {
    templateKey: string;
    templateName: string;
    subject: string;
    body: string;
    channel: string;
  }, id?: number): Observable<number> {
    if (id != null) {
      return this.http.put<number>(`${this.base}/templates/${id}`, payload);
    }
    return this.http.post<number>(`${this.base}/templates`, payload);
  }

  getHistory(id: number): Observable<NotificationDeliveryLog[]> {
    return this.http.get<NotificationDeliveryLog[]>(`${this.base}/${id}/history`).pipe(
      catchError(() => of([]))
    );
  }

  create(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<number>(this.base, payload);
  }

  bulk(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<number>(`${this.base}/bulk`, payload);
  }

  /** Admin compose — Email / Push / SMS / Browser; stored in Notification Center. */
  sendManualMessage(payload: {
    subject: string;
    body: string;
    priority?: number;
    recipientUserIds?: number[];
    emailAddresses?: string[];
    role?: string | null;
    channels?: string[];
    templateKey?: string | null;
    sendNow?: boolean;
  }): Observable<number> {
    return this.http.post<number>(`${this.base}/send-email`, payload).pipe(
      tap(() => this.refresh())
    );
  }

  send(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/send`, {});
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`).pipe(
      tap(() => this.refresh())
    );
  }

  archive(ids: number[]): Observable<number> {
    return this.http.post<number>(`${this.base}/archive`, { ids }).pipe(tap(() => this.refresh()));
  }

  restore(ids: number[]): Observable<number> {
    return this.http.post<number>(`${this.base}/restore`, { ids }).pipe(tap(() => this.refresh()));
  }

  bulkDelete(ids: number[]): Observable<number> {
    return this.http.post<number>(`${this.base}/bulk-delete`, { ids }).pipe(tap(() => this.refresh()));
  }

  /** Soft-delete all inbox/archived, or permanently empty trash. scope: inbox | archived | trash */
  deleteAll(scope: 'inbox' | 'archived' | 'trash' = 'inbox'): Observable<number> {
    return this.http.post<number>(`${this.base}/delete-all`, null, {
      params: new HttpParams().set('scope', scope)
    }).pipe(tap(() => this.refresh()));
  }

  getRetention(): Observable<NotificationRetentionPolicy> {
    return this.http.get<NotificationRetentionPolicy>(`${this.base}/retention`);
  }

  saveRetention(policy: NotificationRetentionPolicy): Observable<NotificationRetentionPolicy> {
    return this.http.put<NotificationRetentionPolicy>(`${this.base}/retention`, policy);
  }

  getRetentionEstimate(): Observable<NotificationRetentionEstimate> {
    return this.http.get<NotificationRetentionEstimate>(`${this.base}/retention/estimate`);
  }

  runRetentionCleanup(): Observable<NotificationRetentionEstimate> {
    return this.http.post<NotificationRetentionEstimate>(`${this.base}/retention/run`, {});
  }

  markAsRead(ids?: number[]): Observable<boolean> {
    if (!this.auth.getToken()) return of(false);
    return this.http.put<boolean>(`${this.base}/read`, ids ?? null).pipe(
      tap(() => this.refresh())
    );
  }

  markAllAsRead(): Observable<boolean> {
    return this.markAsRead();
  }

  refresh(): void {
    if (!this.auth.getToken()) {
      this.notifications$.next([]);
      this.unreadCount$.next(0);
      return;
    }
    this.getAll({ page: 1, pageSize: 50 }).subscribe(res => {
      this.notifications$.next(res.items);
    });
    this.refreshUnreadCount();
  }

  refreshUnreadCount(): void {
    this.getUnreadCount().subscribe(count => this.unreadCount$.next(count));
  }

  reset(): void {
    this.stopPolling();
    void this.disconnectHub();
    this.notifications$.next([]);
    this.unreadCount$.next(0);
  }

  startPolling(intervalMs = 120000): void {
    this.stopPolling();
    if (!this.auth.getToken()) {
      this.notifications$.next([]);
      this.unreadCount$.next(0);
      return;
    }

    this.refresh();
    void this.connectHub();

    this.pollSub = interval(intervalMs).pipe(
      switchMap(() => {
        if (!this.auth.getToken()) return of(this.emptyPaged(1, 50));
        return this.getAll({ page: 1, pageSize: 50 });
      })
    ).subscribe(res => {
      this.notifications$.next(res.items);
      this.refreshUnreadCount();
    });
  }

  stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }

  async requestBrowserPermission(): Promise<NotificationPermission | 'unsupported'> {
    if (!('Notification' in window)) return 'unsupported';
    if (Notification.permission === 'granted') return 'granted';
    if (Notification.permission === 'denied') return 'denied';
    return Notification.requestPermission();
  }

  private async connectHub(): Promise<void> {
    if (this.hub?.state === signalR.HubConnectionState.Connected) return;

    const hubUrl = environment.apiUrl.replace('/api', '/hubs/notifications');
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.getToken() ?? '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.hub.on('ReceiveNotification', (payload: AppNotification & { kind?: string }) => {
      if (payload?.kind === 'browser') {
        void this.showBrowserToast(payload.title, payload.message);
        this.toast.info(payload.message || '', payload.title || 'Notification');
        return;
      }

      if (!payload?.id) {
        this.refresh();
        return;
      }

      const current = this.notifications$.value;
      if (!current.some(n => n.id === payload.id)) {
        this.notifications$.next([payload, ...current].slice(0, 50));
      }
      this.refreshUnreadCount();
      this.realtime$.next(payload);
      this.toast.info(payload.message || '', payload.title || 'Notification');
      void this.showBrowserToast(payload.title, payload.message);
    });

    try {
      await this.hub.start();
    } catch {
      // REST polling remains the fallback
    }
  }

  private async disconnectHub(): Promise<void> {
    if (!this.hub) return;
    try { await this.hub.stop(); } catch { /* ignore */ }
    this.hub = undefined;
  }

  private async showBrowserToast(title?: string, body?: string): Promise<void> {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    try {
      new Notification(title || 'SheikhGo', {
        body: body || '',
        icon: '/favicon.ico'
      });
    } catch { /* ignore */ }
  }

  ngOnDestroy(): void {
    this.reset();
    this.realtime$.complete();
  }
}
