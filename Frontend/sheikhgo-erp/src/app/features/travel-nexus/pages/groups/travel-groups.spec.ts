import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import * as XLSX from 'xlsx';
import { environment } from '../../../../../environments/environment';
import {
  GROUP_STATUS_LABEL, GROUP_STATUS_VARIANT, GROUP_TRANSITION_LABEL, LEG_TYPE_LABEL, MEMBER_STATUS_VARIANT, PACKAGE_STATUS_VARIANT, ROOM_CAPACITY,
  TRAVEL_GROUP_STATUSES, TRAVEL_ROOM_TYPES, TRAVEL_TRANSPORT_LEG_TYPES, TravelGroupManifest, TravelGroupMember
} from '../../models/travel.models';
import { TravelGroupService } from '../../services/travel-group.service';
import { TravelPackageService } from '../../services/travel-package.service';
import { roomIssues } from './group-rooms-tab.component';
import { defaultPriceCategory } from './group-pilgrims-tab.component';
import { MANIFEST_PILGRIM_HEADERS, buildManifestPdf, buildManifestWorkbook, manifestFileName, manifestPilgrimRows } from './manifest-export';

const member = (id: number, gender: 'Male' | 'Female', customerId: number, travelerId = id * 10, mahramTravelerId: number | null = null): TravelGroupMember => ({
  id, travelerId, travelerName: `P${id}`, gender, dateOfBirth: null, customerId, customerName: `C${customerId}`, mahramTravelerId,
  passportNumber: null, passportExpiryDate: null, roomPreference: 'Quad', priceCategory: 'Adult', quotedPrice: null, currencyCode: null,
  status: 'Confirmed', cancelReason: null, visaDecision: null, visaNumber: null, visaExpiryDate: null
});

const manifest: TravelGroupManifest = {
  groupNumber: 'GRP-2026-00001', groupName: 'Ramadan A', packageName: '14N Umrah', departureDate: '2026-03-01T00:00:00',
  returnDate: '2026-03-15T00:00:00', leaderName: 'Imran', leaderPhone: null, visaCaseNumber: 'VC-2026-00009', piiMasked: true,
  generatedAtUtc: '2026-02-20T09:30:00', transport: [],
  stays: [{ id: 1, hotelId: 1, hotelName: 'Hilton Makkah', city: 'Makkah', checkIn: '2026-03-01T00:00:00', checkOut: '2026-03-11T00:00:00', sequence: 1, confirmationNumber: 'H-77', roomCount: 1, assignedCount: 2 }],
  pilgrims: [{ no: 1, memberId: 5, surname: 'KHAN', givenNames: 'AISHA', gender: 'Female', dateOfBirth: '1990-04-02T00:00:00', nationalityCode: 'PK',
    passportNumber: '******4567', passportExpiryDate: '2030-01-01T00:00:00', visaNumber: null, visaExpiryDate: null, mahramName: 'Ali Khan',
    phone: null, customerName: 'Khan family', rooms: ['Makkah 101 (Quad)'] }]
};

