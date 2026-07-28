import {
  CreateUserDto,
  User,
  UserRoleLabels,
  EMPLOYEE_TYPES,
  USER_LIFECYCLE_STATUSES
} from '../../core/models/user.model';
import { RoleSummary } from '../../core/models/platform.model';
import { ExportColumn } from '../../core/services/export.service';
import { resolveImportRole, ResolvedImportRole } from './user-import-role-resolver';

/** Canonical import/export headers — keep stable for re-import tooling. */
export const USER_IMPORT_HEADERS = [
  'FullName',
  'Email',
  'Password',
  'Phone',
  'Role',
  'PlatformRoleCode',
  'BranchId',
  'BranchName',
  'DepartmentId',
  'DepartmentName',
  'JobTitle',
  'EmployeeCode',
  'EmployeeType',
  'Status',
  'DefaultWorkspaceKey',
  'DefaultDashboardKey',
  'HomeRoute',
  'TimeZone',
  'Language',
  'Theme'
] as const;

export type UserImportHeader = (typeof USER_IMPORT_HEADERS)[number];

export interface UserImportRow {
  FullName: string;
  Email: string;
  Password: string;
  Phone: string;
  Role: string;
  PlatformRoleCode: string;
  BranchId: string;
  BranchName: string;
  DepartmentId: string;
  DepartmentName: string;
  JobTitle: string;
  EmployeeCode: string;
  EmployeeType: string;
  Status: string;
  DefaultWorkspaceKey: string;
  DefaultDashboardKey: string;
  HomeRoute: string;
  TimeZone: string;
  Language: string;
  Theme: string;
}

export type RowSeverity = 'ready' | 'warning' | 'error';

export interface ImportRowIssue {
  code: string;
  message: string;
  suggestion?: string;
  field?: string;
  severity: 'error' | 'warning';
}

export interface ParsedUserImportRow {
  rowNumber: number;
  dto?: CreateUserDto;
  raw?: Partial<UserImportRow>;
  resolvedRole?: ResolvedImportRole;
  issues: ImportRowIssue[];
  severity: RowSeverity;
  /** @deprecated use issues */
  error?: string;
  rawEmail?: string;
  displayPhone?: string;
  branchStatus?: string;
  departmentStatus?: string;
  passwordPreview?: 'blank' | 'provided' | 'generated';
}

export interface UserImportParseContext {
  branches: { id: number; name: string }[];
  departments: { id: number; name: string }[];
  assignableRoles: RoleSummary[];
  isPlatformSuperAdmin: boolean;
  existingEmails: Set<string>;
  existingPhones: Set<string>;
  existingEmployeeCodes: Set<string>;
}

export const USER_IMPORT_SAMPLE: UserImportRow = {
  FullName: 'Sample User',
  Email: 'sample.user@example.com',
  Password: '',
  Phone: '+971500000000',
  Role: 'Fleet Manager',
  PlatformRoleCode: 'FLEET_MANAGER',
  BranchId: '',
  BranchName: '',
  DepartmentId: '',
  DepartmentName: '',
  JobTitle: 'Operations',
  EmployeeCode: 'EMP-001',
  EmployeeType: 'Staff',
  Status: 'Active',
  DefaultWorkspaceKey: '',
  DefaultDashboardKey: '',
  HomeRoute: '',
  TimeZone: 'Asia/Dubai',
  Language: 'en',
  Theme: 'system'
};

export const USER_IMPORT_SAMPLE_ROWS: UserImportRow[] = [
  USER_IMPORT_SAMPLE,
  {
    ...USER_IMPORT_SAMPLE,
    FullName: 'Tenant Admin Example',
    Email: 'tenant.admin@example.com',
    Role: 'Tenant Administrator',
    PlatformRoleCode: 'TENANT_ADMIN',
    EmployeeType: 'Admin'
  },
  {
    ...USER_IMPORT_SAMPLE,
    FullName: 'Dispatcher Example',
    Email: 'dispatch@example.com',
    Role: 'Dispatcher',
    PlatformRoleCode: 'DISPATCHER',
    JobTitle: 'Dispatch'
  }
];

