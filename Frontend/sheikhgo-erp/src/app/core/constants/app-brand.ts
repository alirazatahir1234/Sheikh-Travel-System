export const APP_PRODUCT_NAME = 'SheikhGo ERP';
export const APP_PRODUCT_SHORT_NAME = 'SheikhGo ERP';
export const COMPANY_NAME = 'Sheikh Travel Group';
/** Placeholder until a real company address is supplied — never fabricated, shown as-is on report letterheads. */
export const COMPANY_ADDRESS = '';
export const APP_LOGO_PATH = '/brand/sheikhgo-logo.png';
export const APP_SIDEBAR_LOGO_PATH = '/brand/sheikhgo-logo-white.png';

/** PDF/export document titles: "SheikhGo ERP – Users" */
export function exportDocumentTitle(section: string): string {
  return `${APP_PRODUCT_NAME} – ${section}`;
}
