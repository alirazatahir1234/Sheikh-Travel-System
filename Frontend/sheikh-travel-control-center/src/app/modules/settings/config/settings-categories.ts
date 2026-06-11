import { SettingsCategory } from '../models/settings.model';

/** Client-side fallback when the categories API is unavailable. Ids match backend + schema keys. */
export const SETTINGS_CATEGORIES_FALLBACK: SettingsCategory[] = [
  { id: 'General', label: 'General', icon: 'tune', description: 'Company identity, contact details and regional defaults.', isImplemented: true },
  { id: 'Tenant', label: 'Tenant', icon: 'apartment', description: 'Subscription plan, limits and tenant status.', isImplemented: true },
  { id: 'Localization', label: 'Localization', icon: 'language', description: 'Language, direction, region and number formats.', isImplemented: true },
  { id: 'Security', label: 'Security', icon: 'security', description: 'Authentication, API security, IP management and compliance.', isImplemented: true },
  { id: 'Notifications', label: 'Notifications', icon: 'notifications', description: 'Email, SMS, WhatsApp, push and alert preferences.', isImplemented: true },
  { id: 'Documents', label: 'Documents', icon: 'description', description: 'Upload limits, extensions and document lifecycle.', isImplemented: true },
  { id: 'Workflows', label: 'Workflows', icon: 'account_tree', description: 'Approval levels, auto-approval and escalation.', isImplemented: true },
  { id: 'Numbering', label: 'Numbering', icon: 'tag', description: 'Prefixes and sequences for records.', isImplemented: true },
  { id: 'FileManagement', label: 'File Management', icon: 'folder', description: 'Storage provider, file limits and retention.', isImplemented: true },
  { id: 'Branding', label: 'Branding', icon: 'palette', description: 'Logo, theme colors and public contact details.', isImplemented: true },
  { id: 'System', label: 'System Preferences', icon: 'settings', description: 'Landing page, pagination, theme and auto-save.', isImplemented: true },
  { id: 'Integrations', label: 'Integrations', icon: 'extension', description: 'Maps, payments, GPS and government APIs.', isImplemented: true },
  { id: 'Audit', label: 'Audit & Logging', icon: 'history', description: 'Audit logs, activity tracking and retention.', isImplemented: true },
  { id: 'Features', label: 'Feature Management', icon: 'toggle_on', description: 'Enable or disable platform modules.', isImplemented: true },
  { id: 'AI', label: 'AI', icon: 'smart_toy', description: 'AI provider, keys and assistant capabilities.', isImplemented: true }
];
