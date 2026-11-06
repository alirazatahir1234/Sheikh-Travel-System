import { Injectable, Injector } from '@angular/core';
import {
  HttpRequest, HttpHandler, HttpEvent, HttpInterceptor, HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, finalize, map, shareReplay, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';
import { resolveTenantSlug } from '../utils/tenant-slug';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  /** Shared in-flight refresh so concurrent 401s wait on one call (and fail together). */
  private refreshInFlight$: Observable<string> | null = null;
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
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.auth.refreshToken().pipe(
        map(res => res.accessToken),
        catchError(err => {
          this.notifySessionEndedIfNeeded(originalError, err);
          // Refresh failed → session is over; clear auth and go to login.
          this.auth.logout();
          return throwError(() => err);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.refreshInFlight$.pipe(
      switchMap(accessToken =>
        next.handle(this.withBearerToken(req, accessToken)).pipe(
          catchError((retryError: HttpErrorResponse) => {
            // Post-refresh 401 means the session is still invalid — do not leave the user in-app.
            if (retryError.status === 401) {
              this.notifySessionEndedIfNeeded(originalError, retryError);
              this.auth.logout();
            }
            return throwError(() => retryError);
          })
        )
      )
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
