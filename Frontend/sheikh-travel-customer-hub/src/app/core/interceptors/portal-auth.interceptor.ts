import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CustomerSessionService } from '../services/customer-session.service';

/** Attaches portal JWT to customer-portal API calls (except anonymous catalog/booking create). */
export const portalAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(CustomerSessionService);
  const router = inject(Router);
  const token = session.accessToken();
  if (!token || !req.url.includes('/customer-portal/')) {
    return next(req);
  }

  const isPublic =
    req.url.includes('/customer-portal/routes') ||
    req.url.includes('/customer-portal/vehicles') ||
    req.url.includes('/customer-portal/price-estimate') ||
    req.url.includes('/customer-portal/quote') ||
    req.url.includes('/customer-portal/promo/validate') ||
    req.url.includes('/customer-portal/auth/') ||
    req.url.includes('/customer-portal/payment-gateway');

  if (isPublic) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    })
  ).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
        session.clear();
        void router.navigate(['/profile']);
      }
      return throwError(() => err);
    })
  );
};
