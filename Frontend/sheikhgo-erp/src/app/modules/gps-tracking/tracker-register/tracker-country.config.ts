import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';

export interface TrackerCountryConfig {
  code: string;
  flag: string;
  label: string;
  localDigits: number;
  localPattern: RegExp;
  localPlaceholder: string;
  simProviders: string[];
}

export const TRACKER_COUNTRIES: TrackerCountryConfig[] = [
  {
    code: '+971',
    flag: '🇦🇪',
    label: 'UAE (+971)',
    localDigits: 9,
    localPattern: /^5\d{8}$/,
    localPlaceholder: '501234567',
    simProviders: ['Etisalat', 'Du']
  },
  {
    code: '+92',
    flag: '🇵🇰',
    label: 'Pakistan (+92)',
    localDigits: 10,
    localPattern: /^3\d{9}$/,
    localPlaceholder: '3001234567',
    simProviders: ['Jazz', 'Zong', 'Ufone', 'Telenor']
  },
  {
    code: '+966',
    flag: '🇸🇦',
    label: 'Saudi Arabia (+966)',
    localDigits: 9,
    localPattern: /^5\d{8}$/,
    localPlaceholder: '501234567',
    simProviders: ['STC', 'Mobily', 'Zain']
  },
  {
    code: '+91',
    flag: '🇮🇳',
    label: 'India (+91)',
    localDigits: 10,
    localPattern: /^[6-9]\d{9}$/,
    localPlaceholder: '9876543210',
    simProviders: ['Jio', 'Airtel', 'Vi']
  },
  {
    code: '+44',
    flag: '🇬🇧',
    label: 'UK (+44)',
    localDigits: 10,
    localPattern: /^\d{10}$/,
    localPlaceholder: '7123456789',
    simProviders: ['EE', 'Vodafone', 'O2']
  },
  {
    code: '+1',
    flag: '🇺🇸',
    label: 'US (+1)',
    localDigits: 10,
    localPattern: /^\d{10}$/,
    localPlaceholder: '2025551234',
    simProviders: ['AT&T', 'Verizon', 'T-Mobile']
  }
];

export const DEFAULT_TRACKER_COUNTRY = '+971';

export function getTrackerCountry(code: string): TrackerCountryConfig | undefined {
  return TRACKER_COUNTRIES.find(c => c.code === code);
}

export function countrySelectOptions(): UiSelectOption[] {
  return TRACKER_COUNTRIES.map(c => ({ value: c.code, label: `${c.flag} ${c.label}` }));
}

export function simProviderOptions(countryCode: string): UiSelectOption[] {
  const country = getTrackerCountry(countryCode);
  return (country?.simProviders ?? []).map(p => ({ value: p, label: p }));
}

export const TRACKER_CATEGORIES: UiSelectOption[] = [
  { value: 'truck', label: 'Truck' },
  { value: 'car', label: 'Car' },
  { value: 'bus', label: 'Bus' },
  { value: 'motorcycle', label: 'Motorcycle' },
  { value: 'trailer', label: 'Trailer' },
  { value: 'asset', label: 'Asset' }
];

export const TRACKER_MODELS = [
  { key: 'teltonika_fmb920', label: 'Teltonika FMB920', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: true },
  { key: 'teltonika_fmb140', label: 'Teltonika FMB140', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: true },
  { key: 'teltonika_fmb001', label: 'Teltonika FMB001', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: false },
  { key: 'teltonika_fmc001', label: 'Teltonika FMC001', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: false },
  { key: 'concox_gt06n', label: 'Concox GT06N', vendor: 'Concox', protocol: 'gt06', supportsEngineCutoff: true },
  { key: 'queclink_gv75', label: 'Queclink GV75', vendor: 'Queclink', protocol: 'gl200', supportsEngineCutoff: true },
] as const;

export const RELAY_OUTPUTS: UiSelectOption[] = [
  { value: 'output1', label: 'Output 1' },
  { value: 'output2', label: 'Output 2' },
  { value: 'output3', label: 'Output 3' },
];

export const SIM_PACKAGES: UiSelectOption[] = [
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Yearly', label: 'Yearly' },
  { value: 'Corporate', label: 'Corporate' },
];

export const CURRENT_STATUSES: UiSelectOption[] = [
  { value: 'Installed', label: 'Installed' },
  { value: 'InStock', label: 'In Stock' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Damaged', label: 'Damaged' },
  { value: 'Removed', label: 'Removed' },
];

export function trackerModelOptions(): UiSelectOption[] {
  return TRACKER_MODELS.map(m => ({ value: m.key, label: m.label }));
}

export function resolveTrackerModel(key: string) {
  return TRACKER_MODELS.find(m => m.key === key);
}
