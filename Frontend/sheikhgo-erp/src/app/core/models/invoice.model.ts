export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Void';

export interface Invoice {
  id: number;
  bookingId: number;
  customerId: number;
  invoiceNumber: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  status: InvoiceStatus;
  issuedAt?: string | null;
  customerName?: string | null;
  bookingNumber?: string | null;
  createdAt: string;
}

export interface InvoiceDetail extends Invoice {
  routeName?: string | null;
  notes?: string | null;
  updatedAt?: string | null;
}

export interface InvoiceFilter {
  status?: string | null;
  bookingId?: number | null;
  customerId?: number | null;
}
