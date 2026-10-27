export interface NewsAnnouncement {
  id: string;
  date: string;
  category: 'Product' | 'Feature' | 'Company';
  title: string;
  excerpt: string;
}

/** Placeholder announcements — replace with CMS or admin content when ready. */
export const NEWS_ANNOUNCEMENTS: NewsAnnouncement[] = [
  {
    id: 'nexus-erp-demo',
    date: 'Oct 27, 2026',
    category: 'Product',
    title: 'New Nexus ERP walkthrough for sales & onboarding',
    excerpt:
      'Watch Demo now highlights the logged-in ERP—live tracking, alerts, driver wallets, and AI— with captions and professional background music.',
  },
  {
    id: 'driver-wallet',
    date: 'Oct 20, 2026',
    category: 'Feature',
    title: 'Driver payment wallet & bulk cash-out approvals',
    excerpt:
      'Collections credit to in-app wallets; finance teams approve payouts with transparent fees and cadence controls in Nexus.',
  },
  {
    id: 'gps-alerts-marketing',
    date: 'Oct 12, 2026',
    category: 'Feature',
    title: 'GPS Alerts marketing pages aligned with ERP modules',
    excerpt:
      'Speed, device health, and safety story pages now mirror the alerts workspace dispatchers use inside Nexus.',
  },
  {
    id: 'sheikhgo-ai-tools',
    date: 'Sep 28, 2026',
    category: 'Company',
    title: 'SheikhGo AI stays tool-first for fleet operators',
    excerpt:
      'Assistants call operational tools—maps, trips, alerts, ERP records—so answers stay grounded in your tenant data.',
  },
];
