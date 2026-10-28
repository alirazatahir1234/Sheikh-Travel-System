export type RentalRateType = 'Hourly' | 'Daily' | 'Weekly' | 'Monthly';
export type RentalStatus = 'Draft' | 'Confirmed' | 'PickedUp' | 'Returned' | 'Closed' | 'Cancelled';

export interface RentalRate {
  id: number;
  vehicleId?: number | null;
  vehicleName?: string | null;
  registrationNumber?: string | null;
  rateType: RentalRateType | string;
  amount: number;
  depositAmount: number;
  currency: string;
  isActive: boolean;
  notes?: string | null;
}

export interface CreateRentalRateRequest {
  vehicleId?: number | null;
  rateType: string;
  amount: number;
  depositAmount?: number;
  currency?: string;
  notes?: string | null;
}

export interface UpdateRentalRateRequest {
  vehicleId?: number | null;
  rateType: string;
  amount: number;
  depositAmount: number;
  currency: string;
  isActive: boolean;
  notes?: string | null;
}

export interface RentalBookingListItem {
  id: number;
  rentalNumber: string;
  customerId: number;
  customerName?: string | null;
  vehicleId: number;
  vehicleName?: string | null;
  registrationNumber?: string | null;
  rateType: string;
  startAt: string;
  endAt: string;
  totalAmount: number;
  depositAmount: number;
  status: RentalStatus | string;
  depositStatus: string;
  paymentStatus: string;
}

export interface RentalBookingDetail extends RentalBookingListItem {
  customerPhone?: string | null;
  vehicleCategory?: string | null;
  rateId?: number | null;
  rateAmount: number;
  pickupLocation?: string | null;
  returnLocation?: string | null;
  discountAmount: number;
  depositHeld: number;
  depositRefunded: number;
  damageCharges: number;
  damageNotes?: string | null;
  paidAmount: number;
  paymentMethod?: string | null;
  paymentReference?: string | null;
  linkedPaymentId?: number | null;
  pickedUpAt?: string | null;
  pickupOdometer?: number | null;
  pickupFuelLevel?: string | null;
  pickupNotes?: string | null;
  returnedAt?: string | null;
  returnOdometer?: number | null;
  returnFuelLevel?: string | null;
  returnNotes?: string | null;
  agreementText?: string | null;
  agreementSignedAt?: string | null;
  agreementSignedBy?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface CreateRentalBookingRequest {
  customerId: number;
  vehicleId: number;
  rateId?: number | null;
  rateType: string;
  startAt: string;
  endAt: string;
  pickupLocation?: string | null;
  returnLocation?: string | null;
  discountAmount?: number | null;
  totalAmount?: number | null;
  depositAmount?: number | null;
  notes?: string | null;
  agreementText?: string | null;
}

export interface RentalAvailability {
  vehicleId: number;
  vehicleName?: string | null;
  registrationNumber?: string | null;
  from: string;
  to: string;
  isAvailable: boolean;
  conflicts: Array<{
    rentalBookingId: number;
    rentalNumber: string;
    status: string;
    startAt: string;
    endAt: string;
    customerName?: string | null;
  }>;
}

export interface RentalQuotation {
  vehicleId: number;
  rateType: string;
  rateAmount: number;
  billableUnits: number;
  baseCharge: number;
  discountAmount: number;
  depositAmount: number;
  estimatedTotal: number;
  currency: string;
}

export interface RentalPayment {
  id: number;
  rentalBookingId: number;
  amount: number;
  paymentMethod: string;
  status: string;
  paymentDate: string;
  transactionReference?: string | null;
  notes?: string | null;
}

export interface RentalBookingFilter {
  status?: string | null;
  vehicleId?: number | null;
  customerId?: number | null;
}
