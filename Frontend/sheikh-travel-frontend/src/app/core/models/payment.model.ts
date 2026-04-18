export type PaymentStatus = 'Pending' | 'PartiallyPaid' | 'Paid' | 'Refunded';
export type PaymentMethod = 'Cash' | 'Card' | 'BankTransfer' | 'Online';

export interface Payment {
  id: number;
  bookingId: number;
  bookingNumber: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  referenceNumber?: string;
  paidAt: string;
  createdAt: string;
}

export interface CreatePaymentRequest {
  bookingId: number;
  amount: number;
  paymentMethod: PaymentMethod;
  referenceNumber?: string;
}
