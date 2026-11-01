/**
 * Highest-privilege role first (mirrors backend RoleRegistrySeed.SortOrder).
 * Never use numeric Role.Id order for default / display role.
 */
export const ROLE_PRIVILEGE_PRIORITY: readonly string[] = [
  'SUPER_ADMIN',
  'TENANT_ADMIN',
  'ADMIN',
  'OWNER',
  'FLEET_MANAGER',
  'DRIVER_MANAGER',
  'GPS_OPERATOR',
  'DISPATCHER',
  'ACCOUNTANT',
  'DRIVER',
  'FIELD_DRIVER'
];

function privilegeRank(code: string): number {
  const upper = (code || '').toUpperCase();
  const idx = ROLE_PRIVILEGE_PRIORITY.indexOf(upper);
  return idx >= 0 ? idx : ROLE_PRIVILEGE_PRIORITY.length + 1;
}

/** Orders role codes highest privilege first. */
export function orderRolesByPrivilege(roles: string[] | null | undefined): string[] {
  if (!roles?.length) return [];
  const seen = new Set<string>();
  const unique: string[] = [];
  for (const r of roles) {
    const trimmed = (r || '').trim();
    if (!trimmed) continue;
    const key = trimmed.toUpperCase();
    if (seen.has(key)) continue;
    seen.add(key);
    unique.push(trimmed);
  }
  return unique.sort((a, b) => {
    const diff = privilegeRank(a) - privilegeRank(b);
    return diff !== 0 ? diff : a.localeCompare(b, undefined, { sensitivity: 'base' });
  });
}

/** Default / display role for multi-role users. */
export function selectPrimaryRole(roles: string[] | null | undefined, fallback = 'Member'): string {
  const ordered = orderRolesByPrivilege(roles);
  return ordered[0] || fallback;
}

/** Normalize role codes for comparisons (Driver, FIELD-DRIVER → FIELD_DRIVER). */
export function normalizeRoleCode(code: string | null | undefined): string {
  return (code || '').toUpperCase().replace(/[\s-]/g, '_');
}

/** True for DRIVER / FIELD_DRIVER (field-ops driver workspace accounts). */
export function isDriverRoleCode(code: string | null | undefined): boolean {
  const c = normalizeRoleCode(code);
  return c === 'DRIVER' || c === 'FIELD_DRIVER';
}

/** True when any assigned role is a driver workspace role. */
export function rolesIncludeDriver(roles: string[] | null | undefined): boolean {
  return (roles ?? []).some(isDriverRoleCode);
}

/**
 * True when the highest-privilege role is a driver workspace role.
 * Multi-role staff (e.g. Admin + Driver) keep full ERP access; only
 * driver-primary accounts are locked to /my-trips|/wallet|/profile.
 */
export function isDriverPrimaryAccount(roles: string[] | null | undefined): boolean {
  const primary = orderRolesByPrivilege(roles)[0];
  return isDriverRoleCode(primary);
}

/** Case-insensitive role membership (avoids brittle roles.includes('Driver')). */
export function rolesInclude(roles: string[] | null | undefined, ...needles: string[]): boolean {
  if (!roles?.length || !needles.length) return false;
  const set = new Set(roles.map(normalizeRoleCode));
  return needles.some(n => set.has(normalizeRoleCode(n)));
}
