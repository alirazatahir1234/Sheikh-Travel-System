import { DashboardSummary } from '../../../core/models/common.model';

/**
 * Offline fallback for the default (operations) dashboard. The dashboard uses the
 * live API first and only falls back to these values when the request fails.
 */
export const DEFAULT_DASHBOARD_FALLBACK: DashboardSummary = {
  totalVehicles: 12,
  activeTrips: 4,
  totalRevenue: 450000,
  pendingBookings: 7,
  fuelExpense: 85000,
  netProfit: 320000
};
