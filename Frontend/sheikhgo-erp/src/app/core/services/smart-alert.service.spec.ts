import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Subject } from 'rxjs';
import { SmartAlertService } from './smart-alert.service';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';

describe('SmartAlertService.refresh', () => {
  let service: SmartAlertService;
  let http: HttpTestingController;
  let hub: Subject<{ message?: string; ruleType?: string }>;
  const isAlerts = (url: string) => url.includes('/smart-alerts');
  const empty = { newCount: 0, activeCount: 0, items: [] };

  beforeEach(() => {
    hub = new Subject();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        { provide: AuthService, useValue: { getToken: () => 'token', isLoggedIn: () => true } },
        { provide: NotificationService, useValue: { smartAlertEvents: hub.asObservable() } },
        { provide: UiToastService, useValue: { info: () => undefined } }
      ]
    });
    service = TestBed.inject(SmartAlertService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends one request when refresh is called twice before the first answers', () => {
    service.refresh();
    service.refresh();

    const requests = http.match(r => isAlerts(r.url));
    expect(requests.length).toBe(1);
    requests[0].flush(empty);
    http.expectNone(r => isAlerts(r.url));
  });

  it('runs one more reload after a hub event that arrives mid-request', () => {
    service.refresh();
    hub.next({ message: 'Overspeed' });
    hub.next({ message: 'Overspeed again' });

    const first = http.match(r => isAlerts(r.url));
    expect(first.length).toBe(1);
    first[0].flush(empty);

    const followUp = http.match(r => isAlerts(r.url));
    expect(followUp.length).toBe(1);
    followUp[0].flush(empty);
  });

  it('refreshes again once the previous request has finished', () => {
    service.refresh();
    http.match(r => isAlerts(r.url))[0].flush(empty);

    service.refresh();

    const second = http.match(r => isAlerts(r.url));
    expect(second.length).toBe(1);
    second[0].flush(empty);
  });
});
