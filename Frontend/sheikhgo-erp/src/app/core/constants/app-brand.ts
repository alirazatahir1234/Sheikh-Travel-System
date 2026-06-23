export const APP_PRODUCT_NAME = 'SheikhGo ERP';
export const APP_PRODUCT_SHORT_NAME = 'SheikhGo';
export const COMPANY_NAME = 'Sheikh Travel Group';
export const APP_LOGO_PATH = '/brand/sheikhgo-logo.png';
export const APP_SIDEBAR_LOGO_PATH = '/brand/sheikhgo-logo-white.png';

/** PDF/export document titles: "SheikhGo ERP – Users" */
export function exportDocumentTitle(section: string): string {
  return `${APP_PRODUCT_NAME} – ${section}`;
}