export function getUserImportExportColumns(): ExportColumn<UserImportRow>[] {
  return USER_IMPORT_HEADERS.map(header => ({
    header,
    accessor: (row: UserImportRow) => row[header] ?? '',
    excelWidth: Math.max(header.length + 2, 14)
  }));
}

export function userToImportRow(user: User, options?: { includePasswordPlaceholder?: boolean }): UserImportRow {
  const primaryCode = user.assignedRoles?.[0]?.code ?? '';
  return {
    FullName: user.fullName || '',
    Email: user.email || '',
    Password: options?.includePasswordPlaceholder ? '' : '',
    Phone: user.phone || '',
    Role: user.assignedRoles?.[0]?.displayName
      ?? UserRoleLabels[user.role]
      ?? String(user.role),
    PlatformRoleCode: primaryCode,
    BranchId: user.branchId != null ? String(user.branchId) : '',
    BranchName: user.branchName || '',
    DepartmentId: user.departmentId != null ? String(user.departmentId) : '',
    DepartmentName: user.departmentName || '',
    JobTitle: user.jobTitle || '',
    EmployeeCode: user.employeeCode || '',
    EmployeeType: user.employeeType || '',
    Status: user.status || (user.isActive ? 'Active' : 'Inactive'),
    DefaultWorkspaceKey: user.defaultWorkspaceKey || '',
    DefaultDashboardKey: user.defaultDashboardKey || '',
    HomeRoute: user.homeRoute || '',
    TimeZone: user.timeZone || '',
    Language: user.language || '',
    Theme: user.theme || ''
  };
}

function cell(row: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const match = Object.keys(row).find(k => k.trim().toLowerCase() === key.toLowerCase());
    if (match != null && row[match] != null && String(row[match]).trim() !== '') {
      return String(row[match]).trim();
    }
  }
  return '';
}

function parseOptionalInt(value: string): number | null {
  if (!value) return null;
  const n = Number(value);
  return Number.isInteger(n) && n > 0 ? n : null;
}

function resolveBranchId(
  branchIdRaw: string,
  branchName: string,
  branches: { id: number; name: string }[]
): { id: number | null; error?: string; status?: string } {
  const id = parseOptionalInt(branchIdRaw);
  if (id != null) {
    const exists = branches.some(b => b.id === id);
    return {
      id,
      status: exists ? '✓ Branch ID' : 'Unknown BranchId',
      error: exists ? undefined : `BranchId ${id} not found.`
    };
  }
  if (!branchName) return { id: null, status: '—' };
  const matches = branches.filter(b => b.name.toLowerCase() === branchName.toLowerCase());
  if (matches.length === 1) return { id: matches[0].id, status: `✓ ${matches[0].name}` };
  if (matches.length === 0) {
    return {
      id: null,
      status: `? ${branchName}`,
      error: `Unknown branch "${branchName}".`
    };
  }
  return { id: null, status: `? ${branchName}`, error: `Ambiguous branch name "${branchName}". Use BranchId.` };
}

function resolveDepartmentId(
  departmentIdRaw: string,
  departmentName: string,
  departments: { id: number; name: string }[]
): { id: number | null; error?: string; status?: string } {
  const id = parseOptionalInt(departmentIdRaw);
  if (id != null) {
    const exists = departments.some(d => d.id === id);
    return {
      id,
      status: exists ? '✓ Department ID' : 'Unknown DepartmentId',
      error: exists ? undefined : `DepartmentId ${id} not found.`
    };
  }
  if (!departmentName) return { id: null, status: '—' };
  const matches = departments.filter(d => d.name.toLowerCase() === departmentName.toLowerCase());
  if (matches.length === 1) return { id: matches[0].id, status: `✓ ${matches[0].name}` };
  if (matches.length === 0) {
    return {
      id: null,
      status: `? ${departmentName}`,
      error: `Unknown department "${departmentName}".`
    };
  }
  return { id: null, status: `? ${departmentName}`, error: `Ambiguous department name "${departmentName}". Use DepartmentId.` };
}

function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

export function formatImportPhone(phone: string): string {
  const digits = phone.replace(/\D/g, '');
  if (!digits) return phone;
  if (digits.length >= 12 && digits.startsWith('92')) {
    return `+92 ${digits.slice(2, 5)} ${digits.slice(5)}`;
  }
  if (digits.length >= 11 && digits.startsWith('971')) {
    return `+971 ${digits.slice(3)}`;
  }
  if (phone.trim().startsWith('+')) return phone.trim();
  return `+${digits}`;
}

