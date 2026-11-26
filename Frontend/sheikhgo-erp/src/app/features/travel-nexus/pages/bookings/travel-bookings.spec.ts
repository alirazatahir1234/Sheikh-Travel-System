import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../../environments/environment';
import {
  BOOKING_STATUS_VARIANT, CABIN_LABEL, DIRECTION_LABEL, FLIGHT_STATUS_LABEL, FLIGHT_STATUS_VARIANT, HOTEL_BOOKING_STATUS_LABEL,
  HOTEL_BOOKING_STATUS_VARIANT, MEAL_PLAN_LABEL, REFUND_STATUS_LABEL, REFUND_STATUS_VARIANT, SUPPLIER_TYPE_LABEL, TICKET_STATUS_VARIANT,
  TRAVEL_BOOKING_STATUSES, TRAVEL_CABIN_CLASSES, TRAVEL_FLIGHT_DIRECTIONS, TRAVEL_FLIGHT_STATUSES, TRAVEL_HOTEL_BOOKING_STATUSES, TRAVEL_MEAL_PLANS,
  TRAVEL_REFUND_STATUSES, TRAVEL_SUPPLIER_TYPES, TravelBooking, TravelGroupRoom, TravelGroupTransport
} from '../../models/travel.models';
import { TravelBookingService } from '../../services/travel-booking.service';
import { TravelSupplierService } from '../../services/travel-supplier.service';
import { buildItineraryPdf, buildVoucherPdf, itineraryRows, passengerRows, voucherLines } from './booking-documents';
import { roomsFromRooming } from './hotel-editor-drawer.component';

const booking: TravelBooking = {
  id: 70, bookingNumber: 'BKG-2027-00001', title: 'Umrah March', customerId: 5, groupId: null, ownerName: 'Khan family', status: 'Open',
  firstTravelDate: '2027-03-01T00:00:00', lastTravelDate: '2027-03-15T00:00:00', flightCount: 1, hotelCount: 1, pnrs: 'ABC123',
  nextTicketingLimitUtc: null, assignedAgentName: null, createdAt: '2027-01-01T00:00:00', customerName: 'Khan family', groupName: null,
  groupNumber: null, enquiryId: null, enquiryNumber: null, visaCaseId: null, visaCaseNumber: null, assignedAgentUserId: null, branchId: null,
  branchName: null, notes: null, completedAt: null, cancelReason: null, allowedActions: ['Edit'], cancellations: [], history: [], totals: [],
  flights: [
    {
      id: 1, supplierId: null, supplierName: null, direction: 'Outbound', pnr: 'ABC123', airlinePnr: null, status: 'Ticketed', ticketingTimeLimitUtc: null,
      sellAmount: 160000, costAmount: 100000, currencyCode: 'PKR', notes: null, allowedActions: [],
      segments: [{
        id: 11, flightBookingId: 1, sequence: 1, airlineCode: 'PK', flightNumber: '759', fromAirport: 'LHE', fromCity: 'Lahore', toAirport: 'JED',
        toCity: 'Jeddah', departLocal: '2027-03-01T03:00:00', arriveLocal: '2027-03-01T05:30:00', departUtc: '2027-02-28T22:00:00',
        arriveUtc: '2027-03-01T02:30:00', cabinClass: 'Economy', bookingClass: null, baggage: '2PC'
      }],
      passengers: [{ id: 5, flightBookingId: 1, travelerId: 11, travelerName: 'Aisha Khan', groupMemberId: null, passengerType: 'Adult',
        passportNumber: '******567', ticketNumber: '2142345678901', ticketStatus: 'Issued' }]
    },
    {
      id: 2, supplierId: null, supplierName: null, direction: 'Return', pnr: 'XYZ999', airlinePnr: null, status: 'Cancelled', ticketingTimeLimitUtc: null,
      sellAmount: null, costAmount: null, currencyCode: null, notes: null, allowedActions: [],
      segments: [{
        id: 12, flightBookingId: 2, sequence: 1, airlineCode: 'SV', flightNumber: '1', fromAirport: 'MED', fromCity: 'Madinah', toAirport: 'LHE',
        toCity: 'Lahore', departLocal: '2027-03-15T10:00:00', arriveLocal: '2027-03-15T17:00:00', departUtc: '2027-03-15T07:00:00',
        arriveUtc: '2027-03-15T12:00:00', cabinClass: 'Economy', bookingClass: null, baggage: null
      }],
      passengers: []
    }
  ],
  hotels: [{
    id: 400, hotelId: 1, hotelName: 'Hilton Makkah', city: 'Makkah', hotelAddress: 'Ibrahim Al Khalil St', hotelPhone: null, supplierId: null,
    supplierName: 'Bed bank', groupHotelId: null, checkIn: '2027-03-01T00:00:00', checkOut: '2027-03-11T00:00:00', nights: 10, mealPlan: 'Breakfast',
    confirmationNumber: 'H-77', status: 'Confirmed', specialRequests: null, sellAmount: 4000, costAmount: 3200, currencyCode: 'SAR', allowedActions: [],
    rooms: [{ hotelBookingId: 400, roomType: 'Double', quantity: 2 }],
    guests: [{ hotelBookingId: 400, travelerId: 11, travelerName: 'Aisha Khan' }]
  }]
};

