import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { TravelCustomerService } from './travel-customer.service';
import { isMasked } from '../models/travel.models';

describe('TravelCustomerService', () => {
  let service: TravelCustomerService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TravelCustomerService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TravelCustomerService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging and filters, omits empty filters, and normalizes PascalCase paged results', () => {
    let total = -1;
    let pages = -1;
    service.getCustomers({ page: 2, pageSize: 20, search: '  khan ', customerKind: '' }).subscribe(r => {
      total = r.totalCount;
      pages = r.totalPages;
    });

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/travel/customers`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('search')).toBe('khan');
    expect(req.request.params.has('customerKind')).toBeFalse();
    req.flush({ Items: [], TotalCount: 41, Page: 2, PageSize: 20 });

    expect(total).toBe(41);
    expect(pages).toBe(3);
  });

  it('creates travelers under the customer and updates them by traveler id', () => {
    service.createTraveler(7, {} as never).subscribe();
    http.expectOne(`${environment.apiUrl}/travel/customers/7/travelers`).flush(1);

    service.updateTraveler(12, {} as never).subscribe();
    const put = http.expectOne(`${environment.apiUrl}/travel/travelers/12`);
    expect(put.request.method).toBe('PUT');
    put.flush(true);
  });

  it('recognises masked identity values from the API', () => {
    expect(isMasked('••••••567')).toBeTrue();
    expect(isMasked('AB1234567')).toBeFalse();
    expect(isMasked(null)).toBeFalse();
  });
});
