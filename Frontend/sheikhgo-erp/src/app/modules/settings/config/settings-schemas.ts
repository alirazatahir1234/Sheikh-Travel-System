import { SettingFieldSchema } from '../models/settings.model';
import { COMPANY_NAME } from '../../../core/constants/app-brand';

const CURRENCY_OPTIONS = [
  { value: 'AED', label: 'AED — UAE Dirham' },
  { value: 'PKR', label: 'PKR — Pakistani Rupee' },
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'GBP', label: 'GBP — British Pound' },
  { value: 'SAR', label: 'SAR — Saudi Riyal' }
];

const TIMEZONE_OPTIONS = [
  { value: 'Asia/Dubai', label: '(GMT+04:00) Dubai' },
  { value: 'Asia/Karachi', label: '(GMT+05:00) Karachi' },
  { value: 'Asia/Riyadh', label: '(GMT+03:00) Riyadh' },
  { value: 'Europe/London', label: '(GMT+00:00) London' },
  { value: 'America/New_York', label: '(GMT-05:00) New York' }
];

const LANGUAGE_OPTIONS = [
  { value: 'en-AE', label: 'English (United Arab Emirates)' },
  { value: 'en-US', label: 'English (United States)' },
  { value: 'ar-AE', label: 'Arabic (United Arab Emirates)' },
  { value: 'ur-PK', label: 'Urdu (Pakistan)' }
];

const COUNTRY_OPTIONS = [
  { value: 'United Arab Emirates', label: 'United Arab Emirates' },
  { value: 'Pakistan', label: 'Pakistan' },
  { value: 'Saudi Arabia', label: 'Saudi Arabia' },
  { value: 'United Kingdom', label: 'United Kingdom' },
  { value: 'United States', label: 'United States' }
];

const DATE_FORMAT_OPTIONS = [
  { value: 'dd/MM/yyyy', label: 'dd/MM/yyyy (31/12/2026)' },
  { value: 'MM/dd/yyyy', label: 'MM/dd/yyyy (12/31/2026)' },
  { value: 'yyyy-MM-dd', label: 'yyyy-MM-dd (2026-12-31)' },
  { value: 'dd MMM yyyy', label: 'dd MMM yyyy (31 Dec 2026)' }
];

const TIME_FORMAT_OPTIONS = [
  { value: '24h', label: '24-hour (14:30)' },
  { value: '12h', label: '12-hour (02:30 PM)' }
];

// 1. General Settings
const generalSchema: SettingFieldSchema[] = [
  { key: 'CompanyName', label: 'Company Name', type: 'text', section: 'Company Identity', required: true, placeholder: COMPANY_NAME },
  { key: 'CompanyLogo', label: 'Company Logo URL', type: 'url', section: 'Company Identity', placeholder: 'https://...' },
  { key: 'CompanyAddress', label: 'Company Address', type: 'textarea', section: 'Company Identity', placeholder: 'Street, City, Country' },
  { key: 'Phone', label: 'Phone', type: 'text', section: 'Contact', placeholder: '+971 4 000 0000' },
  { key: 'Email', label: 'Email', type: 'email', section: 'Contact', placeholder: 'info@example.com' },
  { key: 'Website', label: 'Website', type: 'url', section: 'Contact', placeholder: 'https://...' },
  { key: 'Timezone', label: 'Time Zone', type: 'dropdown', section: 'Regional Defaults', options: TIMEZONE_OPTIONS },
  { key: 'DefaultCurrency', label: 'Currency', type: 'dropdown', section: 'Regional Defaults', options: CURRENCY_OPTIONS },
  { key: 'Language', label: 'Language', type: 'dropdown', section: 'Regional Defaults', options: LANGUAGE_OPTIONS },
  { key: 'DateFormat', label: 'Date Format', type: 'dropdown', section: 'Regional Defaults', options: DATE_FORMAT_OPTIONS },
  { key: 'TimeFormat', label: 'Time Format', type: 'dropdown', section: 'Regional Defaults', options: TIME_FORMAT_OPTIONS },
  { key: 'FiscalYearStart', label: 'Fiscal Year Start (MM-DD)', type: 'text', section: 'Regional Defaults', placeholder: '01-01' }
];

