/**
 * Channel C — website / SEO solution pages (product-truth copy).
 * No fabricated customers, MRR, or case studies.
 */

import { websiteAssetUrl } from './website-media';

export interface SolutionShot {
  src: string;
  alt: string;
  title: string;
  text: string;
}

export interface SolutionFeature {
  title: string;
  text: string;
  icon?: string;
}

export interface SolutionUseCase {
  title: string;
  text: string;
  icon?: string;
}

export interface SolutionBenefit {
  title: string;
  text: string;
  icon?: string;
}

export interface ChannelCSolutionPage {
  /** URL path without origin, e.g. /fleet-management */
  path: string;
  /** SEO title (brand appended by WebsiteSeoService when no "|") */
  seoTitle: string;
  seoDescription: string;
  kicker: string;
  title: string;
  lead: string;
  heroImage: string;
  targetCustomer: {
    title: string;
    lead: string;
    bullets: readonly string[];
  };
  problem: {
    title: string;
    lead: string;
    bullets: readonly string[];
  };
  featuresTitle: string;
  featuresLead: string;
  features: readonly SolutionFeature[];
  /** Optional industry use-case cards (e.g. Logistics & Fleet). */
  useCasesTitle?: string;
  useCasesLead?: string;
  useCases?: readonly SolutionUseCase[];
  /** Optional operator benefits strip. */
  benefitsTitle?: string;
  benefitsLead?: string;
  benefits?: readonly SolutionBenefit[];
  nextStep: {
    title: string;
    lead: string;
  };
  /** Optional honesty / roadmap note (e.g. rental module). */
  honestyNote?: string;
  /** Optional product screenshots tour */
  shots?: readonly SolutionShot[];
  shotsTitle?: string;
  shotsLead?: string;
}

export const CHANNEL_C_CTAS = {
  primary: { text: 'Book a Demo', url: '/request-demo' },
  secondary: { text: 'Request a Fleet Assessment', url: '/fleet-assessment' },
} as const;

