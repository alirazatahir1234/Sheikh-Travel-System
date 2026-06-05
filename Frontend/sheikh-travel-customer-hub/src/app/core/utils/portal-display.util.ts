import { PortalPayState, PortalRouteDto, PortalVehicleDto } from '../models/portal.models';

/** Labels match admin `FuelTypeLabels`. */
export function portalFuelTypeLabel(fuelType: number): string {
  switch (fuelType) {
    case 1:
      return 'Petrol';
    case 2:
      return 'Diesel';
    case 3:
      return 'CNG';
    default:
      return 'Fuel';
  }
}

/** Labels match admin `VehicleStatusLabels`. */
export function portalVehicleStatusLabel(status: number): string {
  switch (status) {
    case 1:
      return 'Available';
    case 2:
      return 'On trip';
    case 3:
      return 'Maintenance';
    case 4:
      return 'Retired';
    default:
      return 'Status';
  }
}

/** One-line text aligned with admin route list columns (name, from, to, km, base price). */
export function portalRouteOptionLabel(r: PortalRouteDto): string {
  const routeName = r.name?.trim();
  const title = routeName ? `${routeName} · ` : '';
  const km = Number.isInteger(r.distanceKm) ? String(r.distanceKm) : r.distanceKm.toFixed(1);
  const price =
    typeof r.basePrice === 'number' && Math.floor(r.basePrice) === r.basePrice
      ? String(r.basePrice)
      : r.basePrice.toFixed(2);
  return `${title}${r.source} → ${r.destination} · ${km} km · Base PKR ${price}`;
}

/** One-line text aligned with admin vehicle list (name, registration, model, year, seats, fuel, status). */
export function portalVehicleOptionLabel(v: PortalVehicleDto): string {
  const model = v.model?.trim() || '—';
  const year = v.year != null ? String(v.year) : '—';
  const fuel = portalFuelTypeLabel(v.fuelType ?? 0);
  const st = portalVehicleStatusLabel(v.status ?? 0);
  return `${v.name} · ${v.registrationNumber} · ${model} · ${year} · ${v.seatingCapacity} seats · ${fuel} · ${st}`;
}

export function payStateLabel(state: PortalPayState): string {
  switch (state) {
    case 1:
      return 'Paid';
    case 2:
      return 'Partially paid';
    case 3:
      return 'Unpaid';
    default:
      return 'Unknown';
  }
}

export function payStateBadgeClass(state: PortalPayState): string {
  switch (state) {
    case 1:
      return 'bg-emerald-100 text-emerald-800 ring-emerald-600/20';
    case 2:
      return 'bg-amber-100 text-amber-900 ring-amber-600/20';
    case 3:
      return 'bg-rose-100 text-rose-800 ring-rose-600/20';
    default:
      return 'bg-slate-100 text-slate-700';
  }
}

/** BookingStatus enum from API (numeric). */
export function bookingStatusLabel(status: number): string {
  switch (status) {
    case 1:
      return 'Pending';
    case 2:
      return 'Confirmed';
    case 3:
      return 'Started';
    case 4:
      return 'Completed';
    case 5:
      return 'Cancelled';
    default:
      return 'Unknown';
  }
}

export function bookingStatusBadgeClass(status: number): string {
  if (status === 5) return 'bg-slate-200 text-slate-700';
  if (status === 4) return 'bg-emerald-100 text-emerald-800';
  if (status === 3) return 'bg-primary-100 text-primary-800';
  if (status === 2) return 'bg-sky-100 text-sky-900';
  return 'bg-amber-100 text-amber-900';
}

export function paymentLineStatusLabel(status: number): string {
  switch (status) {
    case 1:
      return 'Pending';
    case 2:
      return 'Partial';
    case 3:
      return 'Paid';
    case 4:
      return 'Refunded';
    default:
      return '—';
  }
}
