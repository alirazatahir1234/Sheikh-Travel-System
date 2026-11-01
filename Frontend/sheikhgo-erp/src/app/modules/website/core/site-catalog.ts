import { websiteAssetUrl } from './website-media';

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
  /** Optional — omit / null for a non-navigating mega-menu label. Never use login. */
  link: string | null;
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
    slug: 'vehicle-rental',
    title: 'Vehicle rental',
    text: 'Rates, bookings, availability, and fleet GPS.',
    icon: 'key',
    detail:
      'Rental operators use Nexus Vehicle Rental (rates, bookings, availability) plus fleet GPS, vehicles, customers, and trips — an operator ERP, not a consumer marketplace. See the Vehicle Rental marketing page for scope.',
    highlights: ['Rental rates & bookings', 'Availability checks', 'GPS for assets on rent', 'Operator ERP — not a public booking site'],
    exploreLink: '/vehicle-rental',
    exploreLabel: 'Vehicle rental',
  },
  {
    slug: 'sheikhgo-ai',
    title: 'SheikhGo AI',
    text: 'Tool-first assistants, digests, and copilots.',
    icon: 'auto_awesome',
    detail:
      'SheikhGo AI sits on Nexus and GPS data — assistants that call operational tools, heuristic fleet health, morning digests, and mobile copilots. Described transparently on the dedicated AI Solutions page; no black-box marketing claims.',
    highlights: ['Tool-first AI assistant', 'Fleet health & digests', 'WhatsApp assist (human-approved)', 'Optional LLM you configure'],
    exploreLink: '/ai-fleet-insights',
    exploreLabel: 'AI Fleet Insights',
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
  src: websiteAssetUrl('/website/nexus/fleet-intelligence-dashboard.jpg'),
  alt: 'SheikhGo Nexus fleet intelligence dashboard with live map, GPS alerts, feature cards, and AI assistant',
} as const;

/** Bump when replacing PNGs under website-images/nexus/modules/ (browser/CDN cache). */
export const NEXUS_MODULE_SHOTS_VERSION = '20261027';

const nexusModuleSrc = (file: string) =>
  websiteAssetUrl(`/website/nexus/modules/${file}?v=${NEXUS_MODULE_SHOTS_VERSION}`);

/** Product UI captures from SheikhGo Nexus (demo tenant). Shared by home and /services. */
/** Homepage feature modules — image + in-page fragment or route (pill nav + jump cards). */
export interface HomeModuleJump {
  labelKey: string;
  fragment?: string;
  link?: string;
  image: string;
  alt: string;
}

export const HOME_MODULE_JUMPS: readonly HomeModuleJump[] = [
  {
    labelKey: 'home.pill.modules',
    fragment: 'platform-modules',
    image: nexusModuleSrc('dashboard.png'),
    alt: 'Platform modules gallery in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.subscriptionPlans',
    fragment: 'subscription-plans',
    image: nexusModuleSrc('reports.png'),
    alt: 'Subscription plans in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.clientBilling',
    fragment: 'client-billing',
    image: nexusModuleSrc('customers.png'),
    alt: 'Client billing and customer records in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.driverPayments',
    fragment: 'driver-payments',
    image: nexusModuleSrc('drivers.png'),
    alt: 'Driver payments and wallet in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.smartAssignment',
    fragment: 'smart-assignment',
    image: nexusModuleSrc('trips.png'),
    alt: 'Smart trip assignment in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.liveTraffic',
    fragment: 'live-traffic',
    image: nexusModuleSrc('live-tracking.png'),
    alt: 'Live traffic and fleet map in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.driverVerification',
    fragment: 'driver-verification',
    image: nexusModuleSrc('vehicles.png'),
    alt: 'Driver verification workflow in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.driverVoice',
    fragment: 'driver-voice',
    image: nexusModuleSrc('ai-assistant.png'),
    alt: 'Driver voice commands in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.fatigueDetection',
    fragment: 'fatigue-detection',
    image: nexusModuleSrc('gps-alerts.png'),
    alt: 'Fatigue and safety alerts in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.driverOffline',
    fragment: 'driver-offline-sync',
    image: nexusModuleSrc('trip-history.png'),
    alt: 'Offline sync and trip history in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.capabilities',
    fragment: 'platform-features',
    image: nexusModuleSrc('geofencing.png'),
    alt: 'Platform capabilities in SheikhGo Nexus',
  },
  {
    labelKey: 'home.pill.ai',
    link: '/ai-solutions',
    image: nexusModuleSrc('ai-assistant.png'),
    alt: 'SheikhGo AI solutions',
  },
  {
    labelKey: 'home.pill.pricing',
    link: '/pricing',
    image: nexusModuleSrc('fuel.png'),
    alt: 'SheikhGo Nexus pricing',
  },
  {
    labelKey: 'home.pill.nexus',
    link: '/services',
    image: NEXUS_DASHBOARD_IMAGE.src,
    alt: NEXUS_DASHBOARD_IMAGE.alt,
  },
  {
    labelKey: 'home.pill.demo',
    fragment: 'cta',
    image: websiteAssetUrl('/website/gps-alerts/scene-tracking.jpg'),
    alt: 'Request a SheikhGo Nexus demo',
  },
] as const;

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

