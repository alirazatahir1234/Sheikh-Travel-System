import {
  UserRole,
  UserRoleLabels,
  parseUserRole
} from '../../core/models/user.model';
import { RoleSummary } from '../../core/models/platform.model';
import {
  PRIMARY_ROLE_CATALOG,
  PrimaryRoleDefinition,
  getPrimaryRoleById
} from './user-primary-roles';

export type RoleResolveConfidence = 'exact' | 'fuzzy' | 'custom';

export interface ResolvedImportRole {
  legacyRole: UserRole;
  platformRoleCode: string;
  displayLabel: string;
  mappedFrom: string;
  confidence: RoleResolveConfidence;
  primaryDef?: PrimaryRoleDefinition;
}

export interface UnresolvedImportRole {
  ok: false;
  message: string;
  suggestion?: string;
  didYouMean?: string;
  didYouMeanCode?: string;
}

export type ImportRoleResolution = { ok: true; role: ResolvedImportRole } | UnresolvedImportRole;

const LEGACY_LABEL_TO_PLATFORM: Record<string, string> = {
  admin: 'TENANT_ADMIN',
  administrator: 'TENANT_ADMIN',
  'tenant admin': 'TENANT_ADMIN',
  'tenant administrator': 'TENANT_ADMIN',
  dispatcher: 'DISPATCHER',
  'gps operator': 'GPS_OPERATOR',
  gpsoperator: 'GPS_OPERATOR',
  'gps ops': 'GPS_OPERATOR',
  driver: 'DRIVER',
  accountant: 'ACCOUNTANT',
  'fleet manager': 'FLEET_MANAGER',
  'driver manager': 'DRIVER_MANAGER',
  'super admin': 'SUPER_ADMIN'
};

function normalizeKey(value: string): string {
  return value.trim().toLowerCase().replace(/[_-]+/g, ' ').replace(/\s+/g, ' ');
}

function levenshtein(a: string, b: string): number {
  const m = a.length;
  const n = b.length;
  if (m === 0) return n;
  if (n === 0) return m;
  const dp = Array.from({ length: m + 1 }, () => new Array<number>(n + 1).fill(0));
  for (let i = 0; i <= m; i++) dp[i][0] = i;
  for (let j = 0; j <= n; j++) dp[0][j] = j;
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      dp[i][j] = Math.min(dp[i - 1][j] + 1, dp[i][j - 1] + 1, dp[i - 1][j - 1] + cost);
    }
  }
  return dp[m][n];
}

function catalogByPlatformCode(code: string): PrimaryRoleDefinition | undefined {
  const upper = code.toUpperCase();
  return PRIMARY_ROLE_CATALOG.find(r => r.platformCode === upper || r.id === upper);
}

function legacyForPlatformCode(code: string): UserRole {
  const def = catalogByPlatformCode(code);
  if (def) return def.legacyRole;
  switch (code.toUpperCase()) {
    case 'TENANT_ADMIN':
    case 'SUPER_ADMIN':
    case 'FLEET_MANAGER':
      return UserRole.Admin;
    case 'DRIVER_MANAGER':
    case 'DISPATCHER':
    case 'GPS_OPERATOR':
      return UserRole.Dispatcher;
    case 'DRIVER':
      return UserRole.Driver;
    case 'ACCOUNTANT':
      return UserRole.Accountant;
    default:
      return UserRole.Dispatcher;
  }
}

function displayForCode(code: string, assignable: RoleSummary[]): string {
  const match = assignable.find(r => r.code.toUpperCase() === code.toUpperCase());
  if (match?.displayName || match?.name) return match.displayName || match.name;
  const def = catalogByPlatformCode(code);
  return def?.label ?? code;
}

function findAssignable(
  raw: string,
  assignable: RoleSummary[]
): { role: RoleSummary; confidence: RoleResolveConfidence } | null {
  const key = normalizeKey(raw);
  const codeKey = raw.trim().toUpperCase().replace(/\s+/g, '_');

  const exactCode = assignable.find(
    r => r.isActive && r.code.toUpperCase() === codeKey
  );
  if (exactCode) return { role: exactCode, confidence: 'custom' };

  const exactName = assignable.find(r => {
    if (!r.isActive) return false;
    const dn = normalizeKey(r.displayName || r.name || '');
    const nm = normalizeKey(r.name || '');
    return dn === key || nm === key;
  });
  if (exactName) return { role: exactName, confidence: 'custom' };

  return null;
}

