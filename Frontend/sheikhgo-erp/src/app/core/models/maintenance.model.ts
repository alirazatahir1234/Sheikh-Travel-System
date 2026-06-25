export enum MaintenanceStatus {
  Scheduled   = 1,
  InProgress  = 2,
  Completed   = 3
}

export const MaintenanceStatusLabels: Record<MaintenanceStatus, string> = {
  [MaintenanceStatus.Scheduled]:  'Scheduled',
  [MaintenanceStatus.InProgress]: 'In Progress',
  [MaintenanceStatus.Completed]:  'Completed'
};

export interface Maintenance {
  id: number;
  vehicleId: number;
  description: string;
  cost: number;
  maintenanceDate: string;
  nextDueDate?: string | null;
  status: MaintenanceStatus;
  serviceProvider?: string | null;
  createdAt: string;

  // Joined fields
  vehicleName?: string;
  vehicleRegistration?: string;
}

export interface CreateMaintenanceDto {
  vehicleId: number;
  description: string;
  cost: number;
  maintenanceDate: string;
  nextDueDate?: string | null;
  serviceProvider?: string | null;
}

export interface CreateMaintenanceRequest {
  maintenance: CreateMaintenanceDto;
}

export interface UpdateMaintenanceStatusRequest {
  id: number;
  status: MaintenanceStatus;
}

// ── Fleet Maintenance Module ──────────────────────────────────────────────────

export type WorkOrderStatus =
  | 'Draft' | 'Open' | 'Assigned' | 'InProgress' | 'WaitingParts' | 'Completed' | 'Closed' | 'Cancelled';

export const WorkOrderStatusLabels: Record<WorkOrderStatus, string> = {
  Draft: 'Draft',
  Open: 'Open',
  Assigned: 'Assigned',
  InProgress: 'In Progress',
  WaitingParts: 'Waiting Parts',
  Completed: 'Completed',
  Closed: 'Closed',
  Cancelled: 'Cancelled'
};

export const WORK_ORDER_WORKFLOW_STEPS = [
  'Draft', 'Open', 'Assigned', 'InProgress', 'Completed', 'Closed'
] as const;

export type MaintenanceType = 'Preventive' | 'Corrective' | 'Emergency';

export type RequestPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const ISSUE_CATEGORIES = [
  'Engine', 'Transmission', 'Brake', 'Tire', 'Electrical', 'Battery',
  'AC', 'Body Damage', 'Inspection', 'Oil Change', 'Breakdown', 'Other'
] as const;

export interface WorkOrderStats {
  open: number;
  inProgress: number;
  completed: number;
  cancelled: number;
}

export interface TechnicianListItem {
  id: number;
  fullName: string;
  workshopId?: number | null;
  workshopName?: string | null;
}

export interface MaintenanceKpis {
  totalVehicles: number;
  dueForService: number;
  underMaintenance: number;
  overdueServices: number;
  monthlyMaintenanceCost: number;
  activeWorkOrders: number;
  pendingRequests: number;
}

export interface MaintenanceCostTrendPoint {
  label: string;
  preventiveCost: number;
  correctiveCost: number;
  breakdownCost: number;
}

export interface VehicleHealthSummary {
  healthy: number;
  serviceDueSoon: number;
  overdue: number;
  inWorkshop: number;
}

export interface MaintenanceAlert {
  id: number;
  vehicleId?: number | null;
  vehicleName?: string | null;
  vehicleRegistration?: string | null;
  alertType: string;
  severity: string;
  title: string;
  message: string;
  createdAt: string;
}

export interface UpcomingService {
  scheduleId?: number | null;
  vehicleId: number;
  vehicleName: string;
  vehicleRegistration?: string | null;
  serviceType: string;
  dueDate?: string | null;
  dueMileage?: number | null;
  priority: string;
}

export interface MaintenanceDashboard {
  kpis: MaintenanceKpis;
  costTrend: MaintenanceCostTrendPoint[];
  vehicleHealth: VehicleHealthSummary;
  criticalAlerts: MaintenanceAlert[];
  recentWorkOrders: WorkOrderListItem[];
  upcomingServices: UpcomingService[];
  fuelSummary?: FuelMaintenanceSummary | null;
}

export interface FuelMaintenanceSummary {
  labels: string[];
  fuelCosts: number[];
  maintenanceCosts: number[];
  highCostVehicles: HighCostVehicle[];
}

export interface HighCostVehicle {
  vehicleId: number;
  vehicleName: string;
  fuelCost: number;
  maintenanceCost: number;
}

