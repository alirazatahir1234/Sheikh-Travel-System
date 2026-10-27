import { COMPANY_ADDRESS, COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

export type WebsiteContactPhone = {
  label: string;
  display: string;
  tel: string;
};

/** Public marketing lines — verified with Ali; PK matches WhatsApp Business. */
export const WEBSITE_CONTACT_PHONES: readonly WebsiteContactPhone[] = [
  { label: 'UAE', display: '+971 55 77 0121', tel: '+97155770121' },
  { label: 'Pakistan', display: '+92 317 7368305', tel: '+923177368305' },
];

/** E.164 digits (no +) for wa.me links — same as Pakistan contact line. */
export const WEBSITE_WHATSAPP_DIGITS = '923177368305';

/** Office postal address when confirmed; never fabricated on the marketing site. */
export const WEBSITE_OFFICE_ADDRESS = COMPANY_ADDRESS;

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
  /** Marketing-site product name (login app may still show legacy ERP label). */
  nexusProductName: 'SheikhGo Nexus',
  erpProductName: 'SheikhGo Nexus',
  signInToProductLabel: 'Sign in to Nexus',
  tagline: 'Technology Today. A Smarter Tomorrow.',
  positioning: 'Your Technology Partner for a Smarter Future',
  mission:
    'SheikhGo Technologies delivers innovative IT, AI and software solutions that help businesses grow, streamline operations and succeed in the digital world.',
  footerBlurb:
    'SheikhGo Technologies delivers innovative IT, software and AI solutions for modern businesses.',
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