// 2. Tenant Settings
const tenantSchema: SettingFieldSchema[] = [
  { key: 'TenantName', label: 'Tenant Name', type: 'text', section: 'Tenant', placeholder: COMPANY_NAME },
  { key: 'SubscriptionPlan', label: 'Subscription Plan', type: 'text', section: 'Tenant', placeholder: 'Enterprise' },
  { key: 'TenantStatus', label: 'Tenant Status', type: 'dropdown', section: 'Tenant', options: [
    { value: 'Active', label: 'Active' },
    { value: 'Suspended', label: 'Suspended' },
    { value: 'Trial', label: 'Trial' }
  ] },
  { key: 'MaxUsers', label: 'Maximum Users', type: 'number', section: 'Limits', min: 0 },
  { key: 'MaxBranches', label: 'Maximum Branches', type: 'number', section: 'Limits', min: 0 },
  { key: 'StorageLimitGb', label: 'Storage Limit (GB)', type: 'number', section: 'Limits', min: 0 }
];

// 3. Localization
const localizationSchema: SettingFieldSchema[] = [
  { key: 'Language', label: 'Language', type: 'dropdown', section: 'Language', options: LANGUAGE_OPTIONS },
  { key: 'TextDirection', label: 'Text Direction', type: 'dropdown', section: 'Language', options: [
    { value: 'ltr', label: 'Left to Right (LTR)' },
    { value: 'rtl', label: 'Right to Left (RTL)' }
  ] },
  { key: 'Country', label: 'Country', type: 'dropdown', section: 'Region', options: COUNTRY_OPTIONS },
  { key: 'Timezone', label: 'Timezone', type: 'dropdown', section: 'Region', options: TIMEZONE_OPTIONS },
  { key: 'Currency', label: 'Currency', type: 'dropdown', section: 'Region', options: CURRENCY_OPTIONS },
  { key: 'NumberFormat', label: 'Number Format', type: 'dropdown', section: 'Region', options: [
    { value: '1,234.56', label: '1,234.56' },
    { value: '1.234,56', label: '1.234,56' },
    { value: '1 234.56', label: '1 234.56' }
  ] }
];

// 4. Security Settings (IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
// IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled map to TenantSecuritySettings)
const securitySchema: SettingFieldSchema[] = [
  { key: 'IsMfaRequired', label: 'Require Multi-Factor Authentication', type: 'toggle', section: 'Authentication', hint: 'Force all users to set up MFA at next sign-in.' },
  { key: 'IsOtpEnabled', label: 'OTP on Login', type: 'toggle', section: 'Authentication' },
  { key: 'PasswordExpiryDays', label: 'Password Expiry (days)', type: 'number', section: 'Authentication', min: 0, max: 365, hint: 'Leave at 0 to disable expiry.' },
  { key: 'SessionTimeoutMinutes', label: 'Session Timeout (minutes)', type: 'number', section: 'Authentication', min: 5, max: 1440 },
  { key: 'MaxLoginAttempts', label: 'Maximum Login Attempts', type: 'number', section: 'Authentication', min: 0, max: 20 },
  { key: 'AccountLockoutMinutes', label: 'Account Lockout (minutes)', type: 'number', section: 'Authentication', min: 0 },
  { key: 'JwtExpiryMinutes', label: 'JWT Expiry (minutes)', type: 'number', section: 'API Security', min: 5 },
  { key: 'RefreshTokenExpiryDays', label: 'Refresh Token Expiry (days)', type: 'number', section: 'API Security', min: 1 },
  { key: 'IpWhitelist', label: 'IP Whitelist', type: 'textarea', section: 'IP Management', placeholder: 'One IP/CIDR per line' },
  { key: 'BlockedIps', label: 'Blocked IPs', type: 'textarea', section: 'IP Management', placeholder: 'One IP/CIDR per line' },
  { key: 'IsGdprEnabled', label: 'GDPR Compliance', type: 'toggle', section: 'Compliance' },
  { key: 'IsAuditLoggingEnabled', label: 'Audit Logging', type: 'toggle', section: 'Compliance' },
  { key: 'IsVatEnabled', label: 'VAT / Tax Enabled', type: 'toggle', section: 'Compliance' }
];