export interface MaintenanceRequest {
  id: number;
  requestNumber: string;
  vehicleId: number;
  vehicleName?: string | null;
  vehicleRegistration?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  requestDate: string;
  requestType: string;
  priority: string;
  issueCategory: string;
  description: string;
  breakdownLocation?: string | null;
  driverRemarks?: string | null;
  status: string;
  workOrderId?: number | null;
  createdAt: string;
  photosJson?: string | null;
  documentsJson?: string | null;
  branchName?: string | null;
  departmentName?: string | null;
  rejectionReason?: string | null;
  approvedBy?: string | null;
  approvedAt?: string | null;
  rejectedBy?: string | null;
  rejectedAt?: string | null;
}

export interface MaintenanceRequestStats {
  open: number;
  approved: number;
  inProgress: number;
  pendingApproval: number;
}

export interface MaintenanceSearchResult {
  entityType: string;
  id: number;
  title: string;
  subtitle: string;
  routeHint?: string | null;
}

export interface ComplianceSummary {
  expired: number;
  expiring7Days: number;
  expiring15Days: number;
  expiring30Days: number;
}

export interface WorkOrderPartUsage {
  partId: number;
  partName: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
}

export interface CreateMaintenanceRequestPayload {
  vehicleId: number;
  driverId?: number | null;
  requestType: string;
  priority: string;
  issueCategory: string;
  description: string;
  breakdownLocation?: string | null;
  driverRemarks?: string | null;
}

export interface WorkOrderListItem {
  id: number;
  workOrderNumber: string;
  vehicleId: number;
  vehicleName?: string | null;
  vehicleRegistration?: string | null;
  serviceTypeName?: string | null;
  maintenanceType?: string | null;
  status: string;
  priority?: string | null;
  laborCost: number;
  partsCost: number;
  totalCost: number;
  estimatedLaborCost?: number;
  estimatedPartsCost?: number;
  workshopId?: number | null;
  workshopName?: string | null;
  technicianId?: number | null;
  technicianName?: string | null;
  startDate?: string | null;
  estimatedCompletionDate?: string | null;
  completedAt?: string | null;
  createdAt: string;
}

export interface WorkOrderDetail extends WorkOrderListItem {
  requestId?: number | null;
  serviceTypeId?: number | null;
  driverId?: number | null;
  driverName?: string | null;
  branchId?: number | null;
  branchName?: string | null;
  notes?: string | null;
  technicianNotes?: string | null;
  partsUsage?: WorkOrderPartUsage[];
}

export interface UpdateWorkOrderPayload {
  workshopId?: number | null;
  technicianId?: number | null;
  status?: string | null;
}

export interface CreateWorkOrderPayload {
  vehicleId: number;
  requestId?: number | null;
  workshopId?: number | null;
  technicianId?: number | null;
  serviceTypeId?: number | null;
  serviceTypeName?: string | null;
  maintenanceType?: string | null;
  startDate?: string | null;
  estimatedCompletionDate?: string | null;
  laborCost?: number;
  partsCost?: number;
  priority?: string | null;
  notes?: string | null;
}

export interface Workshop {
  id: number;
  name: string;
  workshopType: string;
  location?: string | null;
  contactPerson?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  capacity?: number | null;
  vendorType?: string | null;
  contractDetails?: string | null;
  sla?: string | null;
  rating?: number | null;
  isActive: boolean;
  activeTechnicians: number;
}

export interface CreateWorkshopPayload {
  name: string;
  workshopType: string;
  location?: string | null;
  contactPerson?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  capacity?: number | null;
  vendorType?: string | null;
  contractDetails?: string | null;
  sla?: string | null;
  rating?: number | null;
}

export interface UpdateWorkshopPayload extends Partial<CreateWorkshopPayload> {
  isActive?: boolean;
}

export interface Vendor {
  id: number;
  name: string;
  category: string;
  contactPerson?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  products: string[];
  rating?: number | null;
  isPreferred: boolean;
  isActive: boolean;
}

export interface CreateVendorPayload {
  name: string;
  category: string;
  contactPerson?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  products?: string[];
  rating?: number | null;
  isPreferred?: boolean;
}

export interface UpdateVendorPayload extends Partial<CreateVendorPayload> {
  isActive?: boolean;
}

export interface WorkshopVendorStats {
  totalWorkshops: number;
  activeWorkshops: number;
  totalVendors: number;
  preferredVendors: number;
}

export interface ServiceType {
  id: number;
  code: string;
  name: string;
  isPreventive: boolean;
}

export interface MaintenanceSchedule {
  id: number;
  vehicleId: number;
  vehicleName?: string | null;
  serviceTypeName: string;
  intervalType: string;
  intervalValue: number;
  lastServiceDate?: string | null;
  lastServiceMileage?: number | null;
  nextDueDate?: string | null;
  nextDueMileage?: number | null;
  priority: string;
  isActive: boolean;
}

export type ScheduleStatus = 'Upcoming' | 'DueSoon' | 'Overdue';

export const ScheduleStatusLabels: Record<ScheduleStatus, string> = {
  Upcoming: 'Upcoming',
  DueSoon: 'Due Soon',
  Overdue: 'Overdue'
};

