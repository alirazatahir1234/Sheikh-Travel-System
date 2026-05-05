/** Mirrors backend PortalPayState */
export type PortalPayState = 1 | 2 | 3;

export type PortalPaymentPlan = 'full' | 'partial' | 'payLater';

export interface PortalRouteDto {
  id: number;
  label: string;
  distanceKm: number;
  basePrice: number;
  source: string;
  destination: string;
  /** Same as admin Routes table “Route name”. */
  name?: string | null;
}

/** Numeric enums match `SheikhTravelSystem.Domain` / admin app. */
export type PortalFuelType = 1 | 2 | 3;
export type PortalVehicleStatus = 1 | 2 | 3 | 4;

export interface PortalVehicleDto {
  id: number;
  name: string;
  registrationNumber: string;
  seatingCapacity: number;
  fuelAverage: number;
  model?: string | null;
  year?: number | null;
  fuelType?: PortalFuelType;
  status?: PortalVehicleStatus;
}

export interface PriceBreakdown {
  distance: number;
  fuelAverage: number;
  fuelPricePerLiter: number;
  fuelCost: number;
  driverAllowance: number;
  tollCharges: number;
  otherCharges: number;
  totalAmount: number;
  isRoundTrip: boolean;
}

export interface PortalBookingCreatedDto {
  bookingId: number;
  bookingNumber: string;
  totalAmount: number;
  priceBreakdown: PriceBreakdown;
  paymentState: PortalPayState;
}

export interface CreatePortalBookingPayload {
  fullName: string;
  phone: string;
  email?: string | null;
  routeId: number;
  vehicleId: number;
  pickupTime: string;
  passengerCount: number;
  isRoundTrip: boolean;
  notes?: string | null;
  paymentPlan: PortalPaymentPlan;
  initialPaymentAmount?: number | null;
}

export interface PortalBookingCardDto {
  id: number;
  bookingNumber: string;
  routeLabel: string;
  pickupTime: string;
  bookingStatus: number;
  totalAmount: number;
  paidAmount: number;
  remaining: number;
  payState: PortalPayState;
}

export interface PortalPaymentLineDto {
  id: number;
  amount: number;
  status: number;
  paymentDate: string;
  paymentMethod: string;
}

export interface PortalBookingDetailDto {
  id: number;
  bookingNumber: string;
  routeLabel: string;
  pickupTime: string;
  passengerCount: number;
  vehicleName: string | null;
  bookingStatus: number;
  totalAmount: number;
  paidAmount: number;
  remaining: number;
  payState: PortalPayState;
  payments: PortalPaymentLineDto[];
}

export interface CreatePortalPaymentPayload {
  phone: string;
  amount: number;
  paymentMethod: string;
  transactionReference?: string | null;
  notes?: string | null;
}
