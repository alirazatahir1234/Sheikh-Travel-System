import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import {
  GeofenceWorkspaceComponent,
  groupFences,
  isUnassigned,
  kpiCards,
  matchesFilter,
  matchesSearch
} from './geofence-workspace.component';
import { GeofenceWorkspaceService } from '../geofence-workspace.service';
import { GeofenceGroup, GeofenceListItem, GeofenceSummary } from '../geofence.models';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { DriverService } from '../../../../core/services/driver.service';
import { AuthService } from '../../../../core/services/auth.service';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

function fence(overrides: Partial<GeofenceListItem>): GeofenceListItem {
  return {
    id: 1, name: 'Zone', areaType: 'circle', centerLat: 31.5, centerLng: 74.3, radiusMeters: 200, geoJson: null, isActive: true,
    color: '#0f766e', type: 'Office', isRestricted: false, groupId: null, groupName: null, address: null, description: null,
    bufferMeters: 25, corridorWidthMeters: null, areaSquareMeters: 125_000, perimeterMeters: 1_256, appliesToAll: false,
    vehiclesCovered: 3, driversAssigned: 0, insideNow: 0, entriesToday: 0, violationsToday: 0, ruleCount: 1,
    alertOnEnter: true, alertOnExit: false,
    ...overrides
  };
}

const FENCES: GeofenceListItem[] = [
  fence({ id: 1, name: 'Pasrur Head Office', groupId: 10, groupName: 'Depots and offices', insideNow: 3, address: 'Sialkot Rd' }),
  fence({ id: 2, name: 'Gujranwala Depot', groupId: 10, groupName: 'Depots and offices', type: 'Depot' }),
  fence({ id: 3, name: 'Border belt', type: 'Restricted', isRestricted: true, appliesToAll: true, violationsToday: 1, groupId: 20, groupName: 'Restricted' }),
  fence({ id: 4, name: 'Old yard', isActive: false, vehiclesCovered: 0 }),
  fence({ id: 5, name: 'Awan Villas', type: 'Customer', vehiclesCovered: 0, driversAssigned: 2 })
];

const GROUPS: GeofenceGroup[] = [
  { id: 20, name: 'Restricted', sortOrder: 2, geofenceCount: 1 },
  { id: 10, name: 'Depots and offices', sortOrder: 1, geofenceCount: 2 }
];

const SUMMARY: GeofenceSummary = {
  total: 24, active: 23, vehiclesInsideNow: 4, vehiclesAssigned: 18, entriesToday: 18, exitsToday: 15, violationsToday: 2,
  restrictedEntriesToday: 1, overstaysToday: 1, overspeedsToday: 0, idleAlertsToday: 0, afterHoursExitsToday: 0,
  notArrivedToday: 0, avgMinutesOnSiteLast7Days: 42
};

describe('geofences main screen helpers', () => {
  it('builds the six KPI cards from the summary, with their windows', () => {
    const cards = kpiCards(SUMMARY);
    expect(cards.map(c => `${c.label}=${c.value} (${c.note})`)).toEqual([
      'Geofences=24 (23 active)',
      'Vehicles inside now=4 (of 18 assigned)',
      'Entries today=18 ()',
      'Exits today=15 ()',
      'Violations today=2 (1 restricted · 1 overstay)',
      'Avg time on site=42m (last 7 days)'
    ]);
    expect(cards[4].tone).toBe('red');
    expect(kpiCards({ ...SUMMARY, violationsToday: 0, restrictedEntriesToday: 0, overstaysToday: 0, avgMinutesOnSiteLast7Days: null })
      .slice(4).map(c => `${c.value}|${c.note}|${c.tone}`)).toEqual(['0|none|default', '—|no visits ended in 7 days|default']);
    expect(kpiCards(null)).toEqual([]);
  });

  it('filters by the chips and searches name, type, address and group', () => {
    expect(FENCES.filter(f => matchesFilter(f, 'inside')).map(f => f.id)).toEqual([1]);
    expect(FENCES.filter(f => matchesFilter(f, 'restricted')).map(f => f.id)).toEqual([3]);
    expect(FENCES.filter(f => matchesFilter(f, 'unassigned')).map(f => f.id)).toEqual([4]);
    expect(FENCES.filter(f => matchesFilter(f, 'inactive')).map(f => f.id)).toEqual([4]);
    expect(FENCES.filter(f => matchesFilter(f, 'all')).length).toBe(5);
    expect(isUnassigned(FENCES[4])).toBeFalse(); // drivers count as assigned
    expect(FENCES.filter(f => matchesSearch(f, 'sialkot')).map(f => f.id)).toEqual([1]);
    expect(FENCES.filter(f => matchesSearch(f, 'DEPOTS')).map(f => f.id)).toEqual([1, 2]);
  });

  it('groups the list in the groups’ order, with ungrouped geofences last', () => {
    const views = groupFences(FENCES, GROUPS);
    expect(views.map(v => `${v.name}:${v.items.map(i => i.id).join(',')}`)).toEqual([
      'Depots and offices:2,1',
      'Restricted:3',
      'No group:5,4'
    ]);
  });
});

