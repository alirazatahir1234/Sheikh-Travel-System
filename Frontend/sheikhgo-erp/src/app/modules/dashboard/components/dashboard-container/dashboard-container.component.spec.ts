import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { DashboardContainerComponent } from './dashboard-container.component';
import { DefaultDashboardComponent } from '../../dashboards/default-dashboard/default-dashboard.component';
import { DashboardService as CoreDashboardService } from '../../../../core/services/dashboard.service';
import { BookingService } from '../../../../core/services/booking.service';
import { InvoiceService } from '../../../../core/services/invoice.service';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { SmartAlertService } from '../../../../core/services/smart-alert.service';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';

/**
 * Regression: the dashboard sometimes stayed on its loading skeleton after all its data
 * had arrived. The container was OnPush, so an HTTP callback setting `loading = false` on
 * the hosted (default-CD) dashboard never got rendered until an unrelated event fired.
 */
describe('DashboardContainerComponent', () => {
  beforeEach(() => {
    // Keep the real DefaultDashboardComponent class (it's what the registry hosts) but
    // stub its template and skip ngOnInit's HTTP work.
    TestBed.overrideComponent(DefaultDashboardComponent, {
      set: { imports: [], template: `<span class="state">{{ loading ? 'loading' : 'ready' }}</span>` }
    });
    spyOn(DefaultDashboardComponent.prototype, 'ngOnInit');

    TestBed.configureTestingModule({
      imports: [DashboardContainerComponent],
      providers: [
        { provide: CoreDashboardService, useValue: {} },
        { provide: BookingService, useValue: {} },
        { provide: InvoiceService, useValue: {} },
        { provide: AuthService, useValue: {} },
        { provide: NotificationService, useValue: { unreadCount: of(0) } },
        { provide: SmartAlertService, useValue: {} },
        { provide: GpsTrackingService, useValue: {} },
        { provide: Router, useValue: {} }
      ]
    });
  });

  it('renders the hosted dashboard when its data arrives outside any template event', () => {
    const fixture = TestBed.createComponent(DashboardContainerComponent);
    fixture.detectChanges();

    const host = fixture.debugElement.query(el => el.componentInstance instanceof DefaultDashboardComponent);
    const dashboard = host.componentInstance as DefaultDashboardComponent;
    dashboard.loading = true;
    fixture.detectChanges();
    expect(text()).toBe('loading');

    // What the forkJoin subscriber does when the five API calls return.
    dashboard.loading = false;
    fixture.detectChanges(); // an application tick, as zone.js runs after the XHR callback

    expect(text()).toBe('ready');

    function text(): string {
      return (fixture.nativeElement as HTMLElement).querySelector('.state')?.textContent?.trim() ?? '';
    }
  });
});