/** Phase 2 — Platform Overview cards (home). Links are existing Channel C / product routes only. */
export interface PlatformOverviewCardDef {
  id: string;
  icon: string;
  link: string;
  titleKey: string;
  textKey: string;
}

export const PLATFORM_OVERVIEW_CARDS: readonly PlatformOverviewCardDef[] = [
  {
    id: 'fleet',
    icon: 'local_shipping',
    link: '/fleet-management',
    titleKey: 'home.overview.card.fleet.title',
    textKey: 'home.overview.card.fleet.text',
  },
  {
    id: 'gps',
    icon: 'gps_fixed',
    link: '/gps-tracking',
    titleKey: 'home.overview.card.gps.title',
    textKey: 'home.overview.card.gps.text',
  },
  {
    id: 'rental',
    icon: 'key',
    link: '/vehicle-rental',
    titleKey: 'home.overview.card.rental.title',
    textKey: 'home.overview.card.rental.text',
  },
  {
    id: 'travel',
    icon: 'luggage',
    link: '/transportation-operations',
    titleKey: 'home.overview.card.travel.title',
    textKey: 'home.overview.card.travel.text',
  },
  {
    id: 'intelligence',
    icon: 'insights',
    link: '/ai-fleet-insights',
    titleKey: 'home.overview.card.intelligence.title',
    textKey: 'home.overview.card.intelligence.text',
  },
] as const;

/** Phase 2 — Product Showcase tabs (home). Visuals use real Nexus screenshots. */
export interface ProductShowcaseTabDef {
  id: string;
  tabKey: string;
  titleKey: string;
  leadKey: string;
  bulletKeys: readonly string[];
  image: string;
  imageAlt: string;
}

export const PRODUCT_SHOWCASE_TABS: readonly ProductShowcaseTabDef[] = [
  {
    id: 'fleet',
    tabKey: 'home.showcaseTabs.fleet.tab',
    titleKey: 'home.showcaseTabs.fleet.title',
    leadKey: 'home.showcaseTabs.fleet.lead',
    bulletKeys: [
      'home.showcaseTabs.fleet.b1',
      'home.showcaseTabs.fleet.b2',
      'home.showcaseTabs.fleet.b3',
      'home.showcaseTabs.fleet.b4',
    ],
    image: nexusModuleSrc('vehicles.png'),
    imageAlt: 'Vehicle and fleet registry in SheikhGo Nexus',
  },
  {
    id: 'gps',
    tabKey: 'home.showcaseTabs.gps.tab',
    titleKey: 'home.showcaseTabs.gps.title',
    leadKey: 'home.showcaseTabs.gps.lead',
    bulletKeys: [
      'home.showcaseTabs.gps.b1',
      'home.showcaseTabs.gps.b2',
      'home.showcaseTabs.gps.b3',
      'home.showcaseTabs.gps.b4',
    ],
    image: nexusModuleSrc('live-tracking.png'),
    imageAlt: 'Live GPS fleet map in SheikhGo Nexus',
  },
  {
    id: 'trips',
    tabKey: 'home.showcaseTabs.trips.tab',
    titleKey: 'home.showcaseTabs.trips.title',
    leadKey: 'home.showcaseTabs.trips.lead',
    bulletKeys: [
      'home.showcaseTabs.trips.b1',
      'home.showcaseTabs.trips.b2',
      'home.showcaseTabs.trips.b3',
      'home.showcaseTabs.trips.b4',
    ],
    image: nexusModuleSrc('trips.png'),
    imageAlt: 'Trip operations workspace in SheikhGo Nexus',
  },
  {
    id: 'rental',
    tabKey: 'home.showcaseTabs.rental.tab',
    titleKey: 'home.showcaseTabs.rental.title',
    leadKey: 'home.showcaseTabs.rental.lead',
    bulletKeys: [
      'home.showcaseTabs.rental.b1',
      'home.showcaseTabs.rental.b2',
      'home.showcaseTabs.rental.b3',
      'home.showcaseTabs.rental.b4',
    ],
    image: nexusModuleSrc('bookings.png'),
    imageAlt: 'Bookings and rental operations in SheikhGo Nexus',
  },
  {
    id: 'analytics',
    tabKey: 'home.showcaseTabs.analytics.tab',
    titleKey: 'home.showcaseTabs.analytics.title',
    leadKey: 'home.showcaseTabs.analytics.lead',
    bulletKeys: [
      'home.showcaseTabs.analytics.b1',
      'home.showcaseTabs.analytics.b2',
      'home.showcaseTabs.analytics.b3',
      'home.showcaseTabs.analytics.b4',
    ],
    image: nexusModuleSrc('reports.png'),
    imageAlt: 'Operational reports and analytics in SheikhGo Nexus',
  },
] as const;