// 5. Notification Settings
const notificationSchema: SettingFieldSchema[] = [
  { key: 'EmailEnabled', label: 'Email Notifications', type: 'toggle', section: 'Channels' },
  { key: 'SmsEnabled', label: 'SMS Notifications', type: 'toggle', section: 'Channels' },
  { key: 'WhatsappEnabled', label: 'WhatsApp Notifications', type: 'toggle', section: 'Channels' },
  { key: 'PushEnabled', label: 'Push Notifications', type: 'toggle', section: 'Channels' },
  { key: 'SmtpServer', label: 'SMTP Server', type: 'text', section: 'Email (SMTP)', placeholder: 'smtp.office365.com' },
  { key: 'SmtpPort', label: 'SMTP Port', type: 'number', section: 'Email (SMTP)', placeholder: '587' },
  { key: 'SmtpUsername', label: 'SMTP Username', type: 'text', section: 'Email (SMTP)' },
  { key: 'SmtpPassword', label: 'SMTP Password', type: 'password', section: 'Email (SMTP)' },
  { key: 'SmsGatewayApiKey', label: 'SMS Gateway API Key', type: 'password', section: 'Gateways' },
  { key: 'WhatsappApiKey', label: 'WhatsApp API Key', type: 'password', section: 'Gateways' },
  { key: 'BookingAlerts', label: 'Booking Alerts', type: 'toggle', section: 'Alert Types' },
  { key: 'VehicleAlerts', label: 'Vehicle Alerts', type: 'toggle', section: 'Alert Types' },
  { key: 'MaintenanceAlerts', label: 'Maintenance Alerts', type: 'toggle', section: 'Alert Types' }
];

// 6. Document Settings
const documentSchema: SettingFieldSchema[] = [
  { key: 'MaxUploadSizeMb', label: 'Max Upload Size (MB)', type: 'number', section: 'Uploads', min: 1 },
  { key: 'AllowedExtensions', label: 'Allowed Extensions', type: 'text', section: 'Uploads', placeholder: 'pdf,jpg,png,docx' },
  { key: 'ExpiryReminderDays', label: 'Document Expiry Reminder (days)', type: 'number', section: 'Lifecycle', min: 0 },
  { key: 'StorageLocation', label: 'Storage Location', type: 'dropdown', section: 'Lifecycle', options: [
    { value: 'Local', label: 'Local Storage' },
    { value: 'Azure', label: 'Azure Blob' },
    { value: 'S3', label: 'AWS S3' }
  ] }
];

// 7. Workflow Settings
const workflowSchema: SettingFieldSchema[] = [
  { key: 'ApprovalLevels', label: 'Approval Levels', type: 'number', section: 'Approvals', min: 0, max: 10 },
  { key: 'AutoApproval', label: 'Auto Approval', type: 'toggle', section: 'Approvals' },
  { key: 'EscalationEnabled', label: 'Escalation Enabled', type: 'toggle', section: 'Escalation' },
  { key: 'EscalationHours', label: 'Escalation After (hours)', type: 'number', section: 'Escalation', min: 0 }
];

