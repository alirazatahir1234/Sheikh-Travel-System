import { Injectable } from '@angular/core';
import {
  HttpInterceptor, HttpRequest, HttpHandler, HttpEvent,
  HttpResponse, HttpErrorResponse
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

/**
 * Auto-unwraps any JSON body that matches the backend's `ApiResponse<T>` envelope:
 *
 *   { "success": true,  "message": "...", "data": <T> }
 *
 * After this interceptor, every downstream subscriber just receives `T` directly —
 * no need to `.pipe(map(res => res.data))` in every service.
 *
 * When the envelope reports `success: false`, the response is converted into an
 * HttpErrorResponse carrying the backend message, so components can rely on the
 * regular `error:` callback.
 *
 * Non-enveloped responses (primitives, arrays, raw objects) are passed through.
 */
@Injectable()
export class ApiEnvelopeInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      map(event => {
        if (!(event instanceof HttpResponse)) return event;

        const body = event.body as ApiEnvelope | unknown;
        if (!isEnvelope(body)) return event;

        if (body.success === false) {
          throw new HttpErrorResponse({
            status: event.status,
            statusText: event.statusText,
            url: event.url ?? undefined,
            error: body
          });
        }

        return event.clone({ body: body.data });
      })
    );
  }
}

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
