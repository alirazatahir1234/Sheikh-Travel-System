/**
 * TravelNexus wire models. String unions mirror
 * Backend/SheikhTravelSystem.Application/Features/TravelNexus/Common/TravelValues.cs — change both together.
 */

export const TRAVEL_PERMISSIONS = {
  view: 'Travel.View',
  settings: 'Travel.Settings',
  customerView: 'Travel.Customer.View',
  customerManage: 'Travel.Customer.Manage',
  travelerViewPii: 'Travel.Traveler.ViewPII'
} as const;

export type TravelVisaCategory =
  | 'Tourist' | 'Business' | 'Umrah' | 'Hajj' | 'Work' | 'Student' | 'Transit' | 'Family' | 'Other';
export const TRAVEL_VISA_CATEGORIES: TravelVisaCategory[] =
  ['Tourist', 'Business', 'Umrah', 'Hajj', 'Work', 'Student', 'Transit', 'Family', 'Other'];

export type TravelVisaEntryType = 'Single' | 'Double' | 'Multiple';
export const TRAVEL_VISA_ENTRY_TYPES: TravelVisaEntryType[] = ['Single', 'Double', 'Multiple'];

export type TravelCustomerKind = 'Individual' | 'Corporate';
export const TRAVEL_CUSTOMER_KINDS: TravelCustomerKind[] = ['Individual', 'Corporate'];

export type TravelGender = 'Male' | 'Female';
export const TRAVEL_GENDERS: TravelGender[] = ['Male', 'Female'];

export type TravelMahramRelation =
  | 'Husband' | 'Father' | 'Son' | 'Brother' | 'Grandfather' | 'Grandson'
  | 'Uncle' | 'Nephew' | 'FatherInLaw' | 'SonInLaw' | 'Other';
export const TRAVEL_MAHRAM_RELATIONS: TravelMahramRelation[] =
  ['Husband', 'Father', 'Son', 'Brother', 'Grandfather', 'Grandson', 'Uncle', 'Nephew', 'FatherInLaw', 'SonInLaw', 'Other'];

export type TravelPassportStatus = 'Missing' | 'Valid' | 'ExpiringSoon' | 'Expired';

// ── Configuration ──────────────────────────────────────────────────────

export interface TravelCountry { code: string; name: string; }

export interface TravelLookupItem { id: number; name: string; }
export interface TravelLookups { branches: TravelLookupItem[]; agents: TravelLookupItem[]; }

export interface TravelVisaType {
  id: number;
  countryCode: string;
  countryName: string;
  name: string;
  category: TravelVisaCategory;
  entryType: TravelVisaEntryType;
  validityDays: number | null;
  stayDays: number | null;
  processingDays: number | null;
  defaultFee: number | null;
  currencyCode: string | null;
  isActive: boolean;
  notes: string | null;
}
export type SaveTravelVisaType = Omit<TravelVisaType, 'id' | 'countryName'>;

export interface TravelDocumentType {
  id: number;
  code: string;
  name: string;
  description: string | null;
  requiresExpiryDate: boolean;
  isActive: boolean;
}
export type SaveTravelDocumentType = Omit<TravelDocumentType, 'id'>;

export interface TravelLeadSource { id: number; name: string; isActive: boolean; }
export type SaveTravelLeadSource = Omit<TravelLeadSource, 'id'>;

export interface TravelSettings {
  defaultCurrencyCode: string | null;
  enquiryPrefix: string;
  visaCasePrefix: string;
  invoicePrefix: string;
  passportExpiryWarningDays: number;
}

// ── Customers & travelers ──────────────────────────────────────────────

export interface TravelCustomerListItem {
  customerId: number;
  customerKind: TravelCustomerKind;
  fullName: string;
  contactPerson: string | null;
  phone: string;
  email: string | null;
  city: string | null;
  assignedAgentUserId: number | null;
  assignedAgentName: string | null;
  branchId: number | null;
  branchName: string | null;
  leadSourceName: string | null;
  travelerCount: number;
  nextPassportExpiry: string | null;
  createdAt: string;
}

export interface TravelCustomer extends TravelCustomerListItem {
  address: string | null;
  cnic: string | null;
  taxNumber: string | null;
  alternatePhone: string | null;
  leadSourceId: number | null;
  notes: string | null;
  piiMasked: boolean;
}

export interface SaveTravelCustomer {
  customerKind: TravelCustomerKind;
  fullName: string;
  phone: string;
  email: string | null;
  address: string | null;
  city: string | null;
  /** On update: null or the masked value keeps the stored CNIC; '' clears it. */
  cnic: string | null;
  contactPerson: string | null;
  taxNumber: string | null;
  alternatePhone: string | null;
  assignedAgentUserId: number | null;
  branchId: number | null;
  leadSourceId: number | null;
  notes: string | null;
}

export interface CreateTravelCustomer extends SaveTravelCustomer {
  existingCustomerId: number | null;
}

export interface TravelCustomerQuery {
  page: number;
  pageSize: number;
  search?: string;
  customerKind?: TravelCustomerKind | '';
}

export interface TravelTraveler {
  id: number;
  customerId: number;
  title: string | null;
  givenNames: string;
  surname: string;
  fullName: string;
  gender: TravelGender;
  dateOfBirth: string | null;
  placeOfBirth: string | null;
  nationalityCode: string | null;
  nationalityName: string | null;
  passportNumber: string | null;
  passportIssuingCountryCode: string | null;
  passportIssuingCountryName: string | null;
  passportIssueDate: string | null;
  passportExpiryDate: string | null;
  passportStatus: TravelPassportStatus;
  cnic: string | null;
  phone: string | null;
  email: string | null;
  mahramTravelerId: number | null;
  mahramName: string | null;
  mahramRelation: TravelMahramRelation | null;
  notes: string | null;
  piiMasked: boolean;
  createdAt: string;
}

export interface SaveTravelTraveler {
  title: string | null;
  givenNames: string;
  surname: string;
  gender: TravelGender;
  dateOfBirth: string | null;
  placeOfBirth: string | null;
  nationalityCode: string | null;
  /** On update: null or the masked value keeps the stored number; '' clears it. */
  passportNumber: string | null;
  passportIssuingCountryCode: string | null;
  passportIssueDate: string | null;
  passportExpiryDate: string | null;
  cnic: string | null;
  phone: string | null;
  email: string | null;
  mahramTravelerId: number | null;
  mahramRelation: TravelMahramRelation | null;
  notes: string | null;
}

export const PASSPORT_STATUS_LABEL: Record<TravelPassportStatus, string> = {
  Missing: 'No passport',
  Valid: 'Valid',
  ExpiringSoon: 'Expiring soon',
  Expired: 'Expired'
};

export const PASSPORT_STATUS_VARIANT: Record<TravelPassportStatus, 'inactive' | 'success' | 'warning' | 'error'> = {
  Missing: 'inactive',
  Valid: 'success',
  ExpiringSoon: 'warning',
  Expired: 'error'
};

/** Masked values from the API contain this character; never send them back as new values. */
export const PII_MASK_CHAR = '•';
export const isMasked = (v: string | null | undefined): boolean => !!v && v.includes(PII_MASK_CHAR);
