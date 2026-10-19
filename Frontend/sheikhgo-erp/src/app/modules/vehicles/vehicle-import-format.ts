import { CreateVehicleDto, FuelType, normalizeFuelType } from '../../core/models/vehicle.model';

export const VEHICLE_IMPORT_HEADERS = [
  'Name',
  'RegistrationNumber',
  'Make',
  'Model',
  'Year',
  'SeatingCapacity',
  'FuelAverage',
  'FuelType',
  'CurrentMileage',
  'Color',
  'VehicleType'
] as const;

export const VEHICLE_IMPORT_SAMPLE_ROWS: string[][] = [
  ['Toyota Hiace', 'LES-1001', 'Toyota', 'Hiace', '2022', '14', '10', 'Diesel', '45000', 'White', 'Van'],
  ['Suzuki Bolan', 'LES-1002', 'Suzuki', 'Bolan', '2020', '7', '14', 'Petrol', '32000', 'Silver', 'Van']
];

export type VehicleImportSeverity = 'ready' | 'warning' | 'error';

export interface ParsedVehicleImportRow {
  row: number;
  severity: VehicleImportSeverity;
  messages: string[];
  dto: CreateVehicleDto | null;
  registration: string;
}

function cell(row: Record<string, unknown>, ...keys: string[]): string {
  for (const k of keys) {
    const direct = row[k] ?? row[k.toLowerCase()] ?? row[k.replace(/\s/g, '')];
    if (direct != null && String(direct).trim() !== '') return String(direct).trim();
  }
  return '';
}

export function parseVehicleImportRecords(records: Record<string, unknown>[]): ParsedVehicleImportRow[] {
  const seen = new Set<string>();
  return records.map((raw, i) => {
    const row = i + 1;
    const messages: string[] = [];
    const name = cell(raw, 'Name', 'name');
    const registration = cell(raw, 'RegistrationNumber', 'Registration', 'Plate', 'registrationNumber');
    const make = cell(raw, 'Make', 'make') || null;
    const model = cell(raw, 'Model', 'model') || null;
    const yearRaw = cell(raw, 'Year', 'year');
    const seatingRaw = cell(raw, 'SeatingCapacity', 'Seats', 'seatingCapacity');
    const fuelAvgRaw = cell(raw, 'FuelAverage', 'fuelAverage');
    const fuelTypeRaw = cell(raw, 'FuelType', 'fuelType') || 'Petrol';
    const mileageRaw = cell(raw, 'CurrentMileage', 'Mileage', 'currentMileage');
    const color = cell(raw, 'Color', 'color') || null;
    const vehicleType = cell(raw, 'VehicleType', 'Type', 'vehicleType') || null;

    if (!name) messages.push('Name is required.');
    if (!registration) messages.push('Registration number is required.');
    const regKey = registration.toUpperCase();
    if (registration && !seen.add(regKey)) messages.push('Duplicate registration in file.');

    const year = yearRaw ? Number(yearRaw) : null;
    const seating = seatingRaw ? Number(seatingRaw) : 7;
    const fuelAverage = fuelAvgRaw ? Number(fuelAvgRaw) : 10;
    const currentMileage = mileageRaw ? Number(mileageRaw) : 0;
    if (seating <= 0) messages.push('Seating capacity must be > 0.');
    if (fuelAverage <= 0) messages.push('Fuel average must be > 0.');

    const fuelType = normalizeFuelType(fuelTypeRaw, FuelType.Petrol);
    const severity: VehicleImportSeverity =
      messages.some(m => !m.includes('Duplicate') && m.includes('required')) || seating <= 0 || fuelAverage <= 0
        ? 'error'
        : messages.length
          ? 'warning'
          : 'ready';

    const dto: CreateVehicleDto | null =
      severity === 'error'
        ? null
        : {
            name,
            registrationNumber: registration,
            model,
            year: year && Number.isFinite(year) ? year : null,
            seatingCapacity: seating,
            fuelAverage,
            fuelType,
            currentMileage: Number.isFinite(currentMileage) ? currentMileage : 0,
            insuranceExpiryDate: null,
            make,
            color,
            vehicleType
          };

    return { row, severity: dto ? (messages.length ? 'warning' : 'ready') : 'error', messages, dto, registration };
  });
}
