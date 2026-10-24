export type SmartAlertRuleType =
  | 'MaintenanceDue'
  | 'DocumentExpiry'
  | 'UnassignedTrip'
  | 'OverdueInvoice'
  | 'FuelAnomaly'
  | string;

export type SmartAlertSeverity = 'Info' | 'Warning' | 'Critical' | string;
export type SmartAlertStatus = 'New' | 'Acknowledged' | 'Resolved' | 'Dismissed' | string;

export interface SmartAlert {
  id: number;
  tenantId: number;
  ruleType: SmartAlertRuleType;
  severity: SmartAlertSeverity;
  message: string;
  relatedEntityType?: string | null;
  relatedEntityId?: number | null;
  status: SmartAlertStatus;
  createdAt: string;
  acknowledgedAt?: string | null;
  acknowledgedBy?: string | null;
  resolvedAt?: string | null;
  resolvedBy?: string | null;
  deduplicationKey: string;
}

export interface SmartAlertSummary {
  newCount: number;
  activeCount: number;
  items: SmartAlert[];
}
