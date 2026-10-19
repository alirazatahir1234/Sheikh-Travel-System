export type SettlementStatus = 'Draft' | 'Approved' | 'Paid';

export interface Settlement {
  id: number;
  driverId: number;
  driverName?: string | null;
  periodStart: string;
  periodEnd: string;
  grossCollected: number;
  fuelDeducted: number;
  allowanceAmount: number;
  adjustments: number;
  payable: number;
  status: SettlementStatus;
  paidAt?: string | null;
  reference?: string | null;
  createdAt: string;
}

export interface SettlementLine {
  id: number;
  lineType: string;
  description: string;
  amount: number;
  referenceId?: number | null;
}

export interface SettlementDetail extends Settlement {
  notes?: string | null;
  updatedAt?: string | null;
  lines: SettlementLine[];
}

export interface SettlementFilter {
  driverId?: number | null;
  status?: string | null;
}

export interface GenerateDraftSettlementRequest {
  driverId: number;
  periodStart: string;
  periodEnd: string;
  adjustments?: number;
}
