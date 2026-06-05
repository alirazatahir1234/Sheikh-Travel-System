export interface DriverTrip {
  id: number;
  bookingNumber: string;
  customerName: string;
  routeName: string;
  pickupTime: string;
  dropoffTime?: string | null;
  status: number;
  statusName: string;
  vehicleId?: number | null;
  vehicleName?: string | null;
  totalAmount: number;
}
