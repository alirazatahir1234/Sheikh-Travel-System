/**
 * Platform default timezone (Pakistan). Used only until the company's timezone has loaded and as the
 * default for new tenants/branches in forms. Mirrors backend Localization:DefaultTimeZone; the API
 * returns the effective company timezone in GET /tenants/branding (effectiveTimeZone).
 */
export const DEFAULT_TIME_ZONE = 'Asia/Karachi';
