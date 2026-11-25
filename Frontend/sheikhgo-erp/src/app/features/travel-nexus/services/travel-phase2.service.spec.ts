import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { TravelEnquiryService } from './travel-enquiry.service';
import { TravelVisaService } from './travel-visa.service';

describe('TravelNexus Phase 2 services', () => {
  let http: HttpTestingController;
  let enquiries: TravelEnquiryService;
  let visas: TravelVisaService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    enquiries = TestBed.inject(TravelEnquiryService);
    visas = TestBed.inject(TravelVisaService);
  });

  afterEach(() => http.verify());

  it('sends the enquiry date range and follow-up filter, omitting empty filters', () => {
    enquiries.getEnquiries({ page: 1, pageSize: 20, status: '', serviceType: 'Visa', createdFrom: '2026-11-01', createdTo: '2026-11-30', followUpDue: true }).subscribe();
    const req = http.expectOne(r => r.url === `${environment.apiUrl}/travel/enquiries`);
    expect(req.request.params.get('createdFrom')).toBe('2026-11-01');
    expect(req.request.params.get('createdTo')).toBe('2026-11-30');
    expect(req.request.params.get('followUpDue')).toBe('true');
    expect(req.request.params.get('serviceType')).toBe('Visa');
    expect(req.request.params.has('status')).toBeFalse();
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('posts enquiry status moves, loss and conversion to their own endpoints', () => {
    enquiries.changeStatus(5, 'MarkQuoted').subscribe();
    expect(http.expectOne(`${environment.apiUrl}/travel/enquiries/5/status`).request.body).toEqual({ action: 'MarkQuoted', note: null });

    enquiries.markLost(5, 2, null).subscribe();
    expect(http.expectOne(`${environment.apiUrl}/travel/enquiries/5/lost`).request.body).toEqual({ lostReasonId: 2, lostNotes: null });

    enquiries.convert(5, { existingCustomerId: null, newCustomerKind: 'Individual', createVisaCase: false, visaCountryCode: null, visaTypeId: null, intendedTravelDate: null }).subscribe();
    http.expectOne(`${environment.apiUrl}/travel/enquiries/5/convert`).flush({ customerId: 1, visaCaseId: null, visaCaseNumber: null });
  });

  it('routes visa transitions, submission, applicants and decisions correctly', () => {
    visas.transition(9, 'Hold', 'Waiting for bank letter').subscribe();
    expect(http.expectOne(`${environment.apiUrl}/travel/visa-cases/9/transition`).request.body).toEqual({ action: 'Hold', note: 'Waiting for bank letter' });

    visas.addApplicants(9, [1, 2]).subscribe();
    expect(http.expectOne(`${environment.apiUrl}/travel/visa-cases/9/applicants`).request.body).toEqual({ travelerIds: [1, 2] });

    visas.removeApplicant(9, 4).subscribe();
    expect(http.expectOne(`${environment.apiUrl}/travel/visa-cases/9/applicants/4`).request.method).toBe('DELETE');

    visas.recordDecision(9, 4, { decision: 'Rejected', visaNumber: null, visaIssueDate: null, visaValidFrom: null, visaExpiryDate: null, rejectionReason: 'Insufficient funds', notes: null }).subscribe();
    http.expectOne(`${environment.apiUrl}/travel/visa-cases/9/applicants/4/decision`).flush({ applied: true, newCaseStatus: 'Rejected' });
  });
});
