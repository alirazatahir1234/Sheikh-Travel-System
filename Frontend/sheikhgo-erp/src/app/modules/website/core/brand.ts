import { APP_PRODUCT_NAME, COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

/**
 * Public marketing brand.
 * Logo MUST match /auth/login — transparent PNG (`sheikhgo-logo.png`), never the white-box asset.
 */
export const WEBSITE_BRAND = {
  productName: 'SheikhGo',
  companyName: COMPANY_NAME,
  erpProductName: APP_PRODUCT_NAME,
  tagline: 'Move Smarter. Travel Further.',
  positioning: 'Intelligent Fleet & Travel Operations Platform',
  /** Same asset as login hero / form card */
  logo: APP_LOGO_PATH,
  logoWhite: APP_LOGO_PATH,
  supportEmail: 'info@sheikhgo.com',
  salesEmail: 'info@sheikhgo.com',
  privacyEmail: 'info@sheikhgo.com',
  loginPath: '/auth/login',
  forgotPasswordPath: '/auth/forgot-password',
  year: new Date().getFullYear(),
} as const;
