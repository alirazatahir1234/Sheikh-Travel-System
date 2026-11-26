import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import {
  CABIN_LABEL, DIRECTION_LABEL, LEG_TYPE_LABEL, MEAL_PLAN_LABEL, TravelBooking, TravelGroupTransport, TravelHotelBooking
} from '../../models/travel.models';

/**
 * Client-side itinerary and hotel voucher. Neither document carries passport numbers; flight times are the
 * airport-local times printed on the ticket, labelled with the airport code.
 */
export interface ItineraryRow { sortKey: string; date: string; time: string; kind: 'Flight' | 'Hotel' | 'Transfer'; title: string; detail: string; }

const day = (iso: string): string => iso.substring(0, 10);
const hm = (iso: string): string => iso.substring(11, 16);
const asUtcKey = (iso: string): string => (iso.endsWith('Z') ? iso.slice(0, -1) : iso).substring(0, 19);

export function itineraryRows(b: TravelBooking, transport: TravelGroupTransport[] = []): ItineraryRow[] {
  const rows: ItineraryRow[] = [];
  for (const f of b.flights.filter(x => x.status !== 'Cancelled')) {
    for (const s of f.segments) {
      rows.push({
        sortKey: asUtcKey(s.departUtc), date: day(s.departLocal), time: `${hm(s.departLocal)} ${s.fromAirport}`, kind: 'Flight',
        title: `${s.airlineCode} ${s.flightNumber} · ${s.fromCity || s.fromAirport} (${s.fromAirport}) → ${s.toCity || s.toAirport} (${s.toAirport})`,
        detail: [`Arrives ${day(s.arriveLocal)} ${hm(s.arriveLocal)} ${s.toAirport}`, CABIN_LABEL[s.cabinClass], s.baggage ? `Baggage ${s.baggage}` : null,
          f.pnr ? `PNR ${f.pnr}` : null, DIRECTION_LABEL[f.direction]].filter(Boolean).join(' · ')
      });
    }
  }
  for (const h of b.hotels.filter(x => x.status !== 'Cancelled')) {
    rows.push({
      sortKey: `${day(h.checkIn)}T12:00:00`, date: day(h.checkIn), time: 'Check-in', kind: 'Hotel', title: `${h.hotelName}, ${h.city}`,
      detail: [`${h.nights} night(s) until ${day(h.checkOut)}`, roomsLabel(h), MEAL_PLAN_LABEL[h.mealPlan],
        h.confirmationNumber ? `Confirmation ${h.confirmationNumber}` : null].filter(Boolean).join(' · ')
    });
  }
  for (const t of transport) {
    rows.push({
      sortKey: asUtcKey(t.departAt), date: day(t.departAt), time: hm(t.departAt) + ' UTC', kind: 'Transfer',
      title: `${LEG_TYPE_LABEL[t.legType]} · ${t.fromPlace} → ${t.toPlace}`,
      detail: [t.providerName, t.vehicleType].filter(Boolean).join(' · ')
    });
  }
  return rows.sort((a, c) => a.sortKey.localeCompare(c.sortKey));
}

export function roomsLabel(h: TravelHotelBooking): string {
  return h.rooms.map(r => `${r.quantity}× ${r.roomType}`).join(', ');
}

export function passengerRows(b: TravelBooking): string[][] {
  return b.flights.filter(f => f.status !== 'Cancelled').flatMap(f => f.passengers.map(p => [
    p.travelerName, p.passengerType, f.pnr ?? '—', p.ticketNumber ?? '—', p.ticketStatus
  ]));
}

export function buildItineraryPdf(b: TravelBooking, transport: TravelGroupTransport[] = []): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  doc.setFontSize(14);
  doc.text('Travel itinerary', 40, 44);
  doc.setFontSize(9);
  doc.text(`${b.bookingNumber} · ${b.title}`, 40, 62);
  doc.text(`For: ${b.ownerName}`, 40, 74);
  doc.text('Flight times are local to each airport.', 40, 86);
  autoTable(doc, {
    startY: 100, head: [['Date', 'Time', 'Service', 'Details']],
    body: itineraryRows(b, transport).map(r => [r.date, r.time, r.title, r.detail]),
    styles: { fontSize: 8 }, headStyles: { fillColor: [30, 64, 175] }, columnStyles: { 0: { cellWidth: 62 }, 1: { cellWidth: 62 } }
  });
  const passengers = passengerRows(b);
  if (passengers.length) {
    const y = ((doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY ?? 100) + 20;
    autoTable(doc, { startY: y, head: [['Passenger', 'Type', 'PNR', 'Ticket', 'Ticket status']], body: passengers, styles: { fontSize: 8 } });
  }
  return doc;
}

export function voucherLines(b: TravelBooking, h: TravelHotelBooking): [string, string][] {
  return [
    ['Booking', `${b.bookingNumber} · ${b.title}`],
    ['Hotel', `${h.hotelName}, ${h.city}`],
    ['Address', h.hotelAddress ?? '—'],
    ['Hotel phone', h.hotelPhone ?? '—'],
    ['Check-in', day(h.checkIn)],
    ['Check-out', day(h.checkOut)],
    ['Nights', String(h.nights)],
    ['Rooms', roomsLabel(h)],
    ['Meal plan', MEAL_PLAN_LABEL[h.mealPlan]],
    ['Confirmation no.', h.confirmationNumber ?? 'Pending'],
    ['Booked through', h.supplierName ?? '—'],
    ['Special requests', h.specialRequests ?? '—']
  ];
}

export function buildVoucherPdf(b: TravelBooking, h: TravelHotelBooking): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  doc.setFontSize(14);
  doc.text('Hotel voucher', 40, 44);
  autoTable(doc, { startY: 60, body: voucherLines(b, h), theme: 'plain', styles: { fontSize: 10 }, columnStyles: { 0: { fontStyle: 'bold', cellWidth: 130 } } });
  const y = ((doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY ?? 200) + 16;
  autoTable(doc, { startY: y, head: [['Guests']], body: h.guests.map(g => [g.travelerName]), styles: { fontSize: 10 } });
  return doc;
}

export const itineraryFileName = (b: TravelBooking): string => `itinerary-${b.bookingNumber}.pdf`;
export const voucherFileName = (b: TravelBooking, h: TravelHotelBooking): string => `voucher-${b.bookingNumber}-${h.id}.pdf`;
