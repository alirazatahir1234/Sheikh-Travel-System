import { WEBSITE_BRAND } from './brand';

/** Anchor targets on /services — SheikhGo Nexus portfolio navigation. */
export interface NexusPortfolioItem {
  slug: string;
  title: string;
  text: string;
  icon: string;
  detail: string;
  highlights: readonly string[];
  /** Optional product page or home fragment */
  exploreLink?: string;
  exploreFragment?: string | null;
  exploreLabel?: string;
}

export interface IndustryItem {
  /** Key under `industry.{i18nKey}` in website i18n dictionaries. */
  i18nKey: string;
  title: string;
  text: string;
  icon: string;
  link: string;
  fragment: string | null;
}

/** @deprecated Use NEXUS_PORTFOLIO_ITEMS — kept as alias for header/footer imports. */
export type ServiceItem = NexusPortfolioItem;

/** Header mega-menu, footer column, and /services jump links. */
export const NEXUS_PORTFOLIO_ITEMS: readonly NexusPortfolioItem[] = [
  {
    slug: 'nexus-overview',
    title: 'SheikhGo Nexus',
    text: 'Flagship fleet & travel operations platform.',
    icon: 'hub',
    detail:
      'SheikhGo Nexus is the product Ali builds and ships for transport operators — not a generic dev shop brochure. One multi-tenant workspace for GPS, fleet, trips, fuel, maintenance, bookings, reports, and notifications, with optional SheikhGo AI on top of live operational tools.',
    highlights: [
      'Multi-tenant SaaS ready for your fleet',
      'GPS + ERP depth in one sign-in',
      'Honest, operator-first product copy',
      'Demo tenant UI you can walk through today',
    ],
    exploreLink: '/request-demo',
    exploreLabel: 'Request a demo',
  },
  {
    slug: 'fleet-intelligence',
    title: 'Fleet intelligence dashboard',
    text: 'Unified NEXUS map, alerts, and AI panel.',
    icon: 'dashboard',
    detail:
      'The NEXUS dashboard is the centerpiece: live fleet map, recent GPS alerts, geofences, trip playback, fleet health signals, and an assistant panel that answers from connected tools — the same experience highlighted on the home page.',
    highlights: ['Live map & trip state', 'Alert inbox & geofences', 'Fleet health at a glance', 'AI panel beside operations'],
    exploreLink: '/',
    exploreFragment: 'see-the-platform',
    exploreLabel: 'See home showcase',
  },
  {
    slug: 'gps-tracking-suite',
    title: 'GPS & tracking suite',
    text: 'Live tracking, geofencing, playback, and alerts.',
    icon: 'gps_fixed',
    detail:
      'Device registry, live positions, geofence editor, trip history with playback, and alert routing — designed for dispatchers and compliance teams who need evidence, not vanity metrics.',
    highlights: ['Live tracking map', 'Geofence entry & exit', 'Route playback & stops', 'Speed, fuel & device alerts'],
    exploreLink: '/gps-tracking',
    exploreLabel: 'GPS Tracking',
  },
  {
    slug: 'operations-modules',
    title: 'Operations modules',
    text: 'Vehicles, drivers, trips, fuel, and maintenance.',
    icon: 'local_shipping',
    detail:
      'Day-to-day fleet operations live in Nexus: vehicle and driver records, trip lifecycle, fuel logs, maintenance schedules, customers, and operational reports — modules appear based on what your tenant enables.',
    highlights: ['Vehicle & driver registry', 'Trip operations', 'Fuel & maintenance', 'Reports & customers'],
    exploreLink: '/fleet-management',
    exploreLabel: 'Fleet management',
  },
  {
    slug: 'sheikhgo-ai',
    title: 'SheikhGo AI',
    text: 'Tool-first assistants, digests, and copilots.',
    icon: 'auto_awesome',
    detail:
      'SheikhGo AI sits on Nexus and GPS data — assistants that call operational tools, heuristic fleet health, morning digests, and mobile copilots. Described transparently on the dedicated AI Solutions page; no black-box marketing claims.',
    highlights: ['Tool-first AI assistant', 'Fleet health & digests', 'WhatsApp assist (human-approved)', 'Optional LLM you configure'],
    exploreLink: '/ai-solutions',
    exploreLabel: 'AI Solutions',
  },
  {
    slug: 'platform-modules',
    title: 'Product UI gallery',
    text: 'Fourteen modules captured from the live app.',
    icon: 'view_module',
    detail:
      'Screenshots below are from the Nexus workspace your team signs into — the same module set showcased on the home page under Platform modules.',
    highlights: ['Dashboard & live map', 'Alerts, geofences & history', 'Fuel, maintenance & trips', 'AI assistant & notifications'],
    exploreLink: '/',
    exploreFragment: 'platform-modules',
    exploreLabel: 'See on homepage',
  },
];

