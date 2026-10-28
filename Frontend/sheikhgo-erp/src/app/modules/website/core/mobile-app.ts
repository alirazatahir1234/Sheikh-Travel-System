/**
 * Public mobile app marketing facts for the website.
 * Store URLs stay null until real App Store / Play listings exist — do not invent fake apps.
 */

export type MobileAppStoreId = 'ios' | 'android';

export type MobileAppStoreLink = {
  id: MobileAppStoreId;
  /** Short badge label (e.g. App Store). */
  storeLabel: string;
  /**
   * Live store URL when published.
   * Null = clearly labeled coming-soon placeholder (no invented listing).
   */
  url: string | null;
};

/** Display name from store listing drafts (`Frontend/sheikh-driver/store/STORE_LISTING.md`). */
export const MOBILE_APP_NAME = 'SheikhGo Tracking';

/** One-line benefit from store short description / App Store subtitle. */
export const MOBILE_APP_BENEFIT = 'Fleet driver app for trips, GPS, attendance, fuel, and safety.';

export const MOBILE_APP_STORE_LINKS: readonly MobileAppStoreLink[] = [
  {
    id: 'ios',
    storeLabel: 'App Store',
    url: null, // PLACEHOLDER — no live App Store listing URL in repo yet
  },
  {
    id: 'android',
    storeLabel: 'Google Play',
    url: null, // PLACEHOLDER — no live Play listing URL in repo yet
  },
] as const;

export const MOBILE_APP_PACKAGE_ID = 'com.sheikhgo.fleet';