export interface MaintenanceScheduleListItem {
  id: number;
  vehicleId: number;
  vehicleName?: string | null;
  vehicleRegistration?: string | null;
  currentMileage: number;
  nextServiceMileage?: number | null;
  dueDate?: string | null;
  serviceTypeName: string;
  intervalType: string;
  intervalValue: number;
  status: ScheduleStatus;
  priority: string;
  isActive: boolean;
  currentEngineHours?: number | null;
  nextDueEngineHours?: number | null;
  lastServiceMileage?: number | null;
  lastServiceDate?: string | null;
  lastServiceEngineHours?: number | null;
}

export interface MaintenanceScheduleCalendarItem {
  scheduleId: number;
  vehicleId: number;
  vehicleName: string;
  serviceTypeName: string;
  dueDate?: string | null;
  status: ScheduleStatus;
  intervalType: string;
  nextServiceMileage?: number | null;
  nextDueEngineHours?: number | null;
}

export interface MaintenanceScheduleTemplate {
  serviceTypeName: string;
  intervalType: string;
  intervalValue: number;
  description: string;
}

export interface CreateMaintenanceSchedulePayload {
  vehicleId: number;
  serviceTypeId?: number | null;
  serviceTypeName: string;
  intervalType: string;
  intervalValue: number;
  lastServiceDate?: string | null;
  lastServiceMileage?: number | null;
  lastServiceEngineHours?: number | null;
  priority: string;
}

export interface RescheduleMaintenanceSchedulePayload {
  lastServiceDate?: string | null;
  lastServiceMileage?: number | null;
  lastServiceEngineHours?: number | null;
  intervalType?: string | null;
  intervalValue?: number | null;
}

export interface VehicleServiceHistoryItem {
  id: number;
  source: string;
  vehicleId: number;
  vehicleName?: string | null;
  vehicleRegistration?: string | null;
  serviceType: string;
  serviceDate: string;
  workshopName?: string | null;
  technicianName?: string | null;
  totalCost: number;
  laborCost: number;
  partsCost: number;
  invoiceUrl?: string | null;
  notes?: string | null;
  status: string;
}

export type PartStockStatus = 'InStock' | 'LowStock' | 'OutOfStock';

export interface Part {
  id: number;
  partNumber: string;
  partName: string;
  category?: string | null;
  brand?: string | null;
  supplier?: string | null;
  unitCost: number;
  minStockLevel: number;
  stockQuantity: number;
  isLowStock: boolean;
  vehicleCompatibility: string[];
  stockStatus: PartStockStatus;
  isOutOfStock: boolean;
  location?: string | null;
}

export interface PartsInventoryStats {
  totalParts: number;
  lowStock: number;
  outOfStock: number;
  inventoryValue: number;
}

export interface CreatePartPayload {
  partNumber: string;
  partName: string;
  category?: string;
  brand?: string;
  supplier?: string;
  unitCost: number;
  minStockLevel: number;
  initialStock: number;
  vehicleCompatibility?: string[];
  location?: string;
}

export interface AddPartStockPayload {
  quantity: number;
  location?: string;
  notes?: string;
}

export interface IssuePartPayload {
  vehicleId: number;
  quantity: number;
  workOrderId?: number;
  notes?: string;
}

export interface TransferPartStockPayload {
  quantity: number;
  fromLocation: string;
  toLocation: string;
  notes?: string;
}

export type MaintenanceReportType =
  | 'vehicle-maintenance'
  | 'service-due'
  | 'overdue-maintenance'
  | 'workshop-performance'
  | 'vendor-performance'
  | 'cost-analysis'
  | 'breakdown';

export interface MaintenanceReportFilters {
  vehicleId?: number | null;
  branchId?: number | null;
  from?: string;
  to?: string;
  status?: string;
}

export interface MaintenanceReportColumn {
  key: string;
  label: string;
  format: 'text' | 'currency' | 'date' | 'number';
}

export interface MaintenanceReportRow {
  key: string;
  label: string;
  count: number;
  totalCost: number;
  fields: Record<string, unknown>;
}

export interface MaintenanceReport {
  reportType: string;
  title: string;
  columns: MaintenanceReportColumn[];
  rows: MaintenanceReportRow[];
  totalCost: number;
  summary?: Record<string, unknown>;
}

export interface MaintenanceReportSchedule {
  id: number;
  reportType: string;
  filters: MaintenanceReportFilters;
  frequency: string;
  recipients: string;
  nextRunAt?: string | null;
  lastRunAt?: string | null;
  lastRunStatus?: string | null;
  isActive: boolean;
}

export interface CreateMaintenanceReportSchedulePayload {
  reportType: string;
  filters: MaintenanceReportFilters;
  frequency: string;
  recipients: string;
}
