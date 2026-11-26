import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, HttpResponse, HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiEnvelopeInterceptor, apiMessageOf } from './api-envelope.interceptor';
import { apiErrorMessage } from '../utils/api-error.util';

describe('ApiEnvelopeInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: HTTP_INTERCEPTORS, useClass: ApiEnvelopeInterceptor, multi: true }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('unwraps a successful envelope to its data', () => {
    let body: unknown;
    http.get('/api/x').subscribe(b => body = b);
    httpMock.expectOne('/api/x').flush({ success: true, message: 'ok', data: { id: 7 } });
    expect(body).toEqual({ id: 7 });
  });

  it('keeps the server message reachable for callers that observe the response', () => {
    let response: HttpResponse<unknown> | undefined;
    http.post('/api/x', {}, { observe: 'response' }).subscribe(r => response = r);
    httpMock.expectOne('/api/x').flush({ success: true, message: 'Pilgrim cancelled. Removed from the group visa case.', data: true });
    expect(response!.body).toBeTrue();
    expect(apiMessageOf(response!)).toBe('Pilgrim cancelled. Removed from the group visa case.');
  });

  it('reports no message when the server sent none', () => {
    let response: HttpResponse<unknown> | undefined;
    http.get('/api/x', { observe: 'response' }).subscribe(r => response = r);
    httpMock.expectOne('/api/x').flush({ success: true, message: '', data: [1, 2] });
    expect(response!.body).toEqual([1, 2]);
    expect(apiMessageOf(response!)).toBeNull();
  });

  it('turns a 200 with success:false into an error that carries the server text', () => {
    let error: HttpErrorResponse | undefined;
    http.post('/api/x', {}).subscribe({ next: () => fail('should error'), error: e => error = e });
    httpMock.expectOne('/api/x').flush({ success: false, message: 'Only 150 PKR is payable to this beneficiary.', data: null });
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect(error!.status).toBe(200);
    expect(error!.statusText).toBe('Only 150 PKR is payable to this beneficiary.');
    expect(apiErrorMessage(error)).toBe('Only 150 PKR is payable to this beneficiary.');
  });

  it('recognises an envelope whose data was omitted by the serializer', () => {
    let error: HttpErrorResponse | undefined;
    http.post('/api/x', {}).subscribe({ next: () => fail('should error'), error: e => error = e });
    httpMock.expectOne('/api/x').flush({ success: false, message: 'The pilgrim is already cancelled.' });
    expect(apiErrorMessage(error)).toBe('The pilgrim is already cancelled.');
  });

  it('passes non-enveloped bodies through untouched', () => {
    let body: unknown;
    http.get('/api/x').subscribe(b => body = b);
    const raw = { success: 'yes', items: [1] };
    httpMock.expectOne('/api/x').flush(raw);
    expect(body).toEqual(raw);
  });
});
