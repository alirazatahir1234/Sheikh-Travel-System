export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

type PagedResultWire<T> = Partial<PagedResult<T>> & {
  Items?: T[];
  TotalCount?: number;
  Page?: number;
  PageSize?: number;
  TotalPages?: number;
};

/** Normalizes API paged results that may use PascalCase property names. */
export function normalizePagedResult<T>(result: PagedResultWire<T>): PagedResult<T> {
  const items = result.items ?? result.Items ?? [];
  const totalCount = result.totalCount ?? result.TotalCount ?? items.length;
  const page = result.page ?? result.Page ?? 1;
  const pageSize = result.pageSize ?? result.PageSize ?? items.length;
  const totalPages = result.totalPages ?? result.TotalPages
    ?? (pageSize > 0 ? Math.max(1, Math.ceil(totalCount / pageSize)) : 1);
  return { items, totalCount, page, pageSize, totalPages };
}

export interface DashboardStats {
  totalBookings: number;
  totalRevenue: number;
  activeVehicles: number;
  activeDrivers: number;
  pendingBookings: number;
  todayBookings: number;
  monthlyRevenue: number;
  completedBookings: number;
}

/**
 * Mirrors SheikhTravelSystem.Application.Features.Dashboard.DTOs.DashboardSummaryDto.
 * This is the *real* shape returned by GET /api/dashboard/summary.
 */
export interface DashboardSummary {
  totalVehicles: number;
  activeTrips: number;
  totalRevenue: number;
  pendingBookings: number;
  fuelExpense: number;
  netProfit: number;
}

export interface RevenueReport {
  period: string;
  totalRevenue: number;
  totalBookings: number;
  averageAmount: number;
}

export interface BookingReport {
  status: string;
  count: number;
  percentage: number;
}

/** Backend BookingReportDto from /api/reports/bookings */
export interface BookingReportDto {
  totalBookings: number;
  completed: number;
  cancelled: number;
  pending: number;
  active: number;
}

/** Backend RevenueReportDto from /api/reports/revenue */
export interface RevenueReportDto {
  totalRevenue: number;
  fuelExpense: number;
  maintenanceCost: number;
  netProfit: number;
}

/** Backend VehicleProfitDto from /api/reports/vehicle-profit */
export interface VehicleProfitDto {
  vehicleId: number;
  vehicleName: string;
  revenue: number;
  fuelCost: number;
  maintenanceCost: number;
  profit: number;
}

/** Backend DriverPerformanceDto from /api/reports/driver-performance */
export interface DriverPerformanceDto {
  driverId: number;
  driverName: string;
  totalTrips: number;
  completedTrips: number;
  totalRevenue: number;
}

/** Backend PaymentDto used inside PaymentReportDto */
export interface PaymentReportItemDto {
  id: number;
  bookingId: number;
  amount: number;
  paymentMethod: string;
  status: number;
  paymentDate: string;
  transactionReference?: string;
  notes?: string;
  createdAt: string;
}

/** Backend PaymentReportDto from /api/payments/report */
export interface PaymentReportDto {
  totalReceived: number;
  totalPending: number;
  totalTransactions: number;
  recentPayments: PaymentReportItemDto[];
}
