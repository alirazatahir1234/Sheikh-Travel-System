/** localStorage key for runtime tenant slug (from branding API). */
export const TENANT_SLUG_STORAGE_KEY = 'sheikhgo.tenant_slug';

/**
 * Resolve tenant slug for X-Tenant-Slug header / branding.
 * Prefer runtime branding slug, then environment fallback.
 */
export function resolveTenantSlug(envFallback?: string | null): string {
  try {
    const stored = localStorage.getItem(TENANT_SLUG_STORAGE_KEY)?.trim();
    if (stored) return stored;
  } catch {
    /* ignore quota / private mode */
  }
  const fallback = (envFallback || '').trim();
  return fallback || 'default';
}

export function persistTenantSlug(slug: string | null | undefined): void {
  const cleaned = (slug || '').trim();
  if (!cleaned) return;
  try {
    localStorage.setItem(TENANT_SLUG_STORAGE_KEY, cleaned);
  } catch {
    /* ignore */
  }
}
