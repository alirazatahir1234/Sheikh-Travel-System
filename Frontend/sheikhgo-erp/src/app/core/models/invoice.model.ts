export type InvoiceStoredStatus = 'Draft' | 'Pending' | 'Paid' | 'Void' | 'Overdue' | 'Issued';
export type InvoicePaymentStatus = 'Pending' | 'Paid' | 'Overdue';

export interface Invoice {
  id: number;
  bookingId?: number | null;
  tripId?: number | null;
  customerId: number;
  invoiceNumber: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  status: InvoiceStoredStatus;
  paymentStatus: InvoicePaymentStatus;
  issuedAt?: string | null;
  dueDate?: string | null;
  customerName?: string | null;
  customerEmail?: string | null;
  bookingNumber?: string | null;
  tripNumber?: string | null;
  createdAt: string;
  lastEmailedAt?: string | null;
}

export interface InvoiceDetail extends Invoice {
  routeName?: string | null;
  notes?: string | null;
  updatedAt?: string | null;
  emailSentCount: number;
}

export interface InvoiceFilter {
  status?: string | null;
  paymentStatus?: string | null;
  bookingId?: number | null;
  tripId?: number | null;
  customerId?: number | null;
}

export interface InvoiceBillingSummary {
  totalBilled: number;
  totalCollected: number;
  outstanding: number;
  pendingCount: number;
  overdueCount: number;
  paidCount: number;
}
