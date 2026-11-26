import { TestBed } from '@angular/core/testing';
import { BehaviorSubject, Subject } from 'rxjs';
import { GpsScopeFilterService } from './gps-scope-filter.service';
import { TenantContextService } from '../../core/services/tenant-context.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { PagedResult } from '../../core/models/common.model';
import { VehicleListItem } from '../../core/models/vehicle.model';

describe('GpsScopeFilterService', () => {
  const STORAGE_KEY = 'stb-gps-scope-filters';
  let tenantId$: BehaviorSubject<number | null>;
  let isAllCompanies$: BehaviorSubject<boolean>;
  let responses: Subject<PagedResult<VehicleListItem>>[];
  let getAll: jasmine.Spy;

  const page = (ids: number[]) =>
    ({ items: ids.map(id => ({ id, name: `V${id}`, status: 'Active' })) } as unknown as PagedResult<VehicleListItem>);

  function create(): GpsScopeFilterService {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: TenantContextService,
          useValue: {
            canUseSwitcher: true,
            hasConcreteTenant: true,
            get currentTenantId() { return tenantId$.value; },
            tenantId$,
            isAllCompanies$
          }
        },
        { provide: VehicleService, useValue: { getAll } }
      ]
    });
    return TestBed.inject(GpsScopeFilterService);
  }

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    tenantId$ = new BehaviorSubject<number | null>(2);
    isAllCompanies$ = new BehaviorSubject(false);
    responses = [];
    getAll = jasmine.createSpy('getAll').and.callFake(() => {
      const s = new Subject<PagedResult<VehicleListItem>>();
      responses.push(s);
      return s;
    });
  });

  afterEach(() => localStorage.removeItem(STORAGE_KEY));

  it('loads vehicles exactly once on start', () => {
    create();

    expect(getAll).toHaveBeenCalledTimes(1);
  });

  it('keeps the restored vehicle filter instead of wiping it on start', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ vehicleFilterId: 50 }));

    const service = create();
    responses[0].next(page([50, 51]));

    expect(service.vehicleFilterId).toBe(50);
  });

  it('reloads once per company change and clears the vehicle filter', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ vehicleFilterId: 50 }));
    const service = create();
    responses[0].next(page([50]));

    tenantId$.next(3);

    expect(getAll).toHaveBeenCalledTimes(2);
    expect(service.vehicleFilterId).toBeNull();
  });

  it('ignores a repeat of the same company', () => {
    create();

    tenantId$.next(2);
    isAllCompanies$.next(false);

    expect(getAll).toHaveBeenCalledTimes(1);
  });

  it('drops a stale response when the company changes mid-request', () => {
    const service = create();

    tenantId$.next(3);
    responses[0].next(page([1, 2, 3])); // late answer for the previous company
    expect(service.vehicles).toEqual([]);

    responses[1].next(page([9]));
    expect(service.vehicles.map(v => v.id)).toEqual([9]);
  });
});
