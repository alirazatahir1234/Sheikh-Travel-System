export interface Tenant {
  id: number;
  name: string;
  slug: string;
  code?: string | null;
  tenantType?: string | null;
  country?: string | null;
  subscriptionPlan?: string | null;
  isActive: boolean;
  branchCount: number;
  departmentCount: number;
  roleCount: number;
  location?: string | null;
  createdAt: string;
  activeUserCount: number;
  maxUsers?: number | null;
  activeVehicleCount: number;
  maxVehicles?: number | null;
  moduleCodes?: string | null;
  subscriptionEndDate?: string | null;
  subscriptionStatus?: string | null;
}

export type TenantHealthStatus = 'Healthy' | 'Trial Ending' | 'Expired' | 'Suspended';

export interface TenantAdminInfo {
  id: number;
  fullName: string;
  email: string;
  phone?: string | null;
  isActive: boolean;
  status: string;
}

export interface TenantDetail extends Omit<Tenant, 'moduleCodes'> {
  tenantType?: string | null;
  industryType?: string | null;
  storageModel: string;
  status: string;
  dataRegion?: string | null;
  updatedAt?: string | null;
  maxUsers?: number | null;
  maxVehicles?: number | null;
  maxDrivers?: number | null;
  maxBranches?: number | null;
  maxGpsDevices?: number | null;
  moduleCodes: string[];
  logoUrl?: string | null;
  primaryColor?: string | null;
  website?: string | null;
  supportEmail?: string | null;
  country?: string | null;
  currencyCode?: string | null;
  timeZone?: string | null;
  adminInfo?: TenantAdminInfo | null;
}

export interface UpdateTenantPayload {
  name: string;
  subscriptionPlan?: string | null;
  isActive: boolean;
  moduleCodes?: string[];
  maxUsers?: number | null;
  maxVehicles?: number | null;
  maxDrivers?: number | null;
  maxBranches?: number | null;
  maxGpsDevices?: number | null;
}

export interface UpdateTenantBrandingPayload {
  logoUrl?: string | null;
  primaryColor?: string | null;
  website?: string | null;
  supportEmail?: string | null;
  country?: string | null;
  currencyCode?: string | null;
  timeZone?: string | null;
}

export interface TenantManagementStats {
  activeTenants: number;
  activeUsers: number;
  activeVehicles: number;
  expiringPlans: number;
  monthlyRevenue: number;
  tenantsAddedThisMonth: number;
}

export interface TenantModuleDefinition {
  code: string;
  name: string;
  legacyKeys: string[];
}

export const DEFAULT_TENANT_MODULE_CODES = [
  'DASHBOARD', 'FLEET', 'GPS', 'TRAVEL', 'CRM', 'FINANCE', 'ANALYTICS', 'ACCESS'
] as const;

export const TENANT_PLAN_TIERS = ['Enterprise', 'Pro', 'Starter'] as const;

export interface PlanQuotas {
  maxUsers: number | null;
  maxVehicles: number | null;
  maxDrivers: number | null;
  maxBranches: number | null;
  maxGpsDevices: number | null;
}

export interface PlanDefinition {
  quotas: PlanQuotas;
  moduleCodes: string[];
  description: string;
  bestFor: string;
}

export const PLAN_DEFINITIONS: Record<string, PlanDefinition> = {
  Starter: {
    description: 'Core fleet & GPS operations',
    bestFor: 'Small companies',
    quotas: { maxUsers: 10, maxVehicles: 50, maxDrivers: 50, maxBranches: 5, maxGpsDevices: 50 },
    moduleCodes: ['DASHBOARD', 'FLEET', 'GPS', 'ACCESS']
  },
  Pro: {
    description: 'Full operations suite',
    bestFor: 'Growing businesses',
    quotas: { maxUsers: 50, maxVehicles: 250, maxDrivers: 250, maxBranches: 15, maxGpsDevices: 250 },
    moduleCodes: ['DASHBOARD', 'FLEET', 'GPS', 'RENTAL', 'CRM', 'FINANCE', 'ANALYTICS', 'ACCESS']
  },
  Enterprise: {
    description: 'Unlimited — all modules',
    bestFor: 'Large organizations',
    quotas: { maxUsers: null, maxVehicles: null, maxDrivers: null, maxBranches: null, maxGpsDevices: null },
    moduleCodes: ['DASHBOARD', 'FLEET', 'GPS', 'RENTAL', 'TRAVEL', 'CRM', 'FINANCE', 'HR', 'ANALYTICS', 'ACCESS']
  }
};