function fuzzyMatch(
  raw: string,
  assignable: RoleSummary[]
): { label: string; code: string } | null {
  const key = normalizeKey(raw);
  if (key.length < 3) return null;

  const candidates: { label: string; code: string; dist: number }[] = [];

  for (const def of PRIMARY_ROLE_CATALOG) {
    const label = def.label;
    const dist = levenshtein(key, normalizeKey(label));
    if (dist <= 2) {
      candidates.push({ label, code: def.platformCode, dist });
    }
  }

  for (const r of assignable) {
    if (!r.isActive) continue;
    const label = r.displayName || r.name;
    const dist = levenshtein(key, normalizeKey(label));
    if (dist <= 2) {
      candidates.push({ label, code: r.code, dist });
    }
  }

  candidates.sort((a, b) => a.dist - b.dist);
  const best = candidates[0];
  if (!best) return null;
  return { label: best.label, code: best.code };
}

export function resolveImportRole(
  raw: string,
  assignableRoles: RoleSummary[],
  isPlatformSuperAdmin: boolean
): ImportRoleResolution {
  const trimmed = raw?.trim() ?? '';
  if (!trimmed) {
    return {
      ok: false,
      message: 'Role is required.',
      suggestion: 'Use a display name (e.g. Fleet Manager) or code (FLEET_MANAGER).'
    };
  }

  const key = normalizeKey(trimmed);
  const codeFromLegacy = LEGACY_LABEL_TO_PLATFORM[key];
  const numericLegacy = /^[1-4]$/.test(trimmed.trim());
  const legacyLabelMatch = Object.values(UserRoleLabels).some(
    l => normalizeKey(l) === key
  );
  const legacyEnumMatch = Object.keys(UserRole).some(
    k => typeof (UserRole as Record<string, unknown>)[k] === 'number'
      && normalizeKey(k) === key
  );

  let platformCode: string | null = null;
  let confidence: RoleResolveConfidence = 'exact';
  let mappedFrom = trimmed;

  if (codeFromLegacy) {
    platformCode = codeFromLegacy;
  } else if (numericLegacy || legacyLabelMatch || legacyEnumMatch) {
    const legacy = parseUserRole(trimmed);
    platformCode = legacy === UserRole.Admin ? 'TENANT_ADMIN'
      : legacy === UserRole.Dispatcher ? 'DISPATCHER'
      : legacy === UserRole.Driver ? 'DRIVER'
      : 'ACCOUNTANT';
  } else {
    const catalog = PRIMARY_ROLE_CATALOG.find(
      r => normalizeKey(r.label) === key
        || r.id.toUpperCase() === trimmed.toUpperCase()
        || r.platformCode.toUpperCase() === trimmed.toUpperCase().replace(/\s+/g, '_')
    );
    if (catalog) {
      platformCode = catalog.platformCode;
      mappedFrom = catalog.label;
    } else {
      const assignable = findAssignable(trimmed, assignableRoles);
      if (assignable) {
        platformCode = assignable.role.code;
        confidence = assignable.confidence;
        mappedFrom = assignable.role.displayName || assignable.role.name;
      } else {
        const fuzzy = fuzzyMatch(trimmed, assignableRoles);
        if (fuzzy) {
          platformCode = fuzzy.code;
          confidence = 'fuzzy';
          mappedFrom = fuzzy.label;
        }
      }
    }
  }

  if (!platformCode) {
    const fuzzy = fuzzyMatch(trimmed, assignableRoles);
    return {
      ok: false,
      message: `Unknown role "${trimmed}".`,
      suggestion: 'Pick a role from the list or use a platform code (e.g. FLEET_MANAGER).',
      didYouMean: fuzzy?.label,
      didYouMeanCode: fuzzy?.code
    };
  }

  const upper = platformCode.toUpperCase();
  if (upper === 'SUPER_ADMIN' && !isPlatformSuperAdmin) {
    return {
      ok: false,
      message: 'Super Admin can only be assigned by platform owners.',
      suggestion: 'Use Tenant Admin or Administrator instead.',
      didYouMean: 'Tenant Administrator',
      didYouMeanCode: 'TENANT_ADMIN'
    };
  }

  const active = assignableRoles.find(
    r => r.isActive && r.code.toUpperCase() === upper
  );
  if (assignableRoles.length > 0 && !active && upper !== 'SUPER_ADMIN') {
    return {
      ok: false,
      message: `Role "${displayForCode(upper, assignableRoles)}" is not available for this company.`,
      suggestion: 'Choose another role or create it under Access Control → Roles.'
    };
  }

  const primaryDef = getPrimaryRoleById(upper) ?? catalogByPlatformCode(upper);
  const legacyRole = primaryDef?.legacyRole ?? legacyForPlatformCode(upper);

  return {
    ok: true,
    role: {
      legacyRole,
      platformRoleCode: upper,
      displayLabel: displayForCode(upper, assignableRoles),
      mappedFrom: trimmed,
      confidence,
      primaryDef: primaryDef ?? undefined
    }
  };
}

export function applyResolvedRoleToRaw(roleRaw: string, code: string): string {
  const def = catalogByPlatformCode(code);
  return def?.label ?? code;
}
