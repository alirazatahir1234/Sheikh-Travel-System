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
  estimatedDurationMinutes?: number | null;
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
  routeId?: number | null;
  vehicleId: number;
  pickupTime: string;
  passengerCount: number;
  isRoundTrip: boolean;
  notes?: string | null;
  paymentPlan: PortalPaymentPlan;
  initialPaymentAmount?: number | null;
  preferredPaymentMethod?: string | null;
  pickupAddress?: string | null;
  dropoffAddress?: string | null;
  pickupLat?: number | null;
  pickupLng?: number | null;
  dropLat?: number | null;
  dropLng?: number | null;
  quotedDistanceKm?: number | null;
  quotedDurationMinutes?: number | null;
  adultCount?: number | null;
  childCount?: number | null;
  luggageCount?: number | null;
  promoCode?: string | null;
  seatLabels?: string[] | null;
}

export interface PortalQuoteResultDto {
  priceBreakdown: PriceBreakdown;
  distanceKm: number;
  durationMinutes: number;
  matchedRouteLabel?: string | null;
}

export interface PortalPointToPointQuotePayload {
  vehicleId: number;
  pickupLat: number;
  pickupLng: number;
  dropLat: number;
  dropLng: number;
  isRoundTrip: boolean;
  routeId?: number | null;
}

export interface PortalPromoResultDto {
  valid: boolean;
  code: string;
  discountAmount: number;
  message: string;
}

export interface PortalSavedAddressDto {
  id: number;
  label: string;
  addressLine: string;
  latitude: number;
  longitude: number;
}

export interface PortalCustomerNotificationDto {
  id: number;
  title: string;
  message: string;
  notificationType: string;
  bookingId: number | null;
  isRead: boolean;
  createdAt: string;
}

export interface PortalDriverPreviewDto {
  fullName: string | null;
  rating: number | null;
  yearsExperience: number | null;
  isVerified: boolean;
}

export interface PortalSeatLayoutDto {
  seatLabel: string;
  rowIndex: number;
  colIndex: number;
  isBooked: boolean;
}

export interface PortalLoyaltyDto {
  points: number;
  tier: string;
}

export interface PortalWalletDto {
  balance: number;
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
  pickupAddress?: string | null;
  dropoffAddress?: string | null;
  driver?: PortalDriverPreviewDto | null;
  seats?: string[] | null;
}

export interface CreatePortalPaymentPayload {
  amount: number;
  paymentMethod: string;
  transactionReference?: string | null;
  notes?: string | null;
}

export interface PortalOtpSentDto {
  phone: string;
  devMode: boolean;
  message: string;
}

export interface PortalAuthResultDto {
  phone: string;
  fullName: string;
  accessToken: string;
}

export interface PortalBookingTrackingDto {
  bookingId: number;
  vehicleId: number | null;
  vehicleName: string | null;
  bookingStatus: number;
  trackingAvailable: boolean;
  driverLatitude: number | null;
  driverLongitude: number | null;
  pickupLatitude: number | null;
  pickupLongitude: number | null;
  distanceKm: number | null;
  etaMinutes: number | null;
  speedKmh?: number | null;
  lastUpdatedUtc?: string | null;
  driverPhoneMasked?: string | null;
}

export interface PortalNotificationPreferencesDto {
  smsEnabled: boolean;
  emailEnabled: boolean;
  email: string | null;
}

export interface PortalPaymentGatewayInfoDto {
  enabled: boolean;
  provider: string;
  message: string;
}