export const NEXUS_VALUE_PILLARS = [
  { title: 'One platform', text: 'GPS, operations, and AI in a single tenant — fewer integrations to babysit.', icon: 'layers' },
  { title: 'Built for operators', text: 'Workflows for dispatch, maintenance, and finance — not a tracker bolt-on.', icon: 'engineering' },
  { title: 'Premium product UI', text: 'Polished Nexus workspace with module depth you can demo today.', icon: 'verified' },
  { title: 'Transparent AI', text: 'Assistants use your tools; claims match what ships.', icon: 'policy' },
] as const;

/** Phase 3 — Business Benefits list (home). */
export interface BusinessBenefitDef {
  id: string;
  icon: string;
  titleKey: string;
  textKey: string;
}

export const BUSINESS_BENEFITS: readonly BusinessBenefitDef[] = [
  {
    id: 'visibility',
    icon: 'map',
    titleKey: 'home.benefits.item.visibility.title',
    textKey: 'home.benefits.item.visibility.text',
  },
  {
    id: 'operations',
    icon: 'account_tree',
    titleKey: 'home.benefits.item.operations.title',
    textKey: 'home.benefits.item.operations.text',
  },
  {
    id: 'decisions',
    icon: 'monitoring',
    titleKey: 'home.benefits.item.decisions.title',
    textKey: 'home.benefits.item.decisions.text',
  },
  {
    id: 'connected',
    icon: 'hub',
    titleKey: 'home.benefits.item.connected.title',
    textKey: 'home.benefits.item.connected.text',
  },
] as const;

/** Phase 3 — Industries We Serve cards (home). Links are existing routes only. */
export interface IndustryServeCardDef {
  id: string;
  icon: string;
  link: string;
  titleKey: string;
  textKey: string;
}

export const INDUSTRY_SERVE_CARDS: readonly IndustryServeCardDef[] = [
  {
    id: 'logistics',
    icon: 'local_shipping',
    link: '/logistics-fleet',
    titleKey: 'home.industriesServe.card.logistics.title',
    textKey: 'home.industriesServe.card.logistics.text',
  },
  {
    id: 'rental',
    icon: 'key',
    link: '/vehicle-rental',
    titleKey: 'home.industriesServe.card.rental.title',
    textKey: 'home.industriesServe.card.rental.text',
  },
  {
    id: 'travel',
    icon: 'flight_takeoff',
    link: '/transportation-operations',
    titleKey: 'home.industriesServe.card.travel.title',
    textKey: 'home.industriesServe.card.travel.text',
  },
  {
    id: 'enterprise',
    icon: 'apartment',
    link: '/fleet-management',
    titleKey: 'home.industriesServe.card.enterprise.title',
    textKey: 'home.industriesServe.card.enterprise.text',
  },
  {
    id: 'transport',
    icon: 'route',
    link: '/transportation-operations',
    titleKey: 'home.industriesServe.card.transport.title',
    textKey: 'home.industriesServe.card.transport.text',
  },
  {
    id: 'field',
    icon: 'near_me',
    link: '/gps-tracking',
    titleKey: 'home.industriesServe.card.field.title',
    textKey: 'home.industriesServe.card.field.text',
  },
] as const;

/** Phase 3 — Benefits visual screenshots (illustrative product UI). */
export const BUSINESS_BENEFITS_VISUAL = {
  primary: {
    src: NEXUS_DASHBOARD_IMAGE.src,
    alt: NEXUS_DASHBOARD_IMAGE.alt,
  },
  map: {
    src: nexusModuleSrc('live-tracking.png'),
    alt: 'Live GPS map panel in SheikhGo Nexus',
  },
  reports: {
    src: nexusModuleSrc('reports.png'),
    alt: 'Operational reports panel in SheikhGo Nexus',
  },
} as const;

export const INDUSTRY_ITEMS: readonly IndustryItem[] = [
  {
    i18nKey: 'logistics-fleet',
    title: 'Logistics & Fleet',
    text: 'Vehicles, drivers, GPS, fuel, maintenance and compliance in one ops platform.',
    icon: 'local_shipping',
    link: '/logistics-fleet',
    fragment: null,
  },
  {
    i18nKey: 'travel-tourism',
    title: 'Travel & Tourism',
    text: 'Bookings, vehicles and passenger operations end to end.',
    icon: 'flight_takeoff',
    link: '/transportation-operations',
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
    title: 'Fleet Management Software',
    text: 'Vehicles, drivers, trips, fuel, maintenance and dashboards.',
    icon: 'local_shipping',
    link: '/fleet-management',
  },
  {
    title: 'GPS Vehicle Tracking',
    text: 'Live maps, geofences, trip history and device management.',
    icon: 'gps_fixed',
    link: '/gps-tracking',
  },
  {
    title: 'Vehicle Rental Management',
    text: 'Rental rates, bookings, availability, plus fleet GPS in Nexus.',
    icon: 'key',
    link: '/vehicle-rental',
  },
  {
    title: 'Transportation Operations',
    text: 'Bookings, trips, drivers and live GPS for ground transport.',
    icon: 'airport_shuttle',
    link: '/transportation-operations',
  },
  {
    title: 'AI Fleet Insights',
    text: 'Tool-first assistants, digests and fleet health signals.',
    icon: 'insights',
    link: '/ai-fleet-insights',
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
    text: 'Full AI capability inventory on Nexus.',
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