// 8. Numbering & Sequence
const numberingSchema: SettingFieldSchema[] = [
  { key: 'VehiclePrefix', label: 'Vehicle Prefix', type: 'text', section: 'Prefixes', placeholder: 'VH-' },
  { key: 'BookingPrefix', label: 'Booking Prefix', type: 'text', section: 'Prefixes', placeholder: 'BK-' },
  { key: 'InvoicePrefix', label: 'Invoice Prefix', type: 'text', section: 'Prefixes', placeholder: 'INV-' },
  { key: 'EmployeePrefix', label: 'Employee Prefix', type: 'text', section: 'Prefixes', placeholder: 'EMP-' },
  { key: 'SequencePadding', label: 'Sequence Padding (digits)', type: 'number', section: 'Format', min: 1, max: 12, hint: 'Number of leading zeros, e.g. 6 → 000001.' },
  { key: 'IncludeYearInSequence', label: 'Include Year', type: 'toggle', section: 'Format' }
];

// 9. File Management
const fileManagementSchema: SettingFieldSchema[] = [
  { key: 'StorageProvider', label: 'Storage Provider', type: 'dropdown', section: 'Provider', options: [
    { value: 'Local', label: 'Local Storage' },
    { value: 'Azure', label: 'Azure Blob' },
    { value: 'S3', label: 'AWS S3' }
  ] },
  { key: 'MaxFileSizeMb', label: 'Max File Size (MB)', type: 'number', section: 'Limits', min: 1 },
  { key: 'AllowedTypes', label: 'Allowed Types', type: 'text', section: 'Limits', placeholder: 'image/*,application/pdf' },
  { key: 'RetentionDays', label: 'Retention Policy (days)', type: 'number', section: 'Limits', min: 0, hint: 'Leave at 0 to retain indefinitely.' }
];

// 10. Branding (LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode,
// TimeZone map to TenantBranding)
const brandingSchema: SettingFieldSchema[] = [
  { key: 'LogoUrl', label: 'Logo URL', type: 'url', section: 'Brand', placeholder: 'https://...' },
  { key: 'FaviconUrl', label: 'Favicon URL', type: 'url', section: 'Brand', placeholder: 'https://...' },
  { key: 'LoginBackgroundUrl', label: 'Login Background URL', type: 'url', section: 'Brand', placeholder: 'https://...' },
  { key: 'PrimaryColor', label: 'Theme Color', type: 'color', section: 'Theme' },
  { key: 'SidebarColor', label: 'Sidebar Color', type: 'color', section: 'Theme' },
  { key: 'Website', label: 'Website', type: 'url', section: 'Public Contact', placeholder: 'https://...' },
  { key: 'SupportEmail', label: 'Support Email', type: 'email', section: 'Public Contact', placeholder: 'support@example.com' },
  { key: 'Country', label: 'Country', type: 'dropdown', section: 'Public Contact', options: COUNTRY_OPTIONS },
  { key: 'CurrencyCode', label: 'Currency', type: 'dropdown', section: 'Public Contact', options: CURRENCY_OPTIONS },
  { key: 'TimeZone', label: 'Timezone', type: 'dropdown', section: 'Public Contact', options: TIMEZONE_OPTIONS }
];

// 11. System Preferences
const systemSchema: SettingFieldSchema[] = [
  { key: 'DefaultLandingPage', label: 'Default Landing Page', type: 'text', section: 'Experience', placeholder: '/dashboard' },
  { key: 'DefaultPageSize', label: 'Default Page Size', type: 'number', section: 'Experience', min: 5, max: 200 },
  { key: 'Theme', label: 'Theme', type: 'dropdown', section: 'Experience', options: [
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
    { value: 'system', label: 'System' }
  ] },
  { key: 'DashboardLayout', label: 'Dashboard Layout', type: 'dropdown', section: 'Experience', options: [
    { value: 'compact', label: 'Compact' },
    { value: 'comfortable', label: 'Comfortable' }
  ] },
  { key: 'AutoSave', label: 'Auto Save', type: 'toggle', section: 'Experience' }
];