export const FLEET_MANAGEMENT_SOLUTION: ChannelCSolutionPage = {
  path: '/fleet-management',
  seoTitle: 'Fleet Management Software',
  seoDescription:
    'SheikhGo Nexus fleet management software for vehicles, drivers, trips, fuel, maintenance, and live GPS — built for transport operators in the GCC and beyond.',
  kicker: 'Fleet management software',
  title: 'Fleet Management Software',
  lead: 'Run vehicles, drivers, assignments, trips, fuel, and maintenance from one SheikhGo Nexus workspace — with live GPS when devices are connected.',
  heroImage: websiteAssetUrl('/website/sheikhgo-fleet-hero.png'),
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'B2B operators who need operational control — not a consumer tracker app.',
    bullets: [
      'Logistics and fleet operators',
      'Travel and ground-transport companies',
      'Corporate fleets and staff transport',
      'Vehicle rental businesses preparing for deeper rental workflows',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'Spreadsheets, radio calls, and disconnected tools leave managers guessing about utilisation, compliance, and cost.',
    bullets: [
      'No single view of vehicles, drivers, and trip status',
      'Assignments and ownership unclear across shifts',
      'Fuel and maintenance costs hard to reconcile',
      'GPS and ops data live in separate systems',
    ],
  },
  featuresTitle: 'Available in SheikhGo Nexus',
  featuresLead: 'Modules your team can walk through in a demo today — enabled per tenant.',
  features: [
    { title: 'Fleet dashboard', text: 'Operational overview of vehicles, drivers, online status, trips, and alerts.', icon: 'dashboard' },
    { title: 'Vehicles & documents', text: 'Registry with plates, status, GPS device linkage, and document records.', icon: 'directions_car' },
    { title: 'Drivers & assignments', text: 'Profiles, duty status, and clear vehicle/driver ownership.', icon: 'badge' },
    { title: 'Trips & bookings', text: 'Plan, monitor, and close trips with GPS history behind each job.', icon: 'route' },
    { title: 'Fuel & maintenance', text: 'Fuel logs, service schedules, and cost visibility for managers.', icon: 'build' },
    { title: 'Alerts & reports', text: 'Actionable GPS and ops alerts plus utilisation and distance reports.', icon: 'assessment' },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo of SheikhGo Nexus on your fleet size and workflows — or request a fleet assessment so we can map gaps before a pilot.',
  },
  shotsTitle: 'Inside the platform',
  shotsLead: 'Screens from SheikhGo Nexus — not mockups.',
  shots: [
    {
      src: websiteAssetUrl('/website/product/live-map.png'),
      alt: 'SheikhGo Fleet live GPS map showing tracked vehicles',
      title: 'Live GPS map',
      text: 'Every vehicle on one map with online state, speed and last update.',
    },
    {
      src: websiteAssetUrl('/website/product/dashboard.png'),
      alt: 'SheikhGo Fleet operations dashboard',
      title: 'Operations dashboard',
      text: 'Fleet health, active trips, alerts and utilisation at a glance.',
    },
    {
      src: websiteAssetUrl('/website/product/vehicles.png'),
      alt: 'SheikhGo Fleet vehicle register',
      title: 'Vehicle register',
      text: 'Plates, status, documents and the GPS device linked to each vehicle.',
    },
    {
      src: websiteAssetUrl('/website/product/trips.png'),
      alt: 'SheikhGo Fleet trip management screen',
      title: 'Trips',
      text: 'Plan, monitor and close trips with full GPS history behind each one.',
    },
    {
      src: websiteAssetUrl('/website/product/drivers.png'),
      alt: 'SheikhGo Fleet driver management screen',
      title: 'Drivers',
      text: 'Profiles, assignments, duty status and activity history.',
    },
    {
      src: websiteAssetUrl('/website/product/reports.png'),
      alt: 'SheikhGo Fleet reports and analytics',
      title: 'Reports',
      text: 'Utilisation, distance, fuel and cost reporting for managers.',
    },
  ],
};

export const GPS_TRACKING_SOLUTION: ChannelCSolutionPage = {
  path: '/gps-tracking',
  seoTitle: 'GPS Vehicle Tracking',
  seoDescription:
    'GPS vehicle tracking with live maps, human-readable addresses, geofences, trip playback, and GPS alerts — part of SheikhGo Nexus fleet operations.',
  kicker: 'GPS vehicle tracking',
  title: 'GPS Vehicle Tracking',
  lead: 'Know where each vehicle is — with live status, road-level addresses, journey playback, geofences, and alerts that reach your team.',
  heroImage: websiteAssetUrl('/website/gps-alerts/hero-bg.jpg'),
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'Dispatchers, fleet owners, and compliance teams who need evidence — not vanity pins.',
    bullets: [
      'Fleet operators with OBD / GPS devices (e.g. Traccar-connected fleets)',
      'Travel companies monitoring passenger trips',
      'Rental operators watching assets between handovers',
      'Managers who need speed, geofence, and device health alerts',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'Calling drivers for location wastes time and still leaves gaps when something goes wrong on the road.',
    bullets: [
      'No shared live map for the whole fleet',
      'Coordinates without street context confuse desk staff',
      'Missed speeding, zone exit, or device offline events',
      'Hard to replay a disputed trip after the fact',
    ],
  },
  featuresTitle: 'Tracking capabilities',
  featuresLead: 'GPS depth that sits beside fleet operations in the same Nexus sign-in.',
  features: [
    { title: 'Live positions', text: 'Online / offline / moving / idle / parked with speed, ignition, and last update.', icon: 'my_location' },
    { title: 'Human-readable addresses', text: 'Road and area context so operators see where the vehicle is in plain language.', icon: 'place' },
    { title: 'History & playback', text: 'Trip history, stops, parking, and route playback for reviews.', icon: 'history' },
    { title: 'Geofencing', text: 'Zone entry and exit for yards, airports, customer sites, and restricted areas.', icon: 'fence' },
    { title: 'GPS alerts', text: 'Speed, SOS, ignition, fuel, device health, and more — see the GPS Alerts page.', icon: 'notifications_active' },
    { title: 'Device registry', text: 'Link vehicles to tracking devices and keep connectivity visible.', icon: 'sensors' },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo to see live tracking and alerts on a sample fleet — or request a fleet assessment covering device coverage and alert priorities.',
  },
};

