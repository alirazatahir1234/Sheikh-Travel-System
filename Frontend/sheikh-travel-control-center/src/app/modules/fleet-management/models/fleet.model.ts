export interface FleetDashboardSummary {
  totalVehicles: number;
  activeVehicles: number;
  driversOnDuty: number;
  maintenanceDue: number;
  monthlyFuelCost: number;
  complianceAlerts: number;
}

export interface ComplianceDocument {
  id: number;
  entityType: 'Vehicle' | 'Driver' | string;
  entityName: string;
  documentType: string;
  documentNumber?: string;
  issuedDate?: string;
  expiryDate?: string;
  status?: string;
  fileUrl?: string;
}

export interface InspectionRow {
  id: number;
  vehicleName: string;
  inspectedBy?: string;
  inspectionDate: string;
  result: 'Pass' | 'Warning' | 'Fail' | string;
  odometerReading?: number;
}

export interface AssignmentRow {
  id: number;
  vehicleName: string;
  driverName?: string;
  assignmentType: string;
  status: string;
  startAt: string;
  endAt?: string;
}

export interface FleetNavItem {
  id: string;
  label: string;
  icon: string;
  route: string;
}
