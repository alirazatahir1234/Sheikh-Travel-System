import { TestBed } from '@angular/core/testing';
import { TenantDatePipe } from './tenant-date.pipe';
import { TenantTimeService } from '../../core/services/tenant-time.service';
import { DEFAULT_TIME_ZONE } from '../../core/constants/time-zone.constants';

describe('TenantDatePipe', () => {
  let pipe: TenantDatePipe;
  let tenantTime: TenantTimeService;
  const tripUtc = '2026-11-23T21:15:00Z';

  beforeEach(() => {
    TestBed.configureTestingModule({});
    tenantTime = TestBed.inject(TenantTimeService);
    pipe = TestBed.runInInjectionContext(() => new TenantDatePipe());
  });

  afterEach(() => tenantTime.setCompanyTimeZone(DEFAULT_TIME_ZONE));

  it('renders in the company zone (Pakistan by default)', () => {
    expect(pipe.transform(tripUtc, 'HH:mm')).toBe('02:15');
    expect(pipe.transform(tripUtc, 'dd MMM yyyy')).toBe('24 Nov 2026');
  });

  it('follows a UAE company zone once branding loads', () => {
    tenantTime.setCompanyTimeZone('Asia/Dubai');
    expect(pipe.transform(tripUtc, 'HH:mm')).toBe('01:15');
  });

  it('prefers an explicit vehicle/branch zone', () => {
    expect(pipe.transform(tripUtc, 'HH:mm', 'Asia/Dubai')).toBe('01:15');
    expect(pipe.transform(tripUtc, 'HH:mm', 'Not/AZone')).toBe('02:15');
  });

  it('keeps calendar dates unshifted and returns null for empty input', () => {
    expect(pipe.transform('2026-11-24', 'dd MMM yyyy')).toBe('24 Nov 2026');
    expect(pipe.transform(null, 'medium')).toBeNull();
    expect(pipe.transform('', 'medium')).toBeNull();
  });
});