function computeSeverity(issues: ImportRowIssue[]): RowSeverity {
  if (issues.some(i => i.severity === 'error')) return 'error';
  if (issues.some(i => i.severity === 'warning')) return 'warning';
  return 'ready';
}

function pushIssue(
  issues: ImportRowIssue[],
  issue: ImportRowIssue
): void {
  issues.push(issue);
}

export function parseUserImportRecords(
  records: Record<string, unknown>[],
  context: UserImportParseContext
): ParsedUserImportRow[] {
  const { branches, departments, assignableRoles, isPlatformSuperAdmin } = context;
  const fileEmails = new Map<string, number>();

  const parsed = records.map((raw, index) => {
    const rowNumber = index + 2;
    const issues: ImportRowIssue[] = [];
    const fullName = cell(raw, 'FullName', 'Name', 'full_name');
    const email = cell(raw, 'Email');
    const password = cell(raw, 'Password');
    const phoneRaw = cell(raw, 'Phone');
    const roleRaw = cell(raw, 'Role');
    const platformRoleRaw = cell(raw, 'PlatformRoleCode', 'PlatformRole');
    const employeeType = cell(raw, 'EmployeeType', 'Type');
    const status = cell(raw, 'Status');
    const employeeCode = cell(raw, 'EmployeeCode', 'Employee ID');

    const rawRow: Partial<UserImportRow> = {
      FullName: fullName,
      Email: email,
      Password: password,
      Phone: phoneRaw,
      Role: roleRaw,
      PlatformRoleCode: platformRoleRaw,
      EmployeeCode: employeeCode,
      EmployeeType: employeeType,
      Status: status
    };

    if (!fullName && !email && !phoneRaw && !roleRaw) {
      return { rowNumber, issues: [{ code: 'empty', message: 'Empty row.', severity: 'error' as const }], severity: 'error' as const, error: 'Empty row.' };
    }

    if (!fullName) pushIssue(issues, { code: 'fullName', field: 'FullName', message: 'FullName is required.', severity: 'error' });
    else if (fullName.length > 100) {
      pushIssue(issues, { code: 'fullName', field: 'FullName', message: 'FullName max length is 100.', severity: 'error' });
    }

    if (!email) pushIssue(issues, { code: 'email', field: 'Email', message: 'Email is required.', severity: 'error' });
    else if (!isValidEmail(email)) {
      pushIssue(issues, { code: 'email', field: 'Email', message: 'Email is invalid.', severity: 'error' });
    } else {
      const key = email.toLowerCase();
      if (fileEmails.has(key)) {
        pushIssue(issues, {
          code: 'dup_file_email',
          field: 'Email',
          message: 'Duplicate email in this file.',
          suggestion: 'Skip or fix duplicate row',
          severity: 'warning'
        });
      } else {
        fileEmails.set(key, rowNumber);
      }
      if (context.existingEmails.has(key)) {
        pushIssue(issues, {
          code: 'dup_tenant_email',
          field: 'Email',
          message: 'Email already exists in this company.',
          suggestion: 'Skip duplicate or update existing (coming soon)',
          severity: 'warning'
        });
      }
    }

    if (!phoneRaw) {
      pushIssue(issues, { code: 'phone', field: 'Phone', message: 'Phone is required.', severity: 'error' });
    } else {
      const digits = phoneRaw.replace(/\D/g, '');
      if (digits.length < 10) {
        pushIssue(issues, {
          code: 'phone',
          field: 'Phone',
          message: 'Phone number looks too short.',
          suggestion: 'Use international format, e.g. +971500000000',
          severity: 'warning'
        });
      }
      if (context.existingPhones.has(digits)) {
        pushIssue(issues, {
          code: 'dup_phone',
          field: 'Phone',
          message: 'Phone already used by another user.',
          suggestion: 'Skip or verify number',
          severity: 'warning'
        });
      }
    }

    if (employeeCode && context.existingEmployeeCodes.has(employeeCode.toLowerCase())) {
      pushIssue(issues, {
        code: 'dup_employee_code',
        field: 'EmployeeCode',
        message: 'Employee code already exists.',
        suggestion: 'Skip or change code',
        severity: 'warning'
      });
    }

    if (password && password.length < 6) {
      pushIssue(issues, {
        code: 'password',
        field: 'Password',
        message: 'Password must be at least 6 characters.',
        suggestion: 'Leave blank to auto-generate',
        severity: 'error'
      });
    }

    const roleInput = platformRoleRaw || roleRaw;
    let resolvedRole: ResolvedImportRole | undefined;
    if (!roleInput) {
      pushIssue(issues, {
        code: 'role',
        field: 'Role',
        message: 'Role is required.',
        suggestion: 'Fleet Manager, Dispatcher, TENANT_ADMIN, etc.',
        severity: 'error'
      });
    } else {
      const resolution = resolveImportRole(roleInput, assignableRoles, isPlatformSuperAdmin);
      if (!resolution.ok) {
        pushIssue(issues, {
          code: 'role',
          field: 'Role',
          message: resolution.message,
          suggestion: resolution.didYouMean
            ? `Map to ${resolution.didYouMean} (${resolution.didYouMeanCode})`
            : resolution.suggestion,
          severity: 'error'
        });
      } else {
        resolvedRole = resolution.role;
        if (resolution.role.confidence === 'fuzzy') {
          pushIssue(issues, {
            code: 'role_fuzzy',
            field: 'Role',
            message: `Role "${resolution.role.mappedFrom}" matched to ${resolution.role.displayLabel}.`,
            suggestion: `Mapped to ${resolution.role.platformRoleCode}`,
            severity: 'warning'
          });
        }
      }
    }

    if (employeeType && !EMPLOYEE_TYPES.some(t => t.toLowerCase() === employeeType.toLowerCase())) {
      pushIssue(issues, {
        code: 'employeeType',
        field: 'EmployeeType',
        message: `Invalid EmployeeType "${employeeType}".`,
        suggestion: EMPLOYEE_TYPES.join(', '),
        severity: 'error'
      });
    }

    if (status && !USER_LIFECYCLE_STATUSES.some(s => s.toLowerCase() === status.toLowerCase())) {
      pushIssue(issues, {
        code: 'status',
        field: 'Status',
        message: `Invalid Status "${status}".`,
        suggestion: USER_LIFECYCLE_STATUSES.join(', '),
        severity: 'error'
      });
    }

    const branch = resolveBranchId(cell(raw, 'BranchId'), cell(raw, 'BranchName'), branches);
    if (branch.error) {
      pushIssue(issues, {
        code: 'branch',
        field: 'BranchName',
        message: branch.error,
        suggestion: 'Use BranchId or fix name',
        severity: 'error'
      });
    }

    const department = resolveDepartmentId(
      cell(raw, 'DepartmentId'),
      cell(raw, 'DepartmentName'),
      departments
    );
    if (department.error) {
      pushIssue(issues, {
        code: 'department',
        field: 'DepartmentName',
        message: department.error,
        suggestion: 'Use DepartmentId or fix name',
        severity: 'error'
      });
    }

    const severity = computeSeverity(issues);
    const firstError = issues.find(i => i.severity === 'error');

    if (severity === 'error' || !resolvedRole || !email || !fullName || !phoneRaw) {
      return {
        rowNumber,
        raw: rawRow,
        issues,
        severity,
        error: firstError?.message ?? issues[0]?.message,
        rawEmail: email,
        displayPhone: phoneRaw ? formatImportPhone(phoneRaw) : undefined,
        branchStatus: branch.status,
        departmentStatus: department.status,
        passwordPreview: (password ? 'provided' : 'blank') as 'blank' | 'provided'
      };
    }

    const normalizedType = employeeType
      ? EMPLOYEE_TYPES.find(t => t.toLowerCase() === employeeType.toLowerCase()) ?? employeeType
      : null;
    const normalizedStatus = status
      ? USER_LIFECYCLE_STATUSES.find(s => s.toLowerCase() === status.toLowerCase()) ?? status
      : null;

    const dto: CreateUserDto = {
      fullName,
      email,
      password: password || '',
      phone: phoneRaw,
      role: resolvedRole.legacyRole,
      platformRoleCode: resolvedRole.platformRoleCode,
      branchId: branch.id,
      departmentId: department.id,
      jobTitle: cell(raw, 'JobTitle') || null,
      employeeCode: employeeCode || null,
      employeeType: normalizedType,
      status: normalizedStatus,
      defaultWorkspaceKey: cell(raw, 'DefaultWorkspaceKey') || null,
      defaultDashboardKey: cell(raw, 'DefaultDashboardKey') || null,
      homeRoute: cell(raw, 'HomeRoute') || null,
      timeZone: cell(raw, 'TimeZone') || null,
      language: cell(raw, 'Language') || null,
      theme: cell(raw, 'Theme') || null
    };

    return {
      rowNumber,
      dto,
      raw: rawRow,
      resolvedRole,
      issues,
      severity,
      rawEmail: email,
      displayPhone: formatImportPhone(phoneRaw),
      branchStatus: branch.status,
      departmentStatus: department.status,
      passwordPreview: (password ? 'provided' : 'blank') as 'blank' | 'provided'
    };
  });

  return parsed.filter(r => !(r.error === 'Empty row.' && r.issues.length === 1 && r.issues[0].code === 'empty')) as ParsedUserImportRow[];
}

