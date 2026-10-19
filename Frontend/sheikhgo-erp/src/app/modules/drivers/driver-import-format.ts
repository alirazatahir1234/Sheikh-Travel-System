import { CreateDriverDto } from '../../core/models/driver.model';

export const DRIVER_IMPORT_HEADERS = [
  'FirstName',
  'LastName',
  'Phone',
  'Email',
  'LicenseNumber',
  'LicenseExpiryDate',
  'DateOfBirth',
  'Gender',
  'Nationality'
] as const;

export const DRIVER_IMPORT_SAMPLE_ROWS: string[][] = [
  ['Ali', 'Khan', '03001234567', 'ali.khan@example.com', 'LIC-1001', '2030-12-31', '1995-05-15', 'Male', 'Pakistani'],
  ['Sara', 'Ahmed', '03007654321', 'sara.ahmed@example.com', 'LIC-1002', '2029-06-30', '1998-01-20', 'Female', 'Pakistani']
];

export type DriverImportSeverity = 'ready' | 'warning' | 'error';

export interface ParsedDriverImportRow {
  row: number;
  severity: DriverImportSeverity;
  messages: string[];
  dto: CreateDriverDto | null;
  phone: string;
}

function cell(row: Record<string, unknown>, ...keys: string[]): string {
  for (const k of keys) {
    const direct = row[k] ?? row[k.toLowerCase()];
    if (direct != null && String(direct).trim() !== '') return String(direct).trim();
  }
  return '';
}

function parseDate(value: string): Date | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function parseDriverImportRecords(records: Record<string, unknown>[]): ParsedDriverImportRow[] {
  const seen = new Set<string>();
  return records.map((raw, i) => {
    const row = i + 1;
    const messages: string[] = [];
    const firstName = cell(raw, 'FirstName', 'firstName');
    const lastName = cell(raw, 'LastName', 'lastName');
    const phone = cell(raw, 'Phone', 'phone');
    const email = cell(raw, 'Email', 'email');
    const licenseNumber = cell(raw, 'LicenseNumber', 'licenseNumber');
    const licenseExpiry = parseDate(cell(raw, 'LicenseExpiryDate', 'licenseExpiryDate'));
    const dob = parseDate(cell(raw, 'DateOfBirth', 'dateOfBirth'));
    const gender = cell(raw, 'Gender', 'gender') || null;
    const nationality = cell(raw, 'Nationality', 'nationality') || null;

    if (!firstName) messages.push('First name is required.');
    if (!lastName) messages.push('Last name is required.');
    if (!phone) messages.push('Phone is required.');
    if (!email) messages.push('Email is required.');
    if (!licenseNumber) messages.push('License number is required.');
    if (!licenseExpiry) messages.push('License expiry date is required (YYYY-MM-DD).');
    if (!dob) messages.push('Date of birth is required (YYYY-MM-DD).');
    const phoneKey = phone.replace(/\D/g, '');
    if (phone && !seen.add(phoneKey)) messages.push('Duplicate phone in file.');

    const hasHardError = messages.some(m => m.includes('required') || m.includes('YYYY-MM-DD'));
    const dto: CreateDriverDto | null = hasHardError
      ? null
      : {
          firstName,
          lastName,
          phone,
          email,
          licenseNumber,
          licenseExpiryDate: licenseExpiry!.toISOString(),
          dateOfBirth: dob!.toISOString(),
          gender,
          nationality
        };

    return {
      row,
      severity: dto ? (messages.length ? 'warning' : 'ready') : 'error',
      messages,
      dto,
      phone
    };
  });
}
