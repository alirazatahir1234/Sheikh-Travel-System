import { DashboardSummary } from '../../../core/models/common.model';

/**
 * Empty fallback when the summary API fails. Never invent demo KPIs — operators
 * must see zeros + an error, not fabricated fleet numbers.
 */
export const DEFAULT_DASHBOARD_FALLBACK: DashboardSummary = {
  totalVehicles: 0,
  activeTrips: 0,
  totalRevenue: 0,
  pendingBookings: 0,
  fuelExpense: 0,
  netProfit: 0
};
