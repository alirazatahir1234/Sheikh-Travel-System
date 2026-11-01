export const APP_PRODUCT_NAME = 'SheikhGo Nexus';
export const APP_PRODUCT_SHORT_NAME = 'SheikhGo Nexus';
/** Sidebar brand subtitle under the product name. */
export const APP_PRODUCT_TAGLINE = 'Smarter Fleet. Stronger Business.';
export const COMPANY_NAME = 'Sheikh Travel Group';
/** Corporate office — shown on report letterheads and the public marketing site. */
export const COMPANY_ADDRESS = 'The Exchange Tower, Business Bay, Dubai, United Arab Emirates';
export const APP_LOGO_PATH = '/brand/sheikhgo-logo.png';
/** ERP shell / fleet sidebar mark — transparent Nexus lockup for dark navy rail. */
export const APP_SIDEBAR_LOGO_PATH = '/brand/sheikhgo-nexus-logo.png';
/** Login page only (`/auth/login`) — do not reuse elsewhere. */
export const APP_LOGIN_LOGO_PATH = '/brand/sheikhgo-logo-login-v8.png';

/** PDF/export document titles: "SheikhGo Nexus – Users" */
export function exportDocumentTitle(section: string): string {
  return `${APP_PRODUCT_NAME} – ${section}`;
}