export const VEHICLE_RENTAL_SOLUTION: ChannelCSolutionPage = {
  path: '/vehicle-rental',
  seoTitle: 'Vehicle Rental Management',
  seoDescription:
    'Vehicle rental management with SheikhGo Nexus — rental rates, bookings, availability, fleet GPS, and ops for rental fleets (operator ERP, not a consumer marketplace).',
  kicker: 'Vehicle rental',
  title: 'Vehicle Rental Management',
  lead: 'Run rental rates, bookings, and availability inside SheikhGo Nexus — alongside fleet GPS, vehicles, customers, and maintenance for every asset on rent.',
  heroImage: websiteAssetUrl('/website/product/vehicles.png'),
  honestyNote:
    'Honest status: Nexus includes an ERP Vehicle Rental module (rates, bookings, availability, payments) plus fleet GPS and ops. This is an operator workspace for rental fleets — not a consumer car-rental marketplace or white-label public booking site.',
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'Car rental and fleet rental companies that need asset control before and after every handover.',
    bullets: [
      'Car rental and luxury rental fleets',
      'Commercial / utility vehicle rental',
      'Operators mixing rental with staff or tourism transport',
      'Teams that want GPS proof of where assets were between contracts',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'Rental desks lose money when vehicles go dark, handovers lack evidence, or rates and bookings live outside the fleet system.',
    bullets: [
      'Unclear vehicle availability vs. “out on rent”',
      'No GPS trail for misuse, late returns, or disputes',
      'Customer and payment context scattered across tools',
      'Ops teams juggling rental paperwork separately from live tracking',
    ],
  },
  featuresTitle: 'What you can demo in Nexus',
  featuresLead: 'Product truth: rental ERP workflows plus the fleet/GPS stack rental operators already need.',
  features: [
    { title: 'Rental rates', text: 'Per-vehicle rate cards, deposits, and currency for how you price rentals.', icon: 'payments' },
    { title: 'Rental bookings', text: 'Rental numbers, customer linkage, lifecycle status, and payment recording.', icon: 'event_available' },
    { title: 'Availability checks', text: 'See whether a vehicle is free for a requested rental window.', icon: 'event' },
    { title: 'Fleet & GPS', text: 'Vehicle registry, live tracking, geofences, and alerts for assets on rent or idle.', icon: 'gps_fixed' },
    { title: 'Customers & trips', text: 'Customer records and trip operations for transfers and related jobs.', icon: 'groups' },
    { title: 'Fuel & maintenance', text: 'Keep rental units serviceable with fuel logs and maintenance schedules.', icon: 'local_gas_station' },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo of Vehicle Rental + fleet GPS in SheikhGo Nexus. Prefer a structured review first? Request a fleet assessment.',
  },
};