/** Alias for layout components that still import SERVICE_ITEMS. */
export const SERVICE_ITEMS: readonly NexusPortfolioItem[] = NEXUS_PORTFOLIO_ITEMS;

export const NEXUS_DASHBOARD_IMAGE = {
  src: '/website/nexus/fleet-intelligence-dashboard.jpg',
  alt: 'SheikhGo Nexus fleet intelligence dashboard with live map, GPS alerts, feature cards, and AI assistant',
} as const;

/** Bump when replacing PNGs under public/website/nexus/modules/ (browser/CDN cache). */
export const NEXUS_MODULE_SHOTS_VERSION = '20261027';

const nexusModuleSrc = (file: string) =>
  `/website/nexus/modules/${file}?v=${NEXUS_MODULE_SHOTS_VERSION}` as const;

/** Product UI captures from SheikhGo Nexus (demo tenant). Shared by home and /services. */
export const NEXUS_MODULE_SHOTS = [
  { caption: 'Dashboard', src: nexusModuleSrc('dashboard.png'), alt: 'SheikhGo Nexus operations dashboard' },
  { caption: 'Live Tracking', src: nexusModuleSrc('live-tracking.png'), alt: 'Live GPS fleet map in SheikhGo Nexus' },
  { caption: 'GPS Alerts', src: nexusModuleSrc('gps-alerts.png'), alt: 'GPS alerts inbox in SheikhGo Nexus' },
  { caption: 'Geofencing', src: nexusModuleSrc('geofencing.png'), alt: 'Geofence editor in SheikhGo Nexus' },
  { caption: 'Trip History', src: nexusModuleSrc('trip-history.png'), alt: 'GPS trip history and playback in SheikhGo Nexus' },
  { caption: 'Vehicles', src: nexusModuleSrc('vehicles.png'), alt: 'Vehicle registry in SheikhGo Nexus' },
  { caption: 'Drivers', src: nexusModuleSrc('drivers.png'), alt: 'Driver management in SheikhGo Nexus' },
  { caption: 'Trips', src: nexusModuleSrc('trips.png'), alt: 'Trip operations in SheikhGo Nexus' },
  { caption: 'Fuel', src: nexusModuleSrc('fuel.png'), alt: 'Fuel logs in SheikhGo Nexus' },
  { caption: 'Maintenance', src: nexusModuleSrc('maintenance.png'), alt: 'Fleet maintenance in SheikhGo Nexus' },
  { caption: 'Reports', src: nexusModuleSrc('reports.png'), alt: 'Operational reports in SheikhGo Nexus' },
  { caption: 'AI Assistant', src: nexusModuleSrc('ai-assistant.png'), alt: 'SheikhGo AI assistant in Nexus' },
  { caption: 'Customers', src: nexusModuleSrc('customers.png'), alt: 'Customer records in SheikhGo Nexus' },
  { caption: 'Notifications', src: nexusModuleSrc('notifications.png'), alt: 'Notification center in SheikhGo Nexus' },
] as const;