// 12. Integration Settings
const integrationSchema: SettingFieldSchema[] = [
  { key: 'GoogleMapsApiKey', label: 'Google Maps API Key', type: 'password', section: 'Maps' },
  { key: 'MapboxApiKey', label: 'MapBox API Key', type: 'password', section: 'Maps' },
  { key: 'StripeApiKey', label: 'Stripe API Key', type: 'password', section: 'Payments' },
  { key: 'PaypalClientId', label: 'PayPal Client ID', type: 'password', section: 'Payments' },
  { key: 'LocalPaymentGatewayKey', label: 'Local Gateway Key', type: 'password', section: 'Payments' },
  { key: 'GpsDeviceApiKey', label: 'GPS Device API Key', type: 'password', section: 'GPS' },
  { key: 'GpsTrackingApiUrl', label: 'Tracking API URL', type: 'url', section: 'GPS' },
  { key: 'MohreApiKey', label: 'MOHRE API Key', type: 'password', section: 'Government' },
  { key: 'IcpApiKey', label: 'ICP API Key', type: 'password', section: 'Government' },
  { key: 'VisaApiKey', label: 'Visa API Key', type: 'password', section: 'Government' }
];

// 13. Audit & Logging
const auditSchema: SettingFieldSchema[] = [
  { key: 'AuditLogsEnabled', label: 'Enable Audit Logs', type: 'toggle', section: 'Audit' },
  { key: 'ActivityTracking', label: 'Activity Tracking', type: 'toggle', section: 'Audit' },
  { key: 'ErrorLoggingEnabled', label: 'Error Logging', type: 'toggle', section: 'Logging' },
  { key: 'LogRetentionDays', label: 'Log Retention (days)', type: 'number', section: 'Logging', min: 0 }
];

// 14. Feature Management
const featureSchema: SettingFieldSchema[] = [
  { key: 'FleetEnabled', label: 'Fleet', type: 'toggle', section: 'Modules' },
  { key: 'GpsEnabled', label: 'GPS Tracking', type: 'toggle', section: 'Modules' },
  { key: 'CrmEnabled', label: 'CRM', type: 'toggle', section: 'Modules' },
  { key: 'HrEnabled', label: 'HR', type: 'toggle', section: 'Modules' },
  { key: 'PayrollEnabled', label: 'Payroll', type: 'toggle', section: 'Modules' },
  { key: 'TravelAgencyEnabled', label: 'Travel Agency', type: 'toggle', section: 'Modules' }
];

// 15. AI Settings
const aiSchema: SettingFieldSchema[] = [
  { key: 'AiProvider', label: 'AI Provider', type: 'dropdown', section: 'Provider', options: [
    { value: 'OpenAI', label: 'OpenAI' },
    { value: 'AzureOpenAI', label: 'Azure OpenAI' },
    { value: 'None', label: 'Disabled' }
  ] },
  { key: 'OpenAiApiKey', label: 'OpenAI API Key', type: 'password', section: 'Provider' },
  { key: 'AzureOpenAiEndpoint', label: 'Azure OpenAI Endpoint', type: 'url', section: 'Provider' },
  { key: 'AzureOpenAiApiKey', label: 'Azure OpenAI API Key', type: 'password', section: 'Provider' },
  { key: 'OcrEnabled', label: 'OCR Document Extraction', type: 'toggle', section: 'Capabilities' },
  { key: 'AiChatAssistant', label: 'AI Chat Assistant', type: 'toggle', section: 'Capabilities' },
  { key: 'DocumentAnalysis', label: 'Document Analysis', type: 'toggle', section: 'Capabilities' }
];

/** Field schemas keyed by backend category id (case-sensitive). */
export const SETTINGS_SCHEMAS: Record<string, SettingFieldSchema[]> = {
  General: generalSchema,
  Tenant: tenantSchema,
  Localization: localizationSchema,
  Security: securitySchema,
  Notifications: notificationSchema,
  Documents: documentSchema,
  Workflows: workflowSchema,
  Numbering: numberingSchema,
  FileManagement: fileManagementSchema,
  Branding: brandingSchema,
  System: systemSchema,
  Integrations: integrationSchema,
  Audit: auditSchema,
  Features: featureSchema,
  AI: aiSchema
};
