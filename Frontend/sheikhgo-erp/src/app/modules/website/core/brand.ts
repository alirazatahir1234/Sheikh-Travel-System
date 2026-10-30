import { COMPANY_ADDRESS, COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

export type WebsiteContactPhone = {
  label: string;
  display: string;
  tel: string;
};

/** Public marketing lines — verified with Ali; PK matches WhatsApp Business. */
export const WEBSITE_CONTACT_PHONES: readonly WebsiteContactPhone[] = [
  { label: 'UAE', display: '+971 55 770 1219', tel: '+971557701219' },
  { label: 'Pakistan', display: '+92 301 616 9209', tel: '+923016169209' },
];

/** E.164 digits (no +) for wa.me links — same as Pakistan contact line. */
export const WEBSITE_WHATSAPP_DIGITS = '923016169209';

/** Office postal address — verified with Ali (Exchange Tower, Business Bay). */
export const WEBSITE_OFFICE_ADDRESS = COMPANY_ADDRESS;

/** Marketing sample pin — Dubai corporate office (Live Tracking / GPS pages). */
export const WEBSITE_DEMO_MAP_COORDINATES = '25.1854, 55.2663';
export const WEBSITE_DEMO_MAP_ADDRESS_SHORT = 'The Exchange Tower, Business Bay, Dubai';

const WEBSITE_OFFICE_MAPS_QUERY = 'The Exchange Tower, Business Bay, Dubai, United Arab Emirates';

/** Prefer verified office address when CMS holds invalid or hidden-service values. */
export function resolveWebsiteOfficeAddress(cmsValue: string | null | undefined): string {
  const trimmed = cmsValue?.trim() ?? '';
  if (!trimmed) return WEBSITE_OFFICE_ADDRESS;
  if (/\.onion\b/i.test(trimmed) || /hidden\s+service/i.test(trimmed)) {
    return WEBSITE_OFFICE_ADDRESS;
  }
  return trimmed;
}

/** Opens Google Maps for directions / full map view. */
export const WEBSITE_OFFICE_MAPS_URL = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(WEBSITE_OFFICE_MAPS_QUERY)}`;

/** iframe embed for Contact page (no API key). */
export const WEBSITE_OFFICE_MAPS_EMBED_URL = `https://www.google.com/maps?q=${encodeURIComponent(WEBSITE_OFFICE_MAPS_QUERY)}&output=embed`;

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