export const NEXUS_VALUE_PILLARS = [
  { title: 'One platform', text: 'GPS, operations, and AI in a single tenant — fewer integrations to babysit.', icon: 'layers' },
  { title: 'Built for operators', text: 'Workflows for dispatch, maintenance, and finance — not a tracker bolt-on.', icon: 'engineering' },
  { title: 'Premium product UI', text: 'Polished Nexus workspace with module depth you can demo today.', icon: 'verified' },
  { title: 'Transparent AI', text: 'Assistants use your tools; claims match what ships.', icon: 'policy' },
] as const;

export const INDUSTRY_ITEMS: readonly IndustryItem[] = [
  {
    i18nKey: 'logistics-fleet',
    title: 'Logistics & Fleet',
    text: 'Fleet, GPS tracking and trip operations in one platform.',
    icon: 'local_shipping',
    link: WEBSITE_BRAND.loginPath,
    fragment: null,
  },
  {
    i18nKey: 'travel-tourism',
    title: 'Travel & Tourism',
    text: 'Bookings, vehicles and passenger operations end to end.',
    icon: 'flight_takeoff',
    link: WEBSITE_BRAND.loginPath,
    fragment: null,
  },
  {
    i18nKey: 'healthcare',
    title: 'Healthcare',
    text: 'Patient transport, scheduling and compliant record keeping.',
    icon: 'local_hospital',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'retail-ecommerce',
    title: 'Retail & E-Commerce',
    text: 'Storefronts, delivery tracking and inventory integrations.',
    icon: 'shopping_cart',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'finance',
    title: 'Finance',
    text: 'Secure portals, reporting and process automation.',
    icon: 'account_balance',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'education',
    title: 'Education',
    text: 'Student transport, campus systems and learning portals.',
    icon: 'school',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'real-estate',
    title: 'Real Estate',
    text: 'Listing platforms, CRM integration and field operations.',
    icon: 'apartment',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'manufacturing',
    title: 'Manufacturing',
    text: 'Plant logistics, asset tracking and operations dashboards.',
    icon: 'precision_manufacturing',
    link: '/contact',
    fragment: null,
  },
  {
    i18nKey: 'enterprise-operations',
    title: 'Enterprise & Operations',
    text: 'Cross-department workflows, dashboards and custom integrations.',
    icon: 'business_center',
    link: '/contact',
    fragment: null,
  },
];

export interface ProductItem {
  title: string;
  text: string;
  icon: string;
  link: string;
}

export const PRODUCT_ITEMS: readonly ProductItem[] = [
  {
    title: 'SheikhGo Fleet',
    text: 'Fleet management, drivers, trips and operational dashboards.',
    icon: 'local_shipping',
    link: '/fleet-management',
  },
  {
    title: 'GPS Tracking',
    text: 'Live maps, geofences, trip history and device management.',
    icon: 'gps_fixed',
    link: '/gps-tracking',
  },
  {
    title: 'Live Tracking',
    text: 'Real-time vehicle positions, speed and connectivity.',
    icon: 'my_location',
    link: '/live-tracking',
  },
  {
    title: 'GPS Alerts',
    text: 'Speed, geofence, fuel and device event notifications.',
    icon: 'notifications_active',
    link: '/gps-alerts',
  },
  {
    title: 'Fuel Management',
    text: 'OBD fuel level, station-stop alerts, receipts, and km/L reports.',
    icon: 'local_gas_station',
    link: '/fuel-management',
  },
  {
    title: 'AI Solutions',
    text: 'Assistant, digests and fleet intelligence tools.',
    icon: 'auto_awesome',
    link: '/ai-solutions',
  },
  {
    title: 'All Features',
    text: 'Overview of platform capabilities and modules.',
    icon: 'grid_view',
    link: '/features',
  },
];
