import { APP_PRODUCT_NAME, COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

/**
 * Public marketing brand — SheikhGo Technologies (IT / AI company).
 * Logo MUST match /auth/login — transparent PNG (`sheikhgo-logo.png`), never the white-box asset.
 */
export const WEBSITE_BRAND = {
  /** Full company name for chrome / SEO / product naming */
  productName: 'SheikhGo Technologies',
  /** Short mark when logo lockup needs a compact wordmark */
  shortMark: 'SheikhGo',
  /** Alias used by chrome that already reads companyName */
  companyName: 'SheikhGo Technologies',
  legalEntity: COMPANY_NAME,
  erpProductName: APP_PRODUCT_NAME,
  tagline: 'Technology Today. A Smarter Tomorrow.',
  positioning: 'Fleet software and custom technology for operators who run vehicles.',
  mission:
    'SheikhGo Technologies builds SheikhGo Fleet — live GPS, vehicles, drivers and trips in one platform — plus custom software and AI when your operation needs more.',
  footerBlurb:
    'SheikhGo Technologies builds fleet management software and custom IT solutions for transport, logistics and travel operators.',
  whatsappDigits: '923177368305',
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
