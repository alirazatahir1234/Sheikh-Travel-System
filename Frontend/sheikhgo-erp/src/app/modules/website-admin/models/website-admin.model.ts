export interface WebsiteSettings {
  id: number;
  siteName: string;
  logoUrl?: string | null;
  faviconUrl?: string | null;
  supportEmail?: string | null;
  salesEmail?: string | null;
  privacyEmail?: string | null;
  phone?: string | null;
  address?: string | null;
  linkedInUrl?: string | null;
  facebookUrl?: string | null;
  xUrl?: string | null;
  youTubeUrl?: string | null;
  defaultMetaTitle?: string | null;
  defaultMetaDescription?: string | null;
  analyticsId?: string | null;
}

export interface WebsitePage {
  id: number;
  slug: string;
  title: string;
  description?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  ogImage?: string | null;
  status: string;
  publishedAt?: string | null;
  updatedAt: string;
}

export interface WebsiteSection {
  id: number;
  pageId: number;
  sectionType: string;
  title?: string | null;
  subtitle?: string | null;
  content?: string | null;
  imageUrl?: string | null;
  buttonText?: string | null;
  buttonUrl?: string | null;
  secondaryButtonText?: string | null;
  secondaryButtonUrl?: string | null;
  displayOrder: number;
  isActive: boolean;
  status: string;
}

export interface WebsiteFeature {
  id: number;
  title: string;
  description?: string | null;
  iconKey?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  displayOrder: number;
  isActive: boolean;
  status: string;
}

export interface WebsiteLegal {
  id: number;
  docType: string;
  title: string;
  content: string;
  version?: string | null;
  status: string;
  publishedAt?: string | null;
  updatedAt: string;
}

export interface WebsiteMedia {
  id: number;
  fileName: string;
  fileUrl: string;
  fileType?: string | null;
  altText?: string | null;
  sizeBytes?: number | null;
  createdAt: string;
}

export interface WebsiteContactRequest {
  id: number;
  firstName: string;
  lastName: string;
  company: string;
  email: string;
  phone?: string | null;
  country?: string | null;
  fleetSize?: string | null;
  interestedIn?: string | null;
  message: string;
  status: string;
  createdAt: string;
}

export interface WebsiteDemoRequest {
  id: number;
  name: string;
  company: string;
  email: string;
  phone?: string | null;
  country?: string | null;
  vehicleCount?: string | null;
  currentGpsProvider?: string | null;
  interestedProduct?: string | null;
  message?: string | null;
  status: string;
  createdAt: string;
}

export interface WebsiteDashboard {
  pageCount: number;
  publishedPages: number;
  draftPages: number;
  featureCount: number;
  contactRequests: number;
  demoRequests: number;
  newContactRequests: number;
  newDemoRequests: number;
  mediaCount: number;
  lastPublishedAt?: string | null;
}

export interface WebsitePublicHome {
  settings: WebsiteSettings;
  sections: WebsiteSection[];
  features: WebsiteFeature[];
}

export interface WebsiteSectionUpsert {
  id?: number | null;
  pageId: number;
  sectionType: string;
  title?: string | null;
  subtitle?: string | null;
  content?: string | null;
  imageUrl?: string | null;
  buttonText?: string | null;
  buttonUrl?: string | null;
  secondaryButtonText?: string | null;
  secondaryButtonUrl?: string | null;
  displayOrder?: number;
  isActive?: boolean;
  status?: string;
}

export interface WebsiteFeatureUpsert {
  id?: number | null;
  title: string;
  description?: string | null;
  iconKey?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  displayOrder?: number;
  isActive?: boolean;
  status?: string;
}

export interface WebsitePageUpdate {
  title: string;
  description?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  ogImage?: string | null;
  status?: string | null;
}

export interface WebsiteSettingsUpdate {
  siteName: string;
  logoUrl?: string | null;
  faviconUrl?: string | null;
  supportEmail?: string | null;
  salesEmail?: string | null;
  privacyEmail?: string | null;
  phone?: string | null;
  address?: string | null;
  linkedInUrl?: string | null;
  facebookUrl?: string | null;
  xUrl?: string | null;
  youTubeUrl?: string | null;
  defaultMetaTitle?: string | null;
  defaultMetaDescription?: string | null;
  analyticsId?: string | null;
}

export interface WebsiteLegalUpdate {
  title: string;
  content: string;
  version?: string | null;
}

export const WEBSITE_LEAD_STATUSES = [
  'New',
  'Contacted',
  'InProgress',
  'Qualified',
  'Converted',
  'Closed'
] as const;

export const WEBSITE_CONTENT_STATUSES = ['Draft', 'Published'] as const;
