export type BookingStatus = 'Pending' | 'Confirmed' | 'InProgress' | 'Completed' | 'Cancelled';

export interface Booking {
  id: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  routeId: number;
  routeName: string;
  vehicleId?: number;
  vehicleName?: string;
  driverId?: number;
  driverName?: string;
  pickupTime: string;
  passengerCount: number;
  totalAmount: number;
  status: BookingStatus;
  notes?: string;
  createdAt: string;
}

export interface CreateBookingRequest {
  customerId: number;
  routeId: number;
  pickupTime: string;
  passengerCount: number;
  totalAmount: number;
  notes?: string;
}

export interface AssignDriverRequest {
  bookingId: number;
  driverId: number;
}

export interface AssignVehicleRequest {
  bookingId: number;
  vehicleId: number;
}

export interface UpdateBookingStatusRequest {
  bookingId: number;
  status: BookingStatus;
}