export const LOGISTICS_FLEET_SOLUTION: ChannelCSolutionPage = {
  path: '/logistics-fleet',
  seoTitle: 'Logistics & Fleet Software',
  seoDescription:
    'Logistics and fleet operations on SheikhGo Nexus — vehicles, drivers, assignments, GPS tracking, trip history, fuel, maintenance, alerts, and compliance for operators in Pakistan and the Gulf.',
  kicker: 'Industry · Logistics & fleet',
  title: 'Logistics & Fleet',
  lead:
    'One SheikhGo Nexus workspace for fleet inventory, drivers, assignments, live GPS, trip history, fuel, maintenance, alerts, and compliance — built for logistics and transport operators across Pakistan and the Gulf.',
  heroImage: websiteAssetUrl('/website/sheikhgo-fleet-hero.png'),
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'Operators who move goods or people at scale and need desk-to-road control — not a consumer tracker.',
    bullets: [
      'Logistics companies and freight / last-mile fleets',
      'Corporate and staff-transport fleets',
      'Regional operators with mixed vehicle types and shifts',
      'Fleet managers who need GPS evidence beside ERP records',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'When inventory, assignments, GPS, fuel, and maintenance live in separate tools, utilisation and cost stay guesswork.',
    bullets: [
      'No single register of vehicles, drivers, and who owns each shift',
      'Live location and trip history disconnected from ops records',
      'Fuel and workshop costs hard to reconcile against distance driven',
      'Alerts and document expiry buried until something goes wrong',
    ],
  },
  featuresTitle: 'What logistics teams run on Nexus',
  featuresLead:
    'Product capabilities you can walk through in a demo — enabled per tenant, grounded in real Nexus modules.',
  features: [
    {
      title: 'Fleet inventory',
      text: 'Vehicle registry with plates, status, documents, and GPS device linkage in one place.',
      icon: 'directions_car',
    },
    {
      title: 'Drivers & assignments',
      text: 'Driver profiles, duty status, and clear vehicle/driver ownership across shifts.',
      icon: 'badge',
    },
    {
      title: 'Live GPS & trip history',
      text: 'Live positions, geofences, playback, and trip trails for reviews and disputes.',
      icon: 'gps_fixed',
    },
    {
      title: 'Fuel & maintenance',
      text: 'Fuel logs, service schedules, and cost visibility so workshop and road stay aligned.',
      icon: 'build',
    },
    {
      title: 'Alerts & compliance',
      text: 'Speed, geofence, device, and ops alerts — plus document and compliance tracking for the fleet.',
      icon: 'notifications_active',
    },
    {
      title: 'Operations dashboard',
      text: 'Fleet health, active trips, utilisation, and alert inbox for managers starting the day.',
      icon: 'dashboard',
    },
  ],
  useCasesTitle: 'Where operators use it',
  useCasesLead: 'Typical logistics and fleet workflows SheikhGo Nexus is built to support.',
  useCases: [
    {
      title: 'Daily dispatch & ownership',
      text: 'Know which driver is on which vehicle, what is on the road, and what still needs assignment.',
      icon: 'assignment_ind',
    },
    {
      title: 'Prove the trip after the fact',
      text: 'Replay GPS history and trip state when a customer, insurer, or manager asks what happened.',
      icon: 'history',
    },
    {
      title: 'Control fuel and workshop spend',
      text: 'Tie fuel logs and maintenance schedules to the same fleet register your dispatchers already use.',
      icon: 'local_gas_station',
    },
    {
      title: 'Stay ahead of compliance gaps',
      text: 'Surface document and alert issues before a vehicle goes out with expired papers or a silent device.',
      icon: 'verified_user',
    },
  ],
  benefitsTitle: 'Why logistics teams choose Nexus',
  benefitsLead: 'Practical outcomes from running fleet ops and GPS in one sign-in — not a stack of disconnected tools.',
  benefits: [
    {
      title: 'One operational picture',
      text: 'Vehicles, drivers, trips, GPS, fuel, and maintenance share the same tenant workspace.',
      icon: 'hub',
    },
    {
      title: 'Evidence when you need it',
      text: 'Live positions, geofences, and trip playback support reviews and dispute handling.',
      icon: 'map',
    },
    {
      title: 'Lower day-to-day friction',
      text: 'Fewer radio chases and spreadsheet reconciliations for location, duty, and cost questions.',
      icon: 'speed',
    },
    {
      title: 'Built for regional operators',
      text: 'Multi-tenant SaaS oriented to Pakistan and Gulf transport fleets — demoable on real Nexus modules.',
      icon: 'public',
    },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo focused on logistics workflows — inventory, assignments, GPS, fuel, and maintenance — or request a fleet assessment of your current stack.',
  },
  shotsTitle: 'Inside logistics ops',
  shotsLead: 'Screens from SheikhGo Nexus — the same modules your team would run.',
  shots: [
    {
      src: websiteAssetUrl('/website/product/live-map.png'),
      alt: 'SheikhGo Nexus live GPS map for logistics fleets',
      title: 'Live GPS map',
      text: 'See the whole fleet with online state, speed, and last update.',
    },
    {
      src: websiteAssetUrl('/website/product/dashboard.png'),
      alt: 'SheikhGo Nexus fleet operations dashboard',
      title: 'Fleet dashboard',
      text: 'Active trips, alerts, and utilisation at a glance for dispatch and managers.',
    },
    {
      src: websiteAssetUrl('/website/product/vehicles.png'),
      alt: 'SheikhGo Nexus vehicle inventory register',
      title: 'Vehicle inventory',
      text: 'Plates, status, documents, and the GPS device linked to each asset.',
    },
    {
      src: websiteAssetUrl('/website/product/drivers.png'),
      alt: 'SheikhGo Nexus driver and assignment management',
      title: 'Drivers & assignments',
      text: 'Profiles, duty status, and assignment history for who is on which vehicle.',
    },
    {
      src: websiteAssetUrl('/website/product/trips.png'),
      alt: 'SheikhGo Nexus trip history for logistics jobs',
      title: 'Trips & history',
      text: 'Plan, monitor, and close jobs with GPS history behind each trip.',
    },
    {
      src: websiteAssetUrl('/website/product/reports.png'),
      alt: 'SheikhGo Nexus fleet reports for logistics managers',
      title: 'Reports',
      text: 'Distance, utilisation, fuel, and operational reports for managers.',
    },
  ],
};