describe('GeofenceWorkspaceComponent', () => {
  let fixture: ComponentFixture<GeofenceWorkspaceComponent>;
  let component: GeofenceWorkspaceComponent;
  let api: jasmine.SpyObj<GeofenceWorkspaceService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<GeofenceWorkspaceService>('GeofenceWorkspaceService', [
      'list', 'summary', 'groups', 'detail', 'inside', 'activity', 'bulk', 'recipientOptions', 'create', 'createGroup', 'import'
    ]);
    api.list.and.returnValue(of(FENCES));
    api.summary.and.returnValue(of(SUMMARY));
    api.groups.and.returnValue(of(GROUPS));
    api.recipientOptions.and.returnValue(of({ roles: [], users: [] }));
    api.bulk.and.returnValue(of({ affected: 2 }));
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    dialog.open.and.returnValue({ afterClosed: () => of(true) } as ReturnType<MatDialog['open']>);

    await TestBed.configureTestingModule({
      imports: [GeofenceWorkspaceComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: GeofenceWorkspaceService, useValue: api },
        { provide: GpsTrackingService, useValue: { getAllVehicleLocations: () => of([]) } },
        { provide: VehicleService, useValue: { getAll: () => of({ items: [], totalCount: 0, page: 1, pageSize: 500, totalPages: 0 }) } },
        { provide: PlatformService, useValue: { getBranches: () => of([]), getDepartments: () => of([]) } },
        { provide: DriverService, useValue: { getAll: () => of({ items: [], totalCount: 0, page: 1, pageSize: 500, totalPages: 0 }) } },
        { provide: AuthService, useValue: { hasPermission: (p: string) => p === 'Gps.GeofenceManage', getCurrentUser: () => ({ tenantId: 1, userId: 1 }) } },
        { provide: GoogleMapsLoaderService, useValue: { isConfigured: false, authFailures$: of() } },
        { provide: UiToastService, useValue: jasmine.createSpyObj('UiToastService', ['success', 'error', 'info', 'warning']) },
        { provide: MatDialog, useValue: dialog }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GeofenceWorkspaceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('shows the KPI cards from GET /geofences/summary', () => {
    const values = Array.from(fixture.nativeElement.querySelectorAll('.kpi__value')).map(e => (e as HTMLElement).textContent!.trim());
    expect(values).toEqual(['24', '4', '18', '15', '2', '42m']);
  });

  it('lists the geofences grouped, with inside and violation badges, and counts each chip', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Depots and offices');
    expect(text).toContain('3 inside');
    expect(text).toContain('1 violation');
    const chips = Array.from(fixture.nativeElement.querySelectorAll('.gw-chips .chip')).map(e => (e as HTMLElement).textContent!.trim());
    expect(chips).toEqual(['All 5', 'Inside now 1', 'Restricted 1', 'Unassigned 1', 'Inactive 1']);
  });

  it('sends bulk actions for the ticked geofences and asks before deleting', () => {
    component.toggleTick(1, true);
    component.toggleTick(2, true);
    component.runBulk('disable', component.tickedIds);
    expect(api.bulk).toHaveBeenCalledWith({ ids: [1, 2], action: 'disable', groupId: null, vehicleIds: null });

    component.moveToGroup([1, 2], 20);
    expect(api.bulk).toHaveBeenCalledWith({ ids: [1, 2], action: 'group', groupId: 20, vehicleIds: null });

    component.runBulk('delete', [1, 2]);
    expect(dialog.open).toHaveBeenCalled();
    expect(api.bulk).toHaveBeenCalledWith({ ids: [1, 2], action: 'delete', groupId: null, vehicleIds: null });
  });

  it('adds picked vehicles to geofences with the assign action', () => {
    component.openAssign([3]);
    component.toggleAssign(11, true);
    component.toggleAssign(12, true);
    component.confirmAssign();
    expect(api.bulk).toHaveBeenCalledWith({ ids: [3], action: 'assign', groupId: null, vehicleIds: [11, 12] });
  });

  it('says why there is no map when Google Maps is not set up', () => {
    expect(component.mapError).toContain('Google Maps is not set up');
  });
});
