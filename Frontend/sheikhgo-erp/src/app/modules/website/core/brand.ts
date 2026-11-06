import { COMPANY_ADDRESS, COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

export type WebsiteContactPhone = {
  label: string;
  display: string;
  tel: string;
};

/** Public marketing lines — verified with Ali; PK matches WhatsApp Business. */
export const WEBSITE_CONTACT_PHONES: readonly WebsiteContactPhone[] = [
  { label: 'Pakistan', display: '+92 317 736 8305', tel: '+923177368305' },
];

/** E.164 digits (no +) for wa.me links — same as Pakistan contact line. */
export const WEBSITE_WHATSAPP_DIGITS = '923177368305';

/** Office postal address — verified with Ali (Park View City, Lahore). */
export const WEBSITE_OFFICE_ADDRESS = COMPANY_ADDRESS;

/** Marketing sample pin — Lahore corporate office (Live Tracking / GPS pages). */
export const WEBSITE_DEMO_MAP_COORDINATES = '31.4528, 74.2025';
export const WEBSITE_DEMO_MAP_ADDRESS_SHORT = 'Park View City, Multan Rd, Lahore';

const WEBSITE_OFFICE_MAPS_QUERY =
  'Park View City, 2nd Floor 08 Rose Commercial, Opposite Soneri Bank, Main Boulevard, Multan Rd, Lahore, 54000, Pakistan';

/** Prefer verified office address when CMS holds invalid, hidden-service, or stale HQ values. */
export function resolveWebsiteOfficeAddress(cmsValue: string | null | undefined): string {
  const trimmed = cmsValue?.trim() ?? '';
  if (!trimmed) return WEBSITE_OFFICE_ADDRESS;
  if (/\.onion\b/i.test(trimmed) || /hidden\s+service/i.test(trimmed)) {
    return WEBSITE_OFFICE_ADDRESS;
  }
  // Supersede previous Dubai HQ if still stored in WebsiteSettings CMS.
  if (/exchange\s+tower/i.test(trimmed) || /business\s+bay/i.test(trimmed)) {
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