export const TRANSPORTATION_OPS_SOLUTION: ChannelCSolutionPage = {
  path: '/transportation-operations',
  seoTitle: 'Transportation Operations',
  seoDescription:
    'Transportation operations software for bookings, trips, drivers, vehicles, and live GPS — SheikhGo Nexus for travel and ground-transport teams.',
  kicker: 'Transportation operations',
  title: 'Transportation Operations',
  lead: 'Coordinate bookings, trips, drivers, and vehicles in one operations platform — with live tracking when your fleet is on the road.',
  heroImage: websiteAssetUrl('/website/product/trips.png'),
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'Travel agencies, transport companies, and operators who move people or goods on a schedule.',
    bullets: [
      'Travel and tourism ground transport',
      'Staff and corporate shuttle operators',
      'Logistics teams running planned trips',
      'Multi-tenant agencies that need company-level control',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'Dispatch without shared trip state creates no-shows, double-assignments, and weak customer communication.',
    bullets: [
      'Bookings disconnected from vehicle and driver assignment',
      'Trip status unclear between desk and road',
      'Hard to prove what happened on a completed job',
      'Reports assembled manually after the shift',
    ],
  },
  featuresTitle: 'Operations modules',
  featuresLead: 'Day-to-day transport workflows that sit beside GPS in SheikhGo Nexus.',
  features: [
    { title: 'Bookings', text: 'Capture and manage passenger or job bookings in the operations workspace.', icon: 'book_online' },
    { title: 'Trip lifecycle', text: 'Assign, start, monitor, and complete trips with operational history.', icon: 'local_shipping' },
    { title: 'Driver mobile app', text: 'Drivers see assigned trips and update status from the SheikhGo mobile apps.', icon: 'smartphone' },
    { title: 'Live tracking', text: 'Follow active trips on the fleet map with connectivity and speed context.', icon: 'map' },
    { title: 'Customers', text: 'Keep customer records close to bookings and trips.', icon: 'person' },
    { title: 'Reports', text: 'Operational reporting for managers — distance, utilisation, and activity.', icon: 'summarize' },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo focused on bookings → assignment → trip → GPS. Or request a fleet assessment of your current dispatch process.',
  },
};