export function applyPlanDefaults(planName: string): PlanDefinition {
  return PLAN_DEFINITIONS[planName] ?? PLAN_DEFINITIONS['Enterprise'];
}

export const TENANT_TYPES = [
  'Travel Agency',
  'Logistics Company',
  'Fleet Operator',
  'Corporate Transport'
] as const;

export const INDUSTRY_TYPES = [
  'Logistics & Transport',
  'Travel & Tourism',
  'Corporate Mobility',
  'Public Transit'
] as const;

export const STORAGE_MODELS = [
  { value: 'SharedDatabase', label: 'Shared Database (Default)' },
  { value: 'DedicatedDatabase', label: 'Dedicated Database' }
] as const;

export const GPS_PROVIDERS = ['Teltonika', 'Queclink', 'Concox', 'Ruptela', 'Other'] as const;

export const MODULE_ICONS: Record<string, string> = {
  DASHBOARD: 'dashboard',
  FLEET: 'local_shipping',
  GPS: 'my_location',
  RENTAL: 'car_rental',
  TRAVEL: 'flight',
  CRM: 'groups',
  FINANCE: 'account_balance_wallet',
  HR: 'badge',
  ANALYTICS: 'bar_chart',
  ACCESS: 'admin_panel_settings'
};

export interface ProvisionTenantRequest {
  name: string;
  slug: string;
  adminEmail: string;
  adminPassword: string;
  adminFullName: string;
  code?: string;
  tenantType?: string;
  industryType?: string;
  storageModel?: string;
  status?: string;
  dataRegion?: string;
  planName?: string;
  maxUsers?: number;
  maxVehicles?: number;
  maxDrivers?: number;
  maxBranches?: number;
  maxGpsDevices?: number;
  moduleCodes?: string[];
  adminMobile?: string;
  country?: string;
  currencyCode?: string;
  timeZone?: string;
  primaryColor?: string;
  logoUrl?: string;
  website?: string;
  supportEmail?: string;
  isMfaRequired?: boolean;
  passwordExpiryDays?: number;
  sessionTimeoutMinutes?: number;
  isGdprEnabled?: boolean;
  isAuditLoggingEnabled?: boolean;
  isVatEnabled?: boolean;
  generateOrganizationStructure?: boolean;
  headOfficeName?: string;
  defaultBranchName?: string;
  defaultDepartments?: string;
  billingContactName?: string;
  billingEmail?: string;
  billingAddress?: string;
  companyTRN?: string;
  gpsProviderName?: string;
}

export type TenantPlanTier = 'Enterprise' | 'Pro' | 'Starter' | string;

export interface TenantPlanMeta {
  label: string;
  cssClass: string;
  icon: string;
}

export function tenantPlanMeta(plan?: string | null): TenantPlanMeta {
  const normalized = (plan ?? 'Starter').toLowerCase();
  if (normalized.includes('enterprise')) {
    return { label: 'Enterprise', cssClass: 'plan-enterprise', icon: 'workspace_premium' };
  }
  if (normalized.includes('pro')) {
    return { label: 'Pro', cssClass: 'plan-pro', icon: 'bolt' };
  }
  return { label: plan?.trim() || 'Starter', cssClass: 'plan-starter', icon: 'shuffle' };
}

export function tenantDisplayCode(tenant: Pick<Tenant, 'id' | 'code'>): string {
  return tenant.code?.trim() || `SK-${String(tenant.id).padStart(4, '0')}`;
}

export function tenantModuleList(tenant: Pick<Tenant, 'moduleCodes'>): string[] {
  return (tenant.moduleCodes ?? '')
    .split(',')
    .map(c => c.trim())
    .filter(Boolean);
}

export function tenantUsageLabel(tenant: Tenant): string {
  const used = tenant.activeUserCount ?? 0;
  const max = tenant.maxUsers;
  return max && max > 0 ? `${used}/${max}` : `${used}`;
}

export function tenantUsagePercent(tenant: Tenant): number {
  const max = tenant.maxUsers;
  if (!max || max <= 0) return 0;
  return Math.min(100, Math.round(((tenant.activeUserCount ?? 0) / max) * 100));
}

