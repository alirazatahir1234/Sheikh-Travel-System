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