export const AI_FLEET_INSIGHTS_SOLUTION: ChannelCSolutionPage = {
  path: '/ai-fleet-insights',
  seoTitle: 'AI Fleet Insights',
  seoDescription:
    'AI fleet insights on SheikhGo Nexus — tool-first assistants, fleet health heuristics, digests, and copilots grounded in your GPS and operations data.',
  kicker: 'AI fleet insights',
  title: 'AI Fleet Insights',
  lead: 'Ask questions and surface fleet health signals from the same GPS and operations data your team already runs — transparent tools, not black-box marketing claims.',
  heroImage: websiteAssetUrl('/website/nexus/modules/ai-assistant.png'),
  honestyNote:
    'SheikhGo AI uses operational tools and heuristics on your tenant data. Optional language-model polish requires a provider you configure — we do not invent accuracy guarantees or unnamed customer results.',
  targetCustomer: {
    title: 'Who it’s for',
    lead: 'Fleet managers and owners who want faster answers from live data without replacing operational systems.',
    bullets: [
      'Operators already on (or evaluating) SheikhGo Nexus',
      'Teams drowning in alerts who need prioritised digests',
      'Managers who want plain-language answers from fleet tools',
      'Buyers comparing “AI fleet” vendors who need honest scope',
    ],
  },
  problem: {
    title: 'Problem we solve',
    lead: 'Dashboards alone still force managers to hunt across modules for “what needs attention today?”',
    bullets: [
      'Too many alerts without a clear morning summary',
      'Questions that require three screens and a spreadsheet',
      'AI demos that cannot show real tool calls on fleet data',
      'Fear of opaque models making unverifiable claims',
    ],
  },
  featuresTitle: 'Insights you can explore',
  featuresLead: 'See the full capability inventory on AI Solutions — this page is the buyer-facing insights summary.',
  features: [
    { title: 'Tool-first assistant', text: 'Answers grounded in fleet tools — vehicles, trips, GPS, and more.', icon: 'smart_toy' },
    { title: 'Fleet health signals', text: 'Heuristic scores and recommendations from rules on your operational data.', icon: 'monitor_heart' },
    { title: 'Morning digests', text: 'Summaries that help managers start the day with priorities.', icon: 'wb_sunny' },
    { title: 'WhatsApp assist', text: 'Draft replies for staff review — nothing sends automatically.', icon: 'chat' },
    { title: 'Mobile copilots', text: 'Driver and fleet app surfaces with the same intelligence in context.', icon: 'phone_iphone' },
    { title: 'Full AI inventory', text: 'Browse every listed capability on the AI Solutions page.', icon: 'auto_awesome' },
  ],
  nextStep: {
    title: 'Next step',
    lead: 'Book a demo of SheikhGo AI on Nexus data — or request a fleet assessment that includes which insight workflows matter for your operation.',
  },
};

/** Footer / nav helpers for Channel C solution URLs. */
export const CHANNEL_C_NAV_LINKS = [
  { title: 'Logistics & Fleet', path: '/logistics-fleet' },
  { title: 'Fleet Management Software', path: '/fleet-management' },
  { title: 'GPS Vehicle Tracking', path: '/gps-tracking' },
  { title: 'Vehicle Rental Management', path: '/vehicle-rental' },
  { title: 'Transportation Operations', path: '/transportation-operations' },
  { title: 'AI Fleet Insights', path: '/ai-fleet-insights' },
  { title: 'Request a Demo', path: '/request-demo' },
] as const;
