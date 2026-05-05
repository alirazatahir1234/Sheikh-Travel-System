import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { switchMap } from 'rxjs/operators';

interface ApiEnvelope {
  success: boolean;
  message?: string;
  data?: unknown;
}

function isEnvelope(body: unknown): body is ApiEnvelope {
  return (
    !!body &&
    typeof body === 'object' &&
    !Array.isArray(body) &&
    'success' in body &&
    'data' in body &&
    typeof (body as ApiEnvelope).success === 'boolean'
  );
}

/** Unwraps `ApiResponse<T>` from the Sheikh Travel API (same contract as the admin app). */
export const apiEnvelopeInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    switchMap((event) => {
      if (!(event instanceof HttpResponse)) return of(event);
      const body = event.body as unknown;
      if (!isEnvelope(body)) return of(event);
      if (body.success === false) {
        return throwError(
          () =>
            new HttpErrorResponse({
              status: event.status,
              statusText: event.statusText,
              url: event.url ?? undefined,
              error: body
            })
        );
      }
      return of(event.clone({ body: body.data }));
    })
  );
};