export function reparseImportRow(
  row: ParsedUserImportRow,
  context: UserImportParseContext,
  overrides?: { roleRaw?: string; platformRoleCode?: string }
): ParsedUserImportRow {
  const roleRaw = overrides?.roleRaw ?? row.raw?.Role ?? '';
  const platform = overrides?.platformRoleCode ?? row.raw?.PlatformRoleCode ?? '';
  const roleInput = platform || roleRaw;
  const record: Record<string, unknown> = {
    FullName: row.raw?.FullName ?? row.dto?.fullName ?? '',
    Email: row.raw?.Email ?? row.dto?.email ?? '',
    Password: row.raw?.Password ?? row.dto?.password ?? '',
    Phone: row.raw?.Phone ?? row.dto?.phone ?? '',
    Role: roleInput,
    PlatformRoleCode: platform,
    EmployeeCode: row.raw?.EmployeeCode ?? row.dto?.employeeCode ?? '',
    EmployeeType: row.raw?.EmployeeType ?? row.dto?.employeeType ?? '',
    Status: row.raw?.Status ?? row.dto?.status ?? '',
    BranchId: row.raw?.BranchId ?? (row.dto?.branchId != null ? String(row.dto.branchId) : ''),
    BranchName: row.raw?.BranchName ?? '',
    DepartmentId: row.raw?.DepartmentId ?? (row.dto?.departmentId != null ? String(row.dto.departmentId) : ''),
    DepartmentName: row.raw?.DepartmentName ?? ''
  };
  const [reparsed] = parseUserImportRecords([record], context);
  return { ...reparsed, rowNumber: row.rowNumber };
}

