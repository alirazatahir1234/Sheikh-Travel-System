import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';
import { LEG_TYPE_LABEL, TravelGroupManifest } from '../../models/travel.models';

/**
 * Manifest builders (client-side PDF and Excel). Values are written exactly as the API returned them:
 * passport numbers stay masked when the manifest was generated without Travel.Traveler.ViewPII.
 */
const day = (iso: string | null): string => (iso ? iso.substring(0, 10) : '');
const dateTime = (iso: string): string => iso.substring(0, 16).replace('T', ' ');

export const MANIFEST_PILGRIM_HEADERS = [
  'No', 'Surname', 'Given names', 'Gender', 'Date of birth', 'Nationality', 'Passport no.', 'Passport expiry',
  'Visa no.', 'Visa expiry', 'Mahram', 'Phone', 'Booking party', 'Rooms'
];

export function manifestPilgrimRows(m: TravelGroupManifest): (string | number)[][] {
  return m.pilgrims.map(p => [
    p.no, p.surname, p.givenNames, p.gender, day(p.dateOfBirth), p.nationalityCode ?? '', p.passportNumber ?? '',
    day(p.passportExpiryDate), p.visaNumber ?? '', day(p.visaExpiryDate), p.mahramName ?? '', p.phone ?? '', p.customerName,
    p.rooms.join(' | ')
  ]);
}

export function manifestStayRows(m: TravelGroupManifest): string[][] {
  return m.stays.map(s => [s.city, s.hotelName, day(s.checkIn), day(s.checkOut), s.confirmationNumber ?? '']);
}

export function manifestTransportRows(m: TravelGroupManifest): string[][] {
  return m.transport.map(t => [
    dateTime(t.departAt), LEG_TYPE_LABEL[t.legType], t.fromPlace, t.toPlace, t.providerName ?? '', t.vehicleType ?? '',
    t.seats !== null ? String(t.seats) : '', t.fleetBookingNumber ?? ''
  ]);
}

function headerLines(m: TravelGroupManifest): string[] {
  return [
    `${m.groupNumber} · ${m.groupName}`,
    `Package: ${m.packageName} · ${day(m.departureDate)} → ${day(m.returnDate)}`,
    `Leader: ${m.leaderName ?? '—'}${m.leaderPhone ? ' (' + m.leaderPhone + ')' : ''} · Visa case: ${m.visaCaseNumber ?? '—'}`,
    `Generated ${dateTime(m.generatedAtUtc)} UTC${m.piiMasked ? ' · passport numbers masked' : ''}`
  ];
}

export function manifestFileName(m: TravelGroupManifest, ext: 'pdf' | 'xlsx'): string {
  return `manifest-${m.groupNumber}.${ext}`;
}

export function buildManifestPdf(m: TravelGroupManifest): jsPDF {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });
  doc.setFontSize(14);
  doc.text('Group manifest', 40, 40);
  doc.setFontSize(9);
  headerLines(m).forEach((l, i) => doc.text(l, 40, 58 + i * 12));
  const next = () => ((doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY ?? 100) + 20;

  autoTable(doc, { startY: 110, head: [MANIFEST_PILGRIM_HEADERS], body: manifestPilgrimRows(m), styles: { fontSize: 7 }, headStyles: { fillColor: [30, 64, 175] } });
  if (m.stays.length) {
    autoTable(doc, { startY: next(), head: [['City', 'Hotel', 'Check-in', 'Check-out', 'Confirmation']], body: manifestStayRows(m), styles: { fontSize: 8 } });
  }
  if (m.transport.length) {
    autoTable(doc, { startY: next(), head: [['Departs', 'Leg', 'From', 'To', 'Provider', 'Vehicle', 'Seats', 'Fleet booking']], body: manifestTransportRows(m), styles: { fontSize: 8 } });
  }
  return doc;
}

export function buildManifestWorkbook(m: TravelGroupManifest): XLSX.WorkBook {
  const book = XLSX.utils.book_new();
  const pilgrims = XLSX.utils.aoa_to_sheet([...headerLines(m).map(l => [l]), [], MANIFEST_PILGRIM_HEADERS, ...manifestPilgrimRows(m)]);
  XLSX.utils.book_append_sheet(book, pilgrims, 'Pilgrims');
  XLSX.utils.book_append_sheet(book, XLSX.utils.aoa_to_sheet([['City', 'Hotel', 'Check-in', 'Check-out', 'Confirmation'], ...manifestStayRows(m)]), 'Hotels');
  XLSX.utils.book_append_sheet(book, XLSX.utils.aoa_to_sheet([['Departs', 'Leg', 'From', 'To', 'Provider', 'Vehicle', 'Seats', 'Fleet booking'], ...manifestTransportRows(m)]), 'Transport');
  return book;
}

export function downloadManifestPdf(m: TravelGroupManifest): void { buildManifestPdf(m).save(manifestFileName(m, 'pdf')); }
export function downloadManifestXlsx(m: TravelGroupManifest): void { XLSX.writeFile(buildManifestWorkbook(m), manifestFileName(m, 'xlsx'), { compression: true }); }
