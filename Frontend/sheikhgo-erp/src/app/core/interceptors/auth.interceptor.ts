import { Injectable, Injector } from '@angular/core';
import {
  HttpRequest, HttpHandler, HttpEvent, HttpInterceptor, HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError, BehaviorSubject } from 'rxjs';
import { catchError, switchMap, filter, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';
import { resolveTenantSlug } from '../utils/tenant-slug';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private isRefreshing = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);
  private sessionEndedToastShown = false;

  constructor(
    private auth: AuthService,
    private injector: Injector) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.auth.getToken();
    const headers: Record<string, string> = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    if (req.url.includes('/api')) {
      // Backend prefers JWT tenant_id; slug is for branding / unauthenticated fallback.
      headers['X-Tenant-Slug'] = resolveTenantSlug(environment.tenantSlug);
    }
    const authReq = Object.keys(headers).length
      ? req.clone({ setHeaders: headers })
      : req;

    return next.handle(authReq).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 && !req.url.includes('/auth/')) {
          return this.handle401(authReq, next, error);
        }
        return throwError(() => error);
      })
    );
  }

  private handle401(
    req: HttpRequest<unknown>,
    next: HttpHandler,
    originalError: HttpErrorResponse
  ): Observable<HttpEvent<unknown>> {
    if (!this.isRefreshing) {
      this.isRefreshing = true;
      this.refreshTokenSubject.next(null);

      return this.auth.refreshToken().pipe(
        switchMap(res => {
          this.isRefreshing = false;
          this.refreshTokenSubject.next(res.accessToken);
          return next.handle(this.withBearerToken(req, res.accessToken));
        }),
        catchError(err => {
          this.isRefreshing = false;
          this.notifySessionEndedIfNeeded(originalError, err);
          this.auth.logout();
          return throwError(() => err);
        })
      );
    }

    return this.refreshTokenSubject.pipe(
      filter(token => token !== null),
      take(1),
      switchMap(token => next.handle(this.withBearerToken(req, token as string)))
    );
  }

  private notifySessionEndedIfNeeded(
    accessError: HttpErrorResponse,
    refreshError: unknown
  ): void {
    if (this.sessionEndedToastShown) return;
    const superseded =
      this.isSessionSuperseded(accessError)
      || this.isSessionSuperseded(refreshError);
    if (!superseded) return;

    this.sessionEndedToastShown = true;
    try {
      const toast = this.injector.get(UiToastService);
      // Legacy kick-oldest header (no longer issued on new logins).
      toast.warning('Your session is no longer valid. Please log in again.');
    } catch {
      /* toast optional */
    }
  }

  private isSessionSuperseded(err: unknown): boolean {
    if (err instanceof HttpErrorResponse) {
      const header = err.headers?.get('X-Session-Ended')
        || err.headers?.get('x-session-ended');
      if (header === 'superseded') return true;
      const body = err.error;
      if (body && typeof body === 'object') {
        const msg = String((body as Record<string, unknown>)['message'] ?? '');
        if (msg.toLowerCase().includes('another login')) return true;
      }
    }
    return false;
  }

  private withBearerToken(req: HttpRequest<unknown>, accessToken: string): HttpRequest<unknown> {
    return req.clone({
      setHeaders: {
        Authorization: `Bearer ${accessToken}`
      }
    });
  }
}
