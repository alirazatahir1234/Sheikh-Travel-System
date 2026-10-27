export interface DriverWalletPolicy {
  minimumCashOutAmountPkr: number;
  processingFeePercent: number;
  currencyCode: string;
}

export interface DriverWalletPayoutDetails {
  bankAccountName?: string | null;
  bankAccountNumber?: string | null;
  bankName?: string | null;
  jazzCashNumber?: string | null;
  easypaisaNumber?: string | null;
}

export interface DriverWallet {
  driverId: number;
  driverName?: string | null;
  balance: number;
  currency: string;
  cashOutCadence: string;
  payoutDetails: DriverWalletPayoutDetails;
}

export interface DriverCashOutPreview {
  grossAmount: number;
  processingFee: number;
  netPayout: number;
  processingFeePercent: number;
  minimumCashOutAmountPkr: number;
  currencyCode: string;
}

export interface DriverWalletLedgerEntry {
  id: number;
  entryType: string;
  amount: number;
  balanceAfter: number;
  referenceType?: string | null;
  referenceId?: number | null;
  description?: string | null;
  createdAt: string;
}

export interface DriverCashOutRequest {
  id: number;
  driverId: number;
  driverName?: string | null;
  grossAmount: number;
  processingFee: number;
  netPayout: number;
  processingFeeRate: number;
  method: string;
  destinationSnapshot?: string | null;
  status: string;
  requestedAt: string;
  processedAt?: string | null;
  paymentReference?: string | null;
  rejectionReason?: string | null;
}

export type CashOutMethod = 'BankTransfer' | 'JazzCash' | 'EasyPaisa';
