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
