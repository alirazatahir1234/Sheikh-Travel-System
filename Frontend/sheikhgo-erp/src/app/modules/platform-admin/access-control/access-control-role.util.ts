import { Permission, RoleSummary } from '../../../core/models/platform.model';

export const ROLE_CATEGORY_OPTIONS = [
  'Fleet',
  'Operations',
  'Finance',
  'Platform',
  'Security',
  'Support',
  'HR'
] as const;

/** Generate SCREAMING_SNAKE_CASE code from display name. */
export function slugifyRoleCode(name: string): string {
  const slug = name
    .trim()
    .replace(/['']/g, '')
    .replace(/[^A-Za-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/_+/g, '_')
    .toUpperCase();
  return slug.slice(0, 64);
}

export function isValidRoleCode(code: string): boolean {
  return /^[A-Z][A-Z0-9_]{1,63}$/.test(code.trim());
}

export function inferRoleCategory(role: RoleSummary): string {
  const explicit = role.category?.trim();
  if (explicit) return explicit;

  const code = (role.code || '').toUpperCase();
  if (code.includes('GPS') || code.includes('FLEET') || code.includes('DRIVER') || code.includes('VEHICLE')) {
    return 'Fleet';
  }
  if (code.includes('DISPATCH') || code.includes('BOOKING') || code.includes('TRIP') || code.includes('OPS')) {
    return 'Operations';
  }
  if (code.includes('ACCOUNT') || code.includes('FINANCE') || code.includes('PAYMENT') || code.includes('VAT')) {
    return 'Finance';
  }
  if (code.includes('ADMIN') || code.includes('TENANT') || code.includes('SUPER')) {
    return 'Platform';
  }
  if (code.includes('SECURITY') || code.includes('AUDIT')) {
    return 'Security';
  }
  return role.isSystem ? 'Platform' : 'Operations';
}

export function roleVisibilityLabel(role: RoleSummary): string {
  if (role.visible === false) return 'Hidden';
  return role.isSystem || role.roleType === 'System' ? 'System' : 'Tenant';
}

export function permissionFriendlyLabel(p: Permission): string {
  if (p.displayName?.trim()) return p.displayName.trim();
  const code = p.permissionCode || '';
  const parts = code.split('.').filter(Boolean);
  const tail = parts[parts.length - 1] || code;
  const spaced = tail.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ');
  const module = parts.length > 1 ? parts[0] : '';
  const prefix = module ? `${module} — ` : '';
  return prefix + spaced;
}

export function permissionDescription(p: Permission): string {
  if (p.description?.trim()) return p.description.trim();
  return `Grants access: ${p.permissionCode}`;
}
