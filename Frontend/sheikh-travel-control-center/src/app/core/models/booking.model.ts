export type BookingStatus = 'Pending' | 'Confirmed' | 'Started' | 'Completed' | 'Cancelled';

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

/** Matches backend CreateBookingDto (CustomerId, RouteId, PickupTime, PassengerCount, TotalAmount, Notes). */
export interface CreateBookingDto {
  customerId: number;
  routeId: number;
  pickupTime: string;
  passengerCount: number;
  totalAmount: number;
  notes?: string | null;
}

/** Envelope sent on POST /api/bookings — backend expects `{ booking: CreateBookingDto }`. */
export interface CreateBookingRequest {
  booking: CreateBookingDto;
}

/** Matches backend UpdateBookingDto. */
export interface UpdateBookingDto {
  customerId: number;
  routeId: number;
  pickupTime: string;
  passengerCount: number;
  totalAmount: number;
  vehicleId?: number | null;
  driverId?: number | null;
  notes?: string | null;
}

/** Envelope sent on PUT /api/bookings/:id — backend expects `{ booking: UpdateBookingDto }`. */
export interface UpdateBookingRequest {
  booking: UpdateBookingDto;
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
  cancellationReason?: string;
}