export function tenantHealthStatus(tenant: Tenant): TenantHealthStatus {
  if (!tenant.isActive) return 'Suspended';
  if (tenant.subscriptionEndDate) {
    const end = new Date(tenant.subscriptionEndDate);
    const now = new Date();
    if (end < now) return 'Expired';
    const days = (end.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
    if (days <= 30) return 'Trial Ending';
  }
  const status = (tenant.subscriptionStatus ?? '').toLowerCase();
  if (status.includes('expired')) return 'Expired';
  if (status.includes('trial')) return 'Trial Ending';
  return 'Healthy';
}

export function formatModuleBadge(code: string): string {
  const map: Record<string, string> = {
    DASHBOARD: 'Dashboard',
    FLEET: 'Fleet',
    GPS: 'GPS',
    TRAVEL: 'Travel',
    CRM: 'CRM',
    FINANCE: 'Finance',
    ANALYTICS: 'Analytics',
    ACCESS: 'Access',
    HR: 'HR',
    RENTAL: 'Rental'
  };
  return map[code] ?? code;
}

export enum BranchStatus {
  Active = 1,
  Inactive = 2,
  Maintenance = 3,
  Closed = 4
}

export interface Branch {
  id: number;
  tenantId: number;
  parentBranchId?: number | null;
  branchCode: string;
  name: string;
  branchType?: string | null;
  branchManagerUserId?: number | null;
  branchManagerName?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  country?: string | null;
  timeZone?: string | null;
  currencyCode?: string | null;
  status: BranchStatus;
  isGpsEnabled: boolean;
  isActive: boolean;
}

export interface BranchPayload {
  branchCode: string;
  name: string;
  branchType?: string | null;
  parentBranchId?: number | null;
  branchManagerUserId?: number | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  country?: string | null;
  timeZone?: string | null;
  currencyCode?: string | null;
  status: BranchStatus;
  isGpsEnabled: boolean;
}

export interface Department {
  id: number;
  tenantId: number;
  name: string;
  departmentHeadUserId?: number | null;
  departmentHeadName?: string | null;
  staffCount: number;
  isActive: boolean;
  createdAt: string;
}

export interface DepartmentPayload {
  name: string;
  departmentHeadUserId?: number | null;
}

export interface PlatformRole {
  id: number;
  tenantId: number;
  name: string;
  code: string;
  isSystem: boolean;
  isActive: boolean;
  permissions: string[];
}

export interface Permission {
  id: number;
  moduleName: string;
  permissionCode: string;
  description?: string | null;
}

export const BRANCH_TYPES = ['Hub', 'Office', 'Depot', 'Warehouse'] as const;

export const BRANCH_COUNTRIES = [
  'United Arab Emirates',
  'Pakistan',
  'Saudi Arabia',
  'Qatar',
  'Oman',
  'Bahrain',
  'Kuwait'
] as const;

export const BRANCH_TIMEZONES = [
  'Asia/Dubai',
  'Asia/Karachi',
  'Asia/Riyadh',
  'Asia/Qatar',
  'Asia/Muscat',
  'Asia/Bahrain',
  'Asia/Kuwait'
] as const;

export const DEFAULT_CURRENCY = 'PKR';

export const BRANCH_CURRENCIES = ['PKR', 'AED', 'SAR', 'QAR', 'OMR', 'BHD', 'KWD', 'USD'] as const;

export function formatCurrency(amount?: number | null, currency = DEFAULT_CURRENCY): string {
  const value = Number(amount ?? 0);
  if (!Number.isFinite(value)) return `${currency} 0`;
  if (value >= 1_000_000) return `${currency} ${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1000) return `${currency} ${Math.round(value / 1000)}k`;
  return `${currency} ${value.toLocaleString('en-PK')}`;
}

/** Normalizes API stats from current or legacy management-stats payloads. */
export function normalizeTenantManagementStats(raw: Record<string, unknown> | null | undefined): TenantManagementStats {
  const n = (key: string, fallback = 0) => {
    const v = raw?.[key];
    return typeof v === 'number' && Number.isFinite(v) ? v : fallback;
  };

  if (raw && 'activeTenants' in raw) {
    return {
      activeTenants: n('activeTenants'),
      activeUsers: n('activeUsers'),
      activeVehicles: n('activeVehicles'),
      expiringPlans: n('expiringPlans'),
      monthlyRevenue: n('monthlyRevenue'),
      tenantsAddedThisMonth: n('tenantsAddedThisMonth')
    };
  }

  return {
    activeTenants: n('totalTenants'),
    activeUsers: 0,
    activeVehicles: 0,
    expiringPlans: 0,
    monthlyRevenue: 0,
    tenantsAddedThisMonth: n('tenantsAddedThisMonth')
  };
}

// Organization Designer Types
export interface OrganizationTree {
  tenantId: number;
  tenantName: string;
  branches: OrganizationBranch[];
  unassignedDepartments: OrganizationDepartment[];
}

export interface OrganizationBranch {
  id: number;
  parentBranchId?: number | null;
  branchCode: string;
  name: string;
  branchType?: string | null;
  city?: string | null;
  country?: string | null;
  isActive: boolean;
  status: number;
  departments: OrganizationDepartment[];
}

export interface OrganizationDepartment {
  id: number;
  branchId?: number | null;
  name: string;
  departmentHeadName?: string | null;
  staffCount: number;
  isActive: boolean;
}

export interface DepartmentPayloadWithBranch {
  name: string;
  departmentHeadUserId?: number | null;
  branchId?: number | null;
}

export interface RoleSummary {
  id: number;
  tenantId: number;
  name: string;
  code: string;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
  permissions: string[];
}

export interface TenantSecuritySettings {
  isMfaRequired: boolean;
  passwordExpiryDays?: number | null;
  sessionTimeoutMinutes?: number | null;
  isGdprEnabled: boolean;
  isAuditLoggingEnabled: boolean;
  isVatEnabled: boolean;
}

export interface RoleTemplate {
  code: string;
  name: string;
  permissionCount: number;
  permissions: string[];
}

export interface ModuleStatus {
  code: string;
  name: string;
  isEnabled: boolean;
}

export interface LicenseLimit {
  resource: string;
  used: number;
  limit?: number | null;
}

export interface TenantModuleOverview {
  tenantId: number;
  tenantName: string;
  planName?: string | null;
  modules: ModuleStatus[];
  licenseLimits: LicenseLimit[];
}

export interface SubscriptionDetail {
  tenantId: number;
  tenantName: string;
  planName?: string | null;
  status: string;
  billingCycle: string;
  monthlyAmount?: number | null;
  currencyCode: string;
  autoRenew: boolean;
  subscriptionStartDate?: string | null;
  subscriptionEndDate?: string | null;
  trialEndDate?: string | null;
  maxUsers?: number | null;
  maxVehicles?: number | null;
  maxDrivers?: number | null;
  maxBranches?: number | null;
  maxGpsDevices?: number | null;
}

export interface Invoice {
  id: number;
  invoiceNumber: string;
  planName?: string | null;
  amount: number;
  currencyCode: string;
  status: string;
  issuedDate: string;
  dueDate?: string | null;
  paidDate?: string | null;
}

export interface Payment {
  id: number;
  invoiceId?: number | null;
  amount: number;
  currencyCode: string;
  paymentMethod?: string | null;
  status: string;
  reference?: string | null;
  paidAt: string;
}

export interface SubscriptionOverview {
  subscription: SubscriptionDetail;
  invoices: Invoice[];
  payments: Payment[];
}

export type SubscriptionAction = 'Upgrade' | 'Renew' | 'Suspend' | 'Cancel' | 'Reactivate';

export interface UpdateSubscriptionRequest {
  action: SubscriptionAction;
  planName?: string | null;
  monthlyAmount?: number | null;
  autoRenew?: boolean | null;
  billingCycle?: string | null;
}

export const BRANCH_STATUS_OPTIONS: { value: BranchStatus; label: string; hint: string }[] = [
  { value: BranchStatus.Active, label: 'Active', hint: 'Visible in all systems' },
  { value: BranchStatus.Inactive, label: 'Inactive', hint: 'Archived status' },
  { value: BranchStatus.Maintenance, label: 'Under Maintenance', hint: 'Temporarily unavailable' },
  { value: BranchStatus.Closed, label: 'Closed', hint: 'Permanently closed' }
];

export function branchStatusLabel(status: BranchStatus): string {
  return BRANCH_STATUS_OPTIONS.find(o => o.value === status)?.label ?? 'Unknown';
}