describe('TravelNexus bookings', () => {
  it('maps every booking, flight, hotel, refund, supplier, cabin and meal value', () => {
    for (const s of TRAVEL_BOOKING_STATUSES) expect(BOOKING_STATUS_VARIANT[s]).toBeTruthy();
    for (const s of TRAVEL_FLIGHT_STATUSES) { expect(FLIGHT_STATUS_LABEL[s]).toBeTruthy(); expect(FLIGHT_STATUS_VARIANT[s]).toBeTruthy(); }
    for (const s of TRAVEL_HOTEL_BOOKING_STATUSES) { expect(HOTEL_BOOKING_STATUS_LABEL[s]).toBeTruthy(); expect(HOTEL_BOOKING_STATUS_VARIANT[s]).toBeTruthy(); }
    for (const s of TRAVEL_REFUND_STATUSES) { expect(REFUND_STATUS_LABEL[s]).toBeTruthy(); expect(REFUND_STATUS_VARIANT[s]).toBeTruthy(); }
    for (const s of TRAVEL_SUPPLIER_TYPES) expect(SUPPLIER_TYPE_LABEL[s]).toBeTruthy();
    for (const s of TRAVEL_FLIGHT_DIRECTIONS) expect(DIRECTION_LABEL[s]).toBeTruthy();
    for (const s of TRAVEL_CABIN_CLASSES) expect(CABIN_LABEL[s]).toBeTruthy();
    for (const s of TRAVEL_MEAL_PLANS) expect(MEAL_PLAN_LABEL[s]).toBeTruthy();
    expect(Object.keys(TICKET_STATUS_VARIANT)).toEqual(['NotIssued', 'Issued', 'Voided', 'Refunded']);
  });

  it('builds the itinerary in time order with airport-local times and skips cancelled services', () => {
    const transport: TravelGroupTransport[] = [{
      id: 1, legType: 'AirportTransfer', departAt: '2027-03-01T03:30:00', fromPlace: 'JED airport', toPlace: 'Makkah hotel', providerName: 'Coach co',
      vehicleType: 'Coach', seats: 49, fleetBookingId: null, fleetBookingNumber: null, notes: null
    }];
    const rows = itineraryRows(booking, transport);
    expect(rows.map(r => r.kind)).toEqual(['Flight', 'Transfer', 'Hotel']);
    expect(rows[0].time).toBe('03:00 LHE');
    expect(rows[0].detail).toContain('Arrives 2027-03-01 05:30 JED');
    expect(rows[0].detail).toContain('PNR ABC123');
    expect(rows.some(r => r.title.includes('SV 1'))).toBeFalse();
    expect(rows[2].detail).toContain('2× Double');
    expect(rows[2].detail).toContain('Confirmation H-77');
  });

  it('never puts passport numbers in the itinerary or the voucher', () => {
    const text = [...itineraryRows(booking).flatMap(r => [r.title, r.detail]), ...passengerRows(booking).flat(),
      ...voucherLines(booking, booking.hotels[0]).flat()].join(' ');
    expect(text).not.toContain('******567');
    expect(passengerRows(booking)).toEqual([['Aisha Khan', 'Adult', 'ABC123', '2142345678901', 'Issued']]);
    expect(voucherLines(booking, booking.hotels[0])).toContain(['Confirmation no.', 'H-77']);
    expect(buildItineraryPdf(booking).getNumberOfPages()).toBeGreaterThan(0);
    expect(buildVoucherPdf(booking, booking.hotels[0]).getNumberOfPages()).toBeGreaterThan(0);
  });

  it('counts group rooming by room type for a hotel booking', () => {
    const rooms: TravelGroupRoom[] = [
      { id: 1, groupHotelId: 7, roomNumber: null, roomType: 'Quad', capacity: 4, genderPolicy: 'Family', memberIds: [1, 2, 3, 4] },
      { id: 2, groupHotelId: 7, roomNumber: null, roomType: 'Double', capacity: 2, genderPolicy: 'Male', memberIds: [5, 6] },
      { id: 3, groupHotelId: 7, roomNumber: null, roomType: 'Quad', capacity: 4, genderPolicy: 'Female', memberIds: [7] },
      { id: 4, groupHotelId: 8, roomNumber: null, roomType: 'Single', capacity: 1, genderPolicy: 'Male', memberIds: [1] }
    ];
    expect(roomsFromRooming(7, rooms)).toEqual([{ roomType: 'Quad', quantity: 2 }, { roomType: 'Double', quantity: 1 }]);
    expect(roomsFromRooming(9, rooms)).toEqual([]);
  });

  describe('services', () => {
    let http: HttpTestingController;
    let bookings: TravelBookingService;
    let suppliers: TravelSupplierService;
    const base = `${environment.apiUrl}/travel`;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
      bookings = TestBed.inject(TravelBookingService);
      suppliers = TestBed.inject(TravelSupplierService);
    });
    afterEach(() => http.verify());

    it('sends the travel date range and ticketing-due filter, omitting empty ones', () => {
      bookings.getBookings({ page: 1, pageSize: 20, search: '', status: '', ownerType: 'Group', travelFrom: '2027-03-01', travelTo: '2027-03-31', ticketingDue: true }).subscribe();
      const req = http.expectOne(r => r.url === `${base}/bookings`);
      expect(req.request.params.get('travelFrom')).toBe('2027-03-01');
      expect(req.request.params.get('travelTo')).toBe('2027-03-31');
      expect(req.request.params.get('ticketingDue')).toBe('true');
      expect(req.request.params.get('ownerType')).toBe('Group');
      expect(req.request.params.has('status')).toBeFalse();
      expect(req.request.params.has('search')).toBeFalse();
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    });

    it('routes flight, hotel, cancellation and refund calls', () => {
      bookings.transitionFlight(70, 1, 'Hold', 'ABC123', '2027-02-20T10:00:00.000Z', null).subscribe();
      expect(http.expectOne(`${base}/bookings/70/flights/1/transition`).request.body)
        .toEqual({ action: 'Hold', pnr: 'ABC123', ticketingTimeLimitUtc: '2027-02-20T10:00:00.000Z', note: null });
      bookings.setTickets(70, 1, [{ passengerId: 5, ticketNumber: '214-2345678901' }]).subscribe();
      expect(http.expectOne(`${base}/bookings/70/flights/1/tickets`).request.method).toBe('PUT');
      const cancel = { reason: 'Ill', supplierPenalty: 0, customerPenalty: 5000, refundFromSupplier: 0, refundToCustomer: 20000, ticketOutcome: 'Refunded' as const };
      bookings.cancelPassenger(70, 1, 5, cancel).subscribe();
      expect(http.expectOne(`${base}/bookings/70/flights/1/passengers/5/cancel`).request.body).toEqual(cancel);
      bookings.cancelHotel(70, 400, cancel).subscribe();
      expect(http.expectOne(`${base}/bookings/70/hotels/400/cancel`).request.body.ticketOutcome).toBeNull();
      bookings.transitionHotel(70, 400, 'Confirm', 'H-77', null).subscribe();
      expect(http.expectOne(`${base}/bookings/70/hotels/400/transition`).request.body).toEqual({ action: 'Confirm', confirmationNumber: 'H-77', note: null });
      bookings.updateRefundStatus(70, 9, 'Closed', null).subscribe();
      expect(http.expectOne(`${base}/bookings/70/cancellations/9/refund-status`).request.body).toEqual({ refundStatus: 'Closed', note: null });
      bookings.getByGroup(60).subscribe();
      expect(http.expectOne(`${base}/bookings/by-group/60`).request.method).toBe('GET');
    });

    it('loads suppliers by type and airports once', () => {
      suppliers.getSuppliers('Airline', true).subscribe();
      const req = http.expectOne(r => r.url === `${base}/suppliers`);
      expect(req.request.params.get('supplierType')).toBe('Airline');
      req.flush([]);
      suppliers.airports$.subscribe();
      suppliers.airports$.subscribe();
      http.expectOne(`${base}/airports`).flush([]);
    });
  });
});
