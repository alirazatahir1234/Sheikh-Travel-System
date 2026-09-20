import { WEBSITE_BRAND } from './brand';

export interface ServiceItem {
  /** Anchor id on /services */
  slug: string;
  title: string;
  text: string;
  icon: string;
  /** Longer copy for the /services section body */
  detail: string;
  highlights: readonly string[];
}

export interface IndustryItem {
  title: string;
  text: string;
  icon: string;
  link: string;
  fragment: string | null;
}

export const SERVICE_ITEMS: readonly ServiceItem[] = [
  {
    slug: 'web-development',
    title: 'Web Development',
    text: 'Modern, scalable and high-performing web applications.',
    icon: 'language',
    detail:
      'We build web applications that stay fast as they grow — from marketing sites and customer portals to complex internal platforms, delivered with modern frameworks and a maintainable architecture.',
    highlights: ['Progressive web apps', 'Portals & dashboards', 'API-driven front ends', 'Performance & SEO'],
  },
  {
    slug: 'mobile-app-development',
    title: 'Mobile App Development',
    text: 'Cross-platform and native mobile applications for modern businesses.',
    icon: 'smartphone',
    detail:
      'iOS and Android apps built from a single codebase where it makes sense, native where it matters. We handle store releases, offline behaviour, push notifications and device integrations.',
    highlights: ['Flutter & native', 'Offline-first sync', 'Push & location services', 'App store delivery'],
  },
  {
    slug: 'ui-ux-design',
    title: 'UI/UX Design',
    text: 'User-centered interfaces that create meaningful experiences.',
    icon: 'palette',
    detail:
      'Research, flows, wireframes and polished interface design — grounded in how your users actually work, then handed to engineering as a design system rather than a pile of screens.',
    highlights: ['Product discovery', 'Design systems', 'Prototyping', 'Accessibility'],
  },
  {
    slug: 'cloud-solutions',
    title: 'Cloud Solutions',
    text: 'Scalable cloud infrastructure, migration and deployment solutions.',
    icon: 'cloud',
    detail:
      'Cloud architecture, migration and automation on Azure and AWS, with the deployment pipelines and monitoring needed to run it confidently once it is live.',
    highlights: ['Azure & AWS', 'Migration planning', 'CI/CD pipelines', 'Monitoring & cost control'],
  },
  {
    slug: 'it-consulting',
    title: 'IT Consulting',
    text: 'Strategic technology guidance to help achieve your business goals.',
    icon: 'support_agent',
    detail:
      'An outside read on your architecture, delivery process and roadmap — with a prioritised plan you can act on, not a slide deck that ends at the recommendation.',
    highlights: ['Architecture review', 'Technology roadmap', 'Delivery process', 'Vendor selection'],
  },
  {
    slug: 'custom-software',
    title: 'Custom Software',
    text: 'Tailored software built around your unique business needs.',
    icon: 'code',
    detail:
      'When off-the-shelf tools force you to change how you work, we build the system around your process instead — including integrations with the platforms you already depend on.',
    highlights: ['Line-of-business systems', 'Workflow automation', 'Third-party integrations', 'Legacy modernisation'],
  },
];

export const INDUSTRY_ITEMS: readonly IndustryItem[] = [
  {
    title: 'Logistics & Fleet',
    text: 'Fleet, GPS tracking and trip operations in one platform.',
    icon: 'local_shipping',
    link: WEBSITE_BRAND.loginPath,
    fragment: null,
  },
  {
    title: 'Travel & Tourism',
    text: 'Bookings, vehicles and passenger operations end to end.',
    icon: 'flight_takeoff',
    link: WEBSITE_BRAND.loginPath,
    fragment: null,
  },
  {
    title: 'Healthcare',
    text: 'Patient transport, scheduling and compliant record keeping.',
    icon: 'local_hospital',
    link: '/',
    fragment: 'industries',
  },
  {
    title: 'Retail & E-Commerce',
    text: 'Storefronts, delivery tracking and inventory integrations.',
    icon: 'shopping_cart',
    link: '/',
    fragment: 'industries',
  },
  {
    title: 'Finance',
    text: 'Secure portals, reporting and process automation.',
    icon: 'account_balance',
    link: '/',
    fragment: 'industries',
  },
  {
    title: 'Education',
    text: 'Student transport, campus systems and learning portals.',
    icon: 'school',
    link: '/',
    fragment: 'industries',
  },
  {
    title: 'Real Estate',
    text: 'Listing platforms, CRM integration and field operations.',
    icon: 'apartment',
    link: '/',
    fragment: 'industries',
  },
  {
    title: 'Manufacturing',
    text: 'Plant logistics, asset tracking and operations dashboards.',
    icon: 'precision_manufacturing',
    link: '/',
    fragment: 'industries',
  },
];
