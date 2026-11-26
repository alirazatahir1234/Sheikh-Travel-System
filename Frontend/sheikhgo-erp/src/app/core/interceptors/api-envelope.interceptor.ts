import { Injectable } from '@angular/core';
import {
  HttpInterceptor, HttpRequest, HttpHandler, HttpEvent,
  HttpResponse, HttpErrorResponse
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

/**
 * Synthetic response header the interceptor sets to the envelope's `message` on success, so a caller that needs
 * what the server said (`observe: 'response'`) still gets it after the body has been unwrapped to `data`.
 * It never travels over the wire.
 */
export const API_MESSAGE_HEADER = 'X-Api-Message';

/** The server's envelope message of an unwrapped response, or null when the server sent none. */
export function apiMessageOf(response: HttpResponse<unknown>): string | null {
  const raw = response.headers.get(API_MESSAGE_HEADER);
  return raw ? decodeURIComponent(raw) : null;
}

/**
 * Auto-unwraps any JSON body that matches the backend's `ApiResponse<T>` envelope:
 *
 *   { "success": true,  "message": "...", "data": <T> }
 *
 * After this interceptor, every downstream subscriber just receives `T` directly —
 * no need to `.pipe(map(res => res.data))` in every service. The envelope's `message`
 * is kept on the response as `X-Api-Message` (see `apiMessageOf`) for callers that observe
 * the full response.
 *
 * When the envelope reports `success: false`, the response is converted into an
 * HttpErrorResponse carrying the backend message (as `error.message` and as the status
 * text), so components can rely on the regular `error:` callback and `apiErrorMessage()`.
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
            statusText: body.message?.trim() || event.statusText,
            url: event.url ?? undefined,
            error: body
          });
        }

        const message = typeof body.message === 'string' ? body.message.trim() : '';
        return event.clone({
          body: body.data ?? null,
          headers: message ? event.headers.set(API_MESSAGE_HEADER, encodeURIComponent(message)) : event.headers
        });
      })
    );
  }
}

interface ApiEnvelope {
  success: boolean;
  message?: string;
  data?: unknown;
}

/**
 * The backend envelope always carries `success`; `data` may be omitted when the serializer drops nulls
 * (e.g. `ApiResponse<bool>` answers that only carry a message), so either `data` or `message` identifies it.
 */
function isEnvelope(body: unknown): body is ApiEnvelope {
  return (
    !!body &&
    typeof body === 'object' &&
    !Array.isArray(body) &&
    'success' in body &&
    typeof (body as ApiEnvelope).success === 'boolean' &&
    ('data' in body || 'message' in body)
  );
}