export interface ImportErrorReportRow {
  Row: number;
  Email: string;
  Field: string;
  Severity: string;
  Message: string;
  Suggestion: string;
  ServerResult: string;
}

export function buildImportErrorReport(rows: {
  rowNumber: number;
  rawEmail?: string;
  issues: ImportRowIssue[];
  serverError?: string | null;
  serverSkipped?: string | null;
}[]): ImportErrorReportRow[] {
  const out: ImportErrorReportRow[] = [];
  for (const row of rows) {
    if (row.serverError) {
      out.push({
        Row: row.rowNumber,
        Email: row.rawEmail ?? '',
        Field: 'Server',
        Severity: 'error',
        Message: row.serverError,
        Suggestion: '',
        ServerResult: 'Failed'
      });
    }
    if (row.serverSkipped) {
      out.push({
        Row: row.rowNumber,
        Email: row.rawEmail ?? '',
        Field: 'Server',
        Severity: 'warning',
        Message: row.serverSkipped,
        Suggestion: 'Skipped',
        ServerResult: 'Skipped'
      });
    }
    for (const issue of row.issues) {
      out.push({
        Row: row.rowNumber,
        Email: row.rawEmail ?? '',
        Field: issue.field ?? '',
        Severity: issue.severity,
        Message: issue.message,
        Suggestion: issue.suggestion ?? '',
        ServerResult: ''
      });
    }
  }
  return out;
}
