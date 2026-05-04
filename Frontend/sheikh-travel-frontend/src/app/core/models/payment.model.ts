export type PaymentStatus = 'Pending' | 'PartiallyPaid' | 'Paid' | 'Refunded';
export type PaymentMethod = 'Cash' | 'Card' | 'BankTransfer' | 'Online';

export interface Payment {
  id: number;
  bookingId: number;
  amount: number;
  paymentMethod: PaymentMethod;
  status: PaymentStatus;
  transactionReference?: string;
  notes?: string;
  paymentDate: string;
  createdAt: string;
}

export interface PaymentDetail {
  id: number;
  bookingId: number;
  bookingNumber?: string;
  customerName?: string;
  routeName?: string;
  amount: number;
  paymentMethod: PaymentMethod;
  status: PaymentStatus;
  transactionReference?: string;
  notes?: string;
  paymentDate: string;
  createdAt: string;
  totalBookingAmount: number;
}

export interface CreatePaymentRequest {
  bookingId: number;
  amount: number;
  paymentMethod: PaymentMethod;
  transactionReference?: string;
  notes?: string;
}

export interface UpdatePaymentStatusRequest {
  status: PaymentStatus;
}

export interface PaymentFilter {
  bookingId?: number | null;
  status?: PaymentStatus | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