describe('TravelNexus Umrah groups', () => {
  it('maps every group status, member status, package status and leg type', () => {
    for (const s of TRAVEL_GROUP_STATUSES) { expect(GROUP_STATUS_LABEL[s]).toBeTruthy(); expect(GROUP_STATUS_VARIANT[s]).toBeTruthy(); }
    expect(Object.keys(GROUP_TRANSITION_LABEL).length).toBe(10);
    expect(Object.keys(MEMBER_STATUS_VARIANT)).toEqual(['Registered', 'Confirmed', 'Cancelled']);
    expect(Object.keys(PACKAGE_STATUS_VARIANT)).toEqual(['Draft', 'Published', 'Archived']);
    for (const l of TRAVEL_TRANSPORT_LEG_TYPES) expect(LEG_TYPE_LABEL[l]).toBeTruthy();
    expect(TRAVEL_ROOM_TYPES.map(t => ROOM_CAPACITY[t])).toEqual([4, 3, 2, 1]);
  });

  it('defaults the price category from age at departure', () => {
    expect(defaultPriceCategory(null, '2026-03-01')).toBe('Adult');
    expect(defaultPriceCategory('2025-01-01', '2026-03-01')).toBe('Infant');
    expect(defaultPriceCategory('2024-03-02', '2026-03-01')).toBe('Infant'); // turns 2 the day after departure
    expect(defaultPriceCategory('2016-01-01', '2026-03-01')).toBe('Child');
    expect(defaultPriceCategory('2014-03-01', '2026-03-01')).toBe('Adult');
  });

  it('flags over-capacity, wrong-gender and mixed family rooms', () => {
    const members = [member(1, 'Male', 1), member(2, 'Female', 1), member(3, 'Male', 2), member(4, 'Female', 3, 40, 30)];
    expect(roomIssues({ roomNumber: null, roomType: 'Double', genderPolicy: 'Male', memberIds: [1, 3] }, members)).toEqual([]);
    expect(roomIssues({ roomNumber: null, roomType: 'Single', genderPolicy: 'Male', memberIds: [1, 3] }, members)[0]).toContain('over capacity');
    expect(roomIssues({ roomNumber: null, roomType: 'Double', genderPolicy: 'Male', memberIds: [1, 2] }, members)[0]).toContain('other genders');
    expect(roomIssues({ roomNumber: null, roomType: 'Double', genderPolicy: 'Family', memberIds: [1, 2] }, members)).toEqual([]);
    expect(roomIssues({ roomNumber: null, roomType: 'Double', genderPolicy: 'Family', memberIds: [2, 3] }, members)[0]).toContain('mahram');
    expect(roomIssues({ roomNumber: null, roomType: 'Double', genderPolicy: 'Family', memberIds: [3, 4] }, members)).toEqual([]); // mahram link
  });

  it('builds manifest rows with the expected columns and keeps masked passports masked', () => {
    const rows = manifestPilgrimRows(manifest);
    expect(rows[0].length).toBe(MANIFEST_PILGRIM_HEADERS.length);
    expect(rows[0]).toEqual([1, 'KHAN', 'AISHA', 'Female', '1990-04-02', 'PK', '******4567', '2030-01-01', '', '', 'Ali Khan', '', 'Khan family', 'Makkah 101 (Quad)']);
    expect(manifestFileName(manifest, 'xlsx')).toBe('manifest-GRP-2026-00001.xlsx');
  });

  it('builds an Excel workbook with pilgrims, hotels and transport sheets, and a PDF', () => {
    const book = buildManifestWorkbook(manifest);
    expect(book.SheetNames).toEqual(['Pilgrims', 'Hotels', 'Transport']);
    const pilgrims = XLSX.utils.sheet_to_json<string[]>(book.Sheets['Pilgrims'], { header: 1 });
    expect(pilgrims.some(r => r.includes('******4567'))).toBeTrue();
    expect(pilgrims.flat().join(' ')).toContain('passport numbers masked');
    expect(XLSX.utils.sheet_to_json<string[]>(book.Sheets['Hotels'], { header: 1 })[1]).toEqual(['Makkah', 'Hilton Makkah', '2026-03-01', '2026-03-11', 'H-77']);
    expect(buildManifestPdf(manifest).getNumberOfPages()).toBeGreaterThan(0);
  });

  describe('services', () => {
    let http: HttpTestingController;
    let groups: TravelGroupService;
    let packages: TravelPackageService;
    const base = `${environment.apiUrl}/travel`;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
      groups = TestBed.inject(TravelGroupService);
      packages = TestBed.inject(TravelPackageService);
    });
    afterEach(() => http.verify());

    it('sends group list filters including the departure range, omitting empty ones', () => {
      groups.getGroups({ page: 2, pageSize: 20, search: ' ', status: '', departureFrom: '2026-03-01', departureTo: '2026-03-31' }).subscribe();
      const req = http.expectOne(r => r.url === `${base}/groups`);
      expect(req.request.params.get('departureFrom')).toBe('2026-03-01');
      expect(req.request.params.get('departureTo')).toBe('2026-03-31');
      expect(req.request.params.has('status')).toBeFalse();
      expect(req.request.params.has('search')).toBeFalse();
      req.flush({ items: [], totalCount: 0, page: 2, pageSize: 20 });
    });

    it('routes group actions to their endpoints', () => {
      groups.transition(3, 'StartVisaProcessing', null).subscribe();
      expect(http.expectOne(`${base}/groups/3/transition`).request.body).toEqual({ action: 'StartVisaProcessing', note: null });
      groups.register(3, 7, [{ travelerId: 1, roomPreference: 'Quad', priceCategory: 'Adult' }]).subscribe();
      expect(http.expectOne(`${base}/groups/3/members`).request.body).toEqual({ customerId: 7, members: [{ travelerId: 1, roomPreference: 'Quad', priceCategory: 'Adult' }] });
      groups.cancelMember(3, 9, 'Ill').subscribe();
      expect(http.expectOne(`${base}/groups/3/members/9/cancel`).request.body).toEqual({ reason: 'Ill' });
      groups.saveStay(3, null, { hotelId: 1, checkIn: '2026-03-01', checkOut: '2026-03-11', confirmationNumber: null }).subscribe();
      expect(http.expectOne(`${base}/groups/3/stays`).request.method).toBe('POST');
      groups.saveStay(3, 4, { hotelId: 1, checkIn: '2026-03-01', checkOut: '2026-03-11', confirmationNumber: null }).subscribe();
      expect(http.expectOne(`${base}/groups/3/stays/4`).request.method).toBe('PUT');
      groups.saveRooming(3, 4, [{ roomNumber: '101', roomType: 'Double', genderPolicy: 'Family', memberIds: [1, 2] }]).subscribe();
      expect(http.expectOne(`${base}/groups/3/stays/4/rooming`).request.body).toEqual({ rooms: [{ roomNumber: '101', roomType: 'Double', genderPolicy: 'Family', memberIds: [1, 2] }] });
      groups.suggestRooming(3, 4).subscribe();
      expect(http.expectOne(`${base}/groups/3/stays/4/rooming/suggest`).request.method).toBe('GET');
      groups.getManifest(3).subscribe();
      expect(http.expectOne(`${base}/groups/3/manifest`).request.method).toBe('GET');
    });

    it('routes package publish/archive and hotel filters', () => {
      packages.publish(5).subscribe();
      expect(http.expectOne(`${base}/packages/5/publish`).request.method).toBe('POST');
      packages.archive(5).subscribe();
      expect(http.expectOne(`${base}/packages/5/archive`).request.method).toBe('POST');
      packages.getHotels('Makkah', true).subscribe();
      const req = http.expectOne(r => r.url === `${base}/hotels`);
      expect(req.request.params.get('city')).toBe('Makkah');
      expect(req.request.params.get('isActive')).toBe('true');
      req.flush([]);
    });
  });
});
