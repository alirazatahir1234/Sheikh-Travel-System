/**
 * TravelNexus wire models. String unions mirror
 * Backend/SheikhTravelSystem.Application/Features/TravelNexus/Common/TravelValues.cs — change both together.
 */

export const TRAVEL_PERMISSIONS = {
  view: 'Travel.View',
  settings: 'Travel.Settings',
  customerView: 'Travel.Customer.View',
  customerManage: 'Travel.Customer.Manage',
  travelerViewPii: 'Travel.Traveler.ViewPII',
  enquiryView: 'Travel.Enquiry.View',
  enquiryManage: 'Travel.Enquiry.Manage',
  visaView: 'Travel.Visa.View',
  visaManage: 'Travel.Visa.Manage',
  visaDecide: 'Travel.Visa.Decide',
  documentManage: 'Travel.Document.Manage',
  documentVerify: 'Travel.Document.Verify',
  documentDownload: 'Travel.Document.Download',
  packageView: 'Travel.Package.View',
  packageManage: 'Travel.Package.Manage',
  groupView: 'Travel.Group.View',
  groupManage: 'Travel.Group.Manage',
  groupManifest: 'Travel.Group.Manifest',
  supplierView: 'Travel.Supplier.View',
  supplierManage: 'Travel.Supplier.Manage',
  bookingView: 'Travel.Booking.View',
  bookingManage: 'Travel.Booking.Manage',
  bookingCancel: 'Travel.Booking.Cancel'
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
  groupPrefix: string;
  bookingPrefix: string;
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

export interface TravelLostReason { id: number; name: string; isActive: boolean; }
export type SaveTravelLostReason = Omit<TravelLostReason, 'id'>;

export type BadgeVariant = 'success' | 'warning' | 'error' | 'info' | 'pending' | 'active' | 'inactive';

// ── Phase 2: enquiries ─────────────────────────────────────────────────

export type TravelServiceType = 'Visa' | 'Umrah' | 'Hajj' | 'Flight' | 'Hotel' | 'Package' | 'Other';
export const TRAVEL_SERVICE_TYPES: TravelServiceType[] = ['Visa', 'Umrah', 'Hajj', 'Flight', 'Hotel', 'Package', 'Other'];

export type TravelEnquiryStatus = 'New' | 'Contacted' | 'Qualified' | 'Quoted' | 'Won' | 'Lost';
export const TRAVEL_ENQUIRY_STATUSES: TravelEnquiryStatus[] = ['New', 'Contacted', 'Qualified', 'Quoted', 'Won', 'Lost'];

export type TravelPriority = 'Low' | 'Normal' | 'High';
export const TRAVEL_PRIORITIES: TravelPriority[] = ['Low', 'Normal', 'High'];

export type TravelActivityType = 'Call' | 'WhatsApp' | 'Sms' | 'Email' | 'Meeting' | 'Note' | 'StatusChange';
/** Types a user may log (StatusChange is written by the server). */
export const TRAVEL_USER_ACTIVITY_TYPES: Exclude<TravelActivityType, 'StatusChange'>[] = ['Call', 'WhatsApp', 'Sms', 'Email', 'Meeting', 'Note'];

export type TravelActivityDirection = 'Inbound' | 'Outbound' | 'Internal';
export const TRAVEL_ACTIVITY_DIRECTIONS: TravelActivityDirection[] = ['Inbound', 'Outbound', 'Internal'];

export type TravelEnquiryAction =
  | 'Edit' | 'LogActivity' | 'MarkContacted' | 'MarkQualified' | 'MarkQuoted' | 'MarkLost' | 'Reopen' | 'Convert';

export const ENQUIRY_STATUS_LABEL: Record<TravelEnquiryStatus, string> = {
  New: 'New', Contacted: 'Contacted', Qualified: 'Qualified', Quoted: 'Quoted', Won: 'Won', Lost: 'Lost'
};
export const ENQUIRY_STATUS_VARIANT: Record<TravelEnquiryStatus, BadgeVariant> = {
  New: 'info', Contacted: 'pending', Qualified: 'active', Quoted: 'warning', Won: 'success', Lost: 'inactive'
};
export const ACTIVITY_ICON: Record<TravelActivityType, string> = {
  Call: 'call', WhatsApp: 'chat', Sms: 'sms', Email: 'mail', Meeting: 'groups', Note: 'sticky_note_2', StatusChange: 'swap_horiz'
};

export interface TravelEnquiryListItem {
  id: number;
  enquiryNumber: string;
  customerId: number | null;
  displayName: string;
  displayPhone: string | null;
  serviceType: TravelServiceType;
  destinationCountryCode: string | null;
  destinationCountryName: string | null;
  status: TravelEnquiryStatus;
  priority: TravelPriority;
  assignedAgentUserId: number | null;
  assignedAgentName: string | null;
  nextFollowUpAt: string | null;
  createdAt: string;
}

export interface TravelEnquiryActivity {
  id: number;
  activityType: TravelActivityType;
  direction: TravelActivityDirection;
  summary: string;
  outcome: string | null;
  occurredAt: string;
  nextFollowUpAt: string | null;
  createdByName: string | null;
}

export interface TravelEnquiry extends TravelEnquiryListItem {
  leadName: string | null;
  leadPhone: string | null;
  leadEmail: string | null;
  visaTypeId: number | null;
  visaTypeName: string | null;
  travelDateFrom: string | null;
  travelDateTo: string | null;
  adults: number;
  children: number;
  infants: number;
  budget: number | null;
  currencyCode: string | null;
  leadSourceId: number | null;
  leadSourceName: string | null;
  branchId: number | null;
  branchName: string | null;
  lostReasonId: number | null;
  lostReasonName: string | null;
  lostNotes: string | null;
  convertedVisaCaseId: number | null;
  convertedVisaCaseNumber: string | null;
  closedAt: string | null;
  notes: string | null;
  allowedActions: TravelEnquiryAction[];
  activities: TravelEnquiryActivity[];
}

export interface SaveTravelEnquiry {
  customerId: number | null;
  leadName: string | null;
  leadPhone: string | null;
  leadEmail: string | null;
  serviceType: TravelServiceType;
  destinationCountryCode: string | null;
  visaTypeId: number | null;
  travelDateFrom: string | null;
  travelDateTo: string | null;
  adults: number;
  children: number;
  infants: number;
  budget: number | null;
  currencyCode: string | null;
  leadSourceId: number | null;
  assignedAgentUserId: number | null;
  branchId: number | null;
  priority: TravelPriority;
  /** ISO UTC timestamp. */
  nextFollowUpAt: string | null;
  notes: string | null;
}

export interface LogTravelEnquiryActivity {
  activityType: Exclude<TravelActivityType, 'StatusChange'>;
  direction: TravelActivityDirection;
  summary: string;
  outcome: string | null;
  occurredAt: string | null;
  nextFollowUpAt: string | null;
}

export interface ConvertTravelEnquiry {
  existingCustomerId: number | null;
  newCustomerKind: TravelCustomerKind | null;
  createVisaCase: boolean;
  visaCountryCode: string | null;
  visaTypeId: number | null;
  intendedTravelDate: string | null;
}

export interface TravelEnquiryConversionResult { customerId: number; visaCaseId: number | null; visaCaseNumber: string | null; }

export interface TravelEnquirySummary { counts: Record<TravelEnquiryStatus, number>; total: number; followUpsDue: number; }

export interface TravelEnquiryQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: TravelEnquiryStatus | '';
  serviceType?: TravelServiceType | '';
  agentUserId?: number | null;
  /** yyyy-MM-dd */
  createdFrom?: string;
  /** yyyy-MM-dd */
  createdTo?: string;
  followUpDue?: boolean;
}

// ── Phase 2: visa cases ────────────────────────────────────────────────

export type TravelVisaCaseStatus =
  | 'Draft' | 'DocumentsPending' | 'DocumentsVerified' | 'Submitted' | 'InProcessing'
  | 'Approved' | 'PartiallyApproved' | 'Rejected' | 'OnHold' | 'Cancelled';
export const TRAVEL_VISA_CASE_STATUSES: TravelVisaCaseStatus[] =
  ['Draft', 'DocumentsPending', 'DocumentsVerified', 'Submitted', 'InProcessing', 'Approved', 'PartiallyApproved', 'Rejected', 'OnHold', 'Cancelled'];
/** Stages shown as the pipeline strip, in order. */
export const VISA_PIPELINE_STAGES: TravelVisaCaseStatus[] =
  ['Draft', 'DocumentsPending', 'DocumentsVerified', 'Submitted', 'InProcessing', 'Approved', 'PartiallyApproved', 'Rejected', 'OnHold', 'Cancelled'];

export type TravelApplicantDecision = 'Pending' | 'Approved' | 'Rejected' | 'Withdrawn';
export type TravelVisaAction =
  | 'Edit' | 'StartDocuments' | 'VerifyDocuments' | 'ReopenDocuments' | 'Submit' | 'StartProcessing'
  | 'Hold' | 'Resume' | 'Cancel' | 'ManageApplicants' | 'RecordDecision';
/** Actions sent to POST /transition (Submit and decisions have their own endpoints). */
export type TravelVisaTransitionAction =
  'StartDocuments' | 'VerifyDocuments' | 'ReopenDocuments' | 'StartProcessing' | 'Hold' | 'Resume' | 'Cancel';
export type TravelVisaHistoryEvent = 'CaseStatus' | 'ApplicantAdded' | 'ApplicantRemoved' | 'ApplicantDecision'
  | 'DocumentUploaded' | 'DocumentVerified' | 'DocumentRejected' | 'DocumentWaived' | 'DocumentRequested' | 'DocumentRemoved';

export const VISA_STATUS_LABEL: Record<TravelVisaCaseStatus, string> = {
  Draft: 'Draft', DocumentsPending: 'Documents pending', DocumentsVerified: 'Documents verified', Submitted: 'Submitted',
  InProcessing: 'In processing', Approved: 'Approved', PartiallyApproved: 'Partly approved', Rejected: 'Rejected',
  OnHold: 'On hold', Cancelled: 'Cancelled'
};
export const VISA_STATUS_VARIANT: Record<TravelVisaCaseStatus, BadgeVariant> = {
  Draft: 'inactive', DocumentsPending: 'pending', DocumentsVerified: 'info', Submitted: 'active', InProcessing: 'warning',
  Approved: 'success', PartiallyApproved: 'warning', Rejected: 'error', OnHold: 'pending', Cancelled: 'inactive'
};
export const APPLICANT_DECISION_VARIANT: Record<TravelApplicantDecision, BadgeVariant> = {
  Pending: 'pending', Approved: 'success', Rejected: 'error', Withdrawn: 'inactive'
};
export const VISA_TRANSITION_LABEL: Record<TravelVisaTransitionAction, string> = {
  StartDocuments: 'Request documents', VerifyDocuments: 'Mark documents verified', ReopenDocuments: 'Reopen documents',
  StartProcessing: 'Mark in processing', Hold: 'Put on hold', Resume: 'Resume', Cancel: 'Cancel case'
};

export interface TravelVisaCaseListItem {
  id: number;
  caseNumber: string;
  /** Null for an Umrah group visa case (see groupId). */
  customerId: number | null;
  groupId: number | null;
  /** Customer name, or the group name for a group case. */
  customerName: string;
  countryCode: string;
  countryName: string;
  visaTypeId: number;
  visaTypeName: string;
  status: TravelVisaCaseStatus;
  priority: TravelPriority;
  applicantCount: number;
  approvedCount: number;
  intendedTravelDate: string | null;
  appointmentAt: string | null;
  submittedAt: string | null;
  assignedAgentUserId: number | null;
  assignedAgentName: string | null;
  createdAt: string;
}

export interface TravelVisaApplicant {
  id: number;
  travelerId: number;
  travelerName: string;
  gender: TravelGender;
  dateOfBirth: string | null;
  passportNumber: string | null;
  passportExpiryDate: string | null;
  passportWarning: boolean;
  decision: TravelApplicantDecision;
  visaNumber: string | null;
  visaIssueDate: string | null;
  visaValidFrom: string | null;
  visaExpiryDate: string | null;
  rejectionReason: string | null;
  decidedAt: string | null;
  notes: string | null;
}

export interface TravelVisaHistory {
  id: number;
  eventType: TravelVisaHistoryEvent;
  applicantId: number | null;
  applicantName: string | null;
  fromValue: string | null;
  toValue: string | null;
  note: string | null;
  changedByName: string | null;
  changedAt: string;
}

export interface TravelVisaCase extends TravelVisaCaseListItem {
  enquiryId: number | null;
  enquiryNumber: string | null;
  submissionReference: string | null;
  expectedDecisionDate: string | null;
  decidedAt: string | null;
  onHoldFromStatus: TravelVisaCaseStatus | null;
  branchId: number | null;
  branchName: string | null;
  quotedServiceFee: number | null;
  quotedEmbassyFee: number | null;
  currencyCode: string | null;
  cancelReason: string | null;
  notes: string | null;
  checklistTemplateId: number | null;
  checklistVersion: number | null;
  documentsMandatoryTotal: number;
  documentsMandatoryComplete: number;
  allowedActions: TravelVisaAction[];
  applicants: TravelVisaApplicant[];
  history: TravelVisaHistory[];
}

export interface UpdateTravelVisaCase {
  intendedTravelDate: string | null;
  appointmentAt: string | null;
  expectedDecisionDate: string | null;
  priority: TravelPriority;
  assignedAgentUserId: number | null;
  quotedServiceFee: number | null;
  quotedEmbassyFee: number | null;
  currencyCode: string | null;
  notes: string | null;
}

export interface CreateTravelVisaCase extends UpdateTravelVisaCase {
  customerId: number;
  visaTypeId: number;
  enquiryId: number | null;
}

export interface SubmitTravelVisaCase {
  submittedAt: string;
  submissionReference: string | null;
  expectedDecisionDate: string | null;
  note: string | null;
}

export interface RecordTravelVisaDecision {
  decision: Exclude<TravelApplicantDecision, 'Pending'>;
  visaNumber: string | null;
  visaIssueDate: string | null;
  visaValidFrom: string | null;
  visaExpiryDate: string | null;
  rejectionReason: string | null;
  notes: string | null;
}

export interface TravelVisaDecisionResult { applied: boolean; newCaseStatus: TravelVisaCaseStatus | null; }

export interface TravelVisaCaseSummary { counts: Record<TravelVisaCaseStatus, number>; total: number; }

export interface TravelVisaCaseQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: TravelVisaCaseStatus | '';
  countryCode?: string;
  createdFrom?: string;
  createdTo?: string;
}

// ── Phase 3: checklists & documents ────────────────────────────────────

export type TravelChecklistStatus = 'Draft' | 'Active' | 'Retired';
export type TravelApplicantCondition = 'All' | 'Adult' | 'Minor' | 'Male' | 'Female' | 'FemaleWithMahram';
export const TRAVEL_APPLICANT_CONDITIONS: TravelApplicantCondition[] = ['All', 'Adult', 'Minor', 'Male', 'Female', 'FemaleWithMahram'];
export const APPLICANT_CONDITION_LABEL: Record<TravelApplicantCondition, string> = {
  All: 'Everyone', Adult: 'Adults (18+)', Minor: 'Minors (under 18)', Male: 'Men', Female: 'Women', FemaleWithMahram: 'Women travelling with a mahram'
};
export const CHECKLIST_STATUS_VARIANT: Record<TravelChecklistStatus, BadgeVariant> = { Draft: 'pending', Active: 'success', Retired: 'inactive' };

export type TravelDocumentStatus = 'Missing' | 'Uploaded' | 'Verified' | 'Rejected' | 'Waived';
export const DOCUMENT_STATUS_VARIANT: Record<TravelDocumentStatus, BadgeVariant> = {
  Missing: 'inactive', Uploaded: 'info', Verified: 'success', Rejected: 'error', Waived: 'warning'
};

export interface TravelChecklistItem {
  id: number;
  documentTypeId: number;
  documentTypeCode: string;
  documentTypeName: string;
  isMandatory: boolean;
  applicantCondition: TravelApplicantCondition;
  minValidityDays: number | null;
  instructions: string | null;
  sortOrder: number;
}

export interface TravelChecklistListItem {
  id: number;
  countryCode: string;
  countryName: string;
  visaTypeId: number | null;
  visaTypeName: string | null;
  name: string;
  version: number;
  status: TravelChecklistStatus;
  itemCount: number;
  activatedAt: string | null;
  createdAt: string;
}

export interface TravelChecklist extends TravelChecklistListItem { notes: string | null; items: TravelChecklistItem[]; }

export interface SaveTravelChecklist { countryCode: string; visaTypeId: number | null; name: string; notes: string | null; }
export interface SaveTravelChecklistItem {
  documentTypeId: number;
  isMandatory: boolean;
  applicantCondition: TravelApplicantCondition;
  minValidityDays: number | null;
  instructions: string | null;
}

export interface TravelDocumentFile {
  id: number;
  applicantDocumentId: number;
  originalFileName: string;
  contentType: 'application/pdf' | 'image/jpeg' | 'image/png';
  sizeBytes: number;
  uploadedByName: string | null;
  uploadedAt: string;
}

export interface TravelApplicantDocument {
  id: number;
  applicantId: number;
  travelerId: number;
  travelerName: string;
  documentTypeId: number;
  documentTypeCode: string;
  documentTypeName: string;
  isMandatory: boolean;
  isAdHoc: boolean;
  minValidityDays: number | null;
  instructions: string | null;
  status: TravelDocumentStatus;
  expiryDate: string | null;
  reviewNote: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  currentFileId: number | null;
  files: TravelDocumentFile[];
}

export interface TravelCaseDocuments {
  visaCaseId: number;
  caseStatus: TravelVisaCaseStatus;
  checklistTemplateId: number | null;
  checklistName: string | null;
  checklistVersion: number | null;
  mandatoryTotal: number;
  mandatoryComplete: number;
  canEdit: boolean;
  requirements: TravelApplicantDocument[];
}

export interface TravelMissingDocumentsItem {
  visaCaseId: number;
  caseNumber: string;
  customerId: number | null;
  customerName: string;
  countryName: string;
  visaTypeName: string;
  status: TravelVisaCaseStatus;
  intendedTravelDate: string | null;
  mandatoryOutstanding: number;
}

export interface MrzPassportData {
  surname: string;
  givenNames: string;
  passportNumber: string;
  issuingCountryCode: string | null;
  nationalityCode: string | null;
  dateOfBirth: string | null;
  gender: TravelGender | null;
  expiryDate: string | null;
  checksumValid: boolean;
  invalidFields: string[];
}

export interface TravelPassportScanResult { available: boolean; found: boolean; passport: MrzPassportData | null; message: string; }

/** Mirrors TravelDocumentFileValidator on the server (pre-check only; the server decides). */
export const DOCUMENT_UPLOAD_ACCEPT = '.pdf,.jpg,.jpeg,.png';
export const DOCUMENT_UPLOAD_MAX_BYTES = 10 * 1024 * 1024;

// ── Phase 4: hotels, packages, Umrah groups ────────────────────────────

export type TravelHotelCity = 'Makkah' | 'Madinah' | 'Jeddah' | 'Taif' | 'Other';
export const TRAVEL_HOTEL_CITIES: TravelHotelCity[] = ['Makkah', 'Madinah', 'Jeddah', 'Taif', 'Other'];
export type TravelPackageType = 'Umrah' | 'Hajj';
export type TravelPackageStatus = 'Draft' | 'Published' | 'Archived';
export type TravelRoomType = 'Quad' | 'Triple' | 'Double' | 'Single';
export const TRAVEL_ROOM_TYPES: TravelRoomType[] = ['Quad', 'Triple', 'Double', 'Single'];
export const ROOM_CAPACITY: Record<TravelRoomType, number> = { Quad: 4, Triple: 3, Double: 2, Single: 1 };
export type TravelPriceCategory = 'Adult' | 'Child' | 'Infant';
export const TRAVEL_PRICE_CATEGORIES: TravelPriceCategory[] = ['Adult', 'Child', 'Infant'];
export type TravelGroupStatus =
  'Draft' | 'Open' | 'RegistrationClosed' | 'VisaProcessing' | 'Ready' | 'Departed' | 'Returned' | 'Closed' | 'Cancelled';
export const TRAVEL_GROUP_STATUSES: TravelGroupStatus[] =
  ['Draft', 'Open', 'RegistrationClosed', 'VisaProcessing', 'Ready', 'Departed', 'Returned', 'Closed', 'Cancelled'];
export type TravelMemberStatus = 'Registered' | 'Confirmed' | 'Cancelled';
export type TravelGenderPolicy = 'Male' | 'Female' | 'Family';
export const TRAVEL_GENDER_POLICIES: TravelGenderPolicy[] = ['Male', 'Female', 'Family'];
export type TravelTransportLegType = 'AirportTransfer' | 'IntercityTransfer' | 'Ziyarat' | 'Other';
export const TRAVEL_TRANSPORT_LEG_TYPES: TravelTransportLegType[] = ['AirportTransfer', 'IntercityTransfer', 'Ziyarat', 'Other'];
export type TravelGroupAction =
  | 'Edit' | 'Open' | 'CloseRegistration' | 'Reopen' | 'StartVisaProcessing' | 'MarkReady' | 'Unready' | 'Depart' | 'Return'
  | 'Close' | 'Cancel' | 'RegisterMembers' | 'ManageMembers' | 'ManageStays' | 'ManageRooms' | 'ManageTransport';
export type TravelGroupTransitionAction =
  'Open' | 'CloseRegistration' | 'Reopen' | 'StartVisaProcessing' | 'MarkReady' | 'Unready' | 'Depart' | 'Return' | 'Close' | 'Cancel';
export type TravelGroupHistoryEvent =
  'GroupStatus' | 'MemberRegistered' | 'MemberConfirmed' | 'MemberCancelled' | 'RoomAssigned' | 'RoomUnassigned' | 'LeaderChanged' | 'ManifestGenerated';

export const GROUP_STATUS_LABEL: Record<TravelGroupStatus, string> = {
  Draft: 'Draft', Open: 'Registration open', RegistrationClosed: 'Registration closed', VisaProcessing: 'Visa processing',
  Ready: 'Ready', Departed: 'Departed', Returned: 'Returned', Closed: 'Closed', Cancelled: 'Cancelled'
};
export const GROUP_STATUS_VARIANT: Record<TravelGroupStatus, BadgeVariant> = {
  Draft: 'inactive', Open: 'info', RegistrationClosed: 'pending', VisaProcessing: 'warning', Ready: 'success',
  Departed: 'active', Returned: 'active', Closed: 'inactive', Cancelled: 'error'
};
export const GROUP_TRANSITION_LABEL: Record<TravelGroupTransitionAction, string> = {
  Open: 'Open registration', CloseRegistration: 'Close registration', Reopen: 'Reopen registration',
  StartVisaProcessing: 'Start visa processing', MarkReady: 'Mark ready', Unready: 'Back to visa processing',
  Depart: 'Mark departed', Return: 'Mark returned', Close: 'Close group', Cancel: 'Cancel group'
};
export const MEMBER_STATUS_VARIANT: Record<TravelMemberStatus, BadgeVariant> = { Registered: 'pending', Confirmed: 'success', Cancelled: 'inactive' };
export const PACKAGE_STATUS_VARIANT: Record<TravelPackageStatus, BadgeVariant> = { Draft: 'pending', Published: 'success', Archived: 'inactive' };
export const LEG_TYPE_LABEL: Record<TravelTransportLegType, string> = {
  AirportTransfer: 'Airport transfer', IntercityTransfer: 'Intercity transfer', Ziyarat: 'Ziyarat', Other: 'Other'
};

export interface TravelHotel {
  id: number; name: string; city: TravelHotelCity; countryCode: string; starRating: number | null;
  distanceToHaramMeters: number | null; address: string | null; phone: string | null; isActive: boolean;
}
export type SaveTravelHotel = Omit<TravelHotel, 'id'>;

export interface TravelPackageStay { hotelId: number; hotelName: string; city: TravelHotelCity; nights: number; sequence: number; }
export interface TravelPackagePrice { roomType: TravelRoomType; adultPrice: number; childPrice: number; infantPrice: number; }
export interface TravelPackageListItem {
  id: number; code: string; name: string; packageType: TravelPackageType; durationNights: number; nightsMakkah: number;
  nightsMadinah: number; visaTypeId: number | null; visaTypeName: string | null; currencyCode: string; status: TravelPackageStatus;
  fromPrice: number | null; groupCount: number; createdAt: string;
}
export interface TravelPackage extends TravelPackageListItem {
  inclusions: string | null; exclusions: string | null; stays: TravelPackageStay[]; prices: TravelPackagePrice[];
}
export interface SaveTravelPackage {
  code: string; name: string; packageType: TravelPackageType; durationNights: number; nightsMakkah: number; nightsMadinah: number;
  visaTypeId: number | null; inclusions: string | null; exclusions: string | null; currencyCode: string;
  stays: { hotelId: number; nights: number }[]; prices: TravelPackagePrice[];
}

export interface TravelGroupListItem {
  id: number; groupNumber: string; name: string; packageId: number; packageName: string; packageType: TravelPackageType;
  departureDate: string; returnDate: string; capacity: number; activeMembers: number; confirmedMembers: number;
  status: TravelGroupStatus; leaderDisplayName: string | null; visaCaseId: number | null; createdAt: string;
}
export interface TravelGroupMember {
  id: number; travelerId: number; travelerName: string; gender: TravelGender; dateOfBirth: string | null; customerId: number;
  customerName: string; mahramTravelerId: number | null; passportNumber: string | null; passportExpiryDate: string | null;
  roomPreference: TravelRoomType; priceCategory: TravelPriceCategory; quotedPrice: number | null; currencyCode: string | null;
  status: TravelMemberStatus; cancelReason: string | null; visaDecision: TravelApplicantDecision | null; visaNumber: string | null;
  visaExpiryDate: string | null;
}
export interface TravelGroupStay {
  id: number; hotelId: number; hotelName: string; city: TravelHotelCity; checkIn: string; checkOut: string; sequence: number;
  confirmationNumber: string | null; roomCount: number; assignedCount: number;
}
export interface TravelGroupRoom {
  id: number; groupHotelId: number; roomNumber: string | null; roomType: TravelRoomType; capacity: number;
  genderPolicy: TravelGenderPolicy; memberIds: number[];
}
export interface TravelGroupTransport {
  id: number; legType: TravelTransportLegType; departAt: string; fromPlace: string; toPlace: string; providerName: string | null;
  vehicleType: string | null; seats: number | null; fleetBookingId: number | null; fleetBookingNumber: string | null; notes: string | null;
}
export interface TravelGroupHistory {
  id: number; eventType: TravelGroupHistoryEvent; memberId: number | null; memberName: string | null; fromValue: string | null;
  toValue: string | null; note: string | null; changedByName: string | null; changedAt: string;
}
export interface TravelGroupReadiness {
  confirmedMembers: number; unconfirmedMembers: number; membersWithApprovedVisa: number; membersWithoutVisa: number;
  membersWithoutRoom: number; membersWithoutTickets: number; stays: number; visaCaseDecided: boolean; isReady: boolean;
}
export interface TravelGroup extends TravelGroupListItem {
  groupLeaderTravelerId: number | null; leaderName: string | null; leaderPhone: string | null; visaCaseNumber: string | null;
  visaCaseStatus: TravelVisaCaseStatus | null; branchId: number | null; branchName: string | null; cancelReason: string | null;
  closedAt: string | null; notes: string | null; packageCurrencyCode: string; packageStatus: TravelPackageStatus;
  allowedActions: TravelGroupAction[]; readiness: TravelGroupReadiness; members: TravelGroupMember[]; stays: TravelGroupStay[];
  rooms: TravelGroupRoom[]; transport: TravelGroupTransport[]; history: TravelGroupHistory[];
}
export interface TravelGroupSummary { counts: Record<TravelGroupStatus, number>; total: number; }
export interface TravelRoomingRoom { roomNumber: string | null; roomType: TravelRoomType; genderPolicy: TravelGenderPolicy; memberIds: number[]; }

export interface TravelManifestPilgrim {
  no: number; memberId: number; surname: string; givenNames: string; gender: TravelGender; dateOfBirth: string | null;
  nationalityCode: string | null; passportNumber: string | null; passportExpiryDate: string | null; visaNumber: string | null;
  visaExpiryDate: string | null; mahramName: string | null; phone: string | null; customerName: string; rooms: string[];
}
export interface TravelGroupManifest {
  groupNumber: string; groupName: string; packageName: string; departureDate: string; returnDate: string; leaderName: string | null;
  leaderPhone: string | null; visaCaseNumber: string | null; piiMasked: boolean; generatedAtUtc: string; stays: TravelGroupStay[];
  transport: TravelGroupTransport[]; pilgrims: TravelManifestPilgrim[];
}

// ── Phase 5: suppliers and bookings ─────────────────────────────────────

export type TravelSupplierType = 'Airline' | 'Hotel' | 'BedBank' | 'Consolidator' | 'Transport' | 'Other';
export const TRAVEL_SUPPLIER_TYPES: TravelSupplierType[] = ['Airline', 'Hotel', 'BedBank', 'Consolidator', 'Transport', 'Other'];
export const SUPPLIER_TYPE_LABEL: Record<TravelSupplierType, string> = {
  Airline: 'Airline', Hotel: 'Hotel', BedBank: 'Bed bank', Consolidator: 'Ticket consolidator', Transport: 'Transport', Other: 'Other'
};
export type TravelBookingStatus = 'Open' | 'Completed' | 'Cancelled';
export const TRAVEL_BOOKING_STATUSES: TravelBookingStatus[] = ['Open', 'Completed', 'Cancelled'];
export type TravelFlightDirection = 'Outbound' | 'Return' | 'RoundTrip' | 'OneWay' | 'Internal';
export const TRAVEL_FLIGHT_DIRECTIONS: TravelFlightDirection[] = ['Outbound', 'Return', 'RoundTrip', 'OneWay', 'Internal'];
export type TravelFlightStatus = 'Requested' | 'OnHold' | 'Confirmed' | 'Ticketed' | 'Cancelled';
export const TRAVEL_FLIGHT_STATUSES: TravelFlightStatus[] = ['Requested', 'OnHold', 'Confirmed', 'Ticketed', 'Cancelled'];
export type TravelCabinClass = 'Economy' | 'PremiumEconomy' | 'Business' | 'First';
export const TRAVEL_CABIN_CLASSES: TravelCabinClass[] = ['Economy', 'PremiumEconomy', 'Business', 'First'];
export type TravelPassengerType = 'Adult' | 'Child' | 'Infant';
export type TravelTicketStatus = 'NotIssued' | 'Issued' | 'Voided' | 'Refunded';
export type TravelHotelBookingStatus = 'Requested' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'NoShow' | 'Cancelled';
export const TRAVEL_HOTEL_BOOKING_STATUSES: TravelHotelBookingStatus[] = ['Requested', 'Confirmed', 'CheckedIn', 'CheckedOut', 'NoShow', 'Cancelled'];
export type TravelMealPlan = 'RoomOnly' | 'Breakfast' | 'HalfBoard' | 'FullBoard';
export const TRAVEL_MEAL_PLANS: TravelMealPlan[] = ['RoomOnly', 'Breakfast', 'HalfBoard', 'FullBoard'];
export type TravelRefundStatus = 'NotApplicable' | 'Pending' | 'ReceivedFromSupplier' | 'PaidToCustomer' | 'Closed';
export const TRAVEL_REFUND_STATUSES: TravelRefundStatus[] = ['NotApplicable', 'Pending', 'ReceivedFromSupplier', 'PaidToCustomer', 'Closed'];
export type TravelBookingAction = 'Edit' | 'ManageServices' | 'Complete' | 'Reopen' | 'Cancel';
export type TravelBookingTransitionAction = 'Complete' | 'Reopen' | 'Cancel';
export type TravelFlightAction = 'Edit' | 'EditPassengers' | 'Hold' | 'Confirm' | 'Ticket' | 'IssueTickets' | 'CancelPassenger' | 'Cancel' | 'Delete';
export type TravelFlightTransitionAction = 'Hold' | 'Confirm' | 'Ticket';
export type TravelHotelAction = 'Edit' | 'Confirm' | 'CheckIn' | 'CheckOut' | 'NoShow' | 'Cancel' | 'Delete';
export type TravelHotelTransitionAction = 'Confirm' | 'CheckIn' | 'CheckOut' | 'NoShow';
export type TravelBookingHistoryEvent =
  'FileStatus' | 'FlightStatus' | 'HotelStatus' | 'TicketIssued' | 'TicketVoided' | 'Cancelled' | 'RefundStatus' | 'ServiceAdded' | 'ServiceUpdated';

export const BOOKING_STATUS_VARIANT: Record<TravelBookingStatus, BadgeVariant> = { Open: 'info', Completed: 'success', Cancelled: 'error' };
export const FLIGHT_STATUS_LABEL: Record<TravelFlightStatus, string> = {
  Requested: 'Requested', OnHold: 'On hold', Confirmed: 'Confirmed', Ticketed: 'Ticketed', Cancelled: 'Cancelled'
};
export const FLIGHT_STATUS_VARIANT: Record<TravelFlightStatus, BadgeVariant> = {
  Requested: 'pending', OnHold: 'warning', Confirmed: 'info', Ticketed: 'success', Cancelled: 'error'
};
export const DIRECTION_LABEL: Record<TravelFlightDirection, string> = {
  Outbound: 'Outbound', Return: 'Return', RoundTrip: 'Round trip', OneWay: 'One way', Internal: 'Internal'
};
export const CABIN_LABEL: Record<TravelCabinClass, string> = { Economy: 'Economy', PremiumEconomy: 'Premium economy', Business: 'Business', First: 'First' };
export const TICKET_STATUS_VARIANT: Record<TravelTicketStatus, BadgeVariant> = { NotIssued: 'pending', Issued: 'success', Voided: 'inactive', Refunded: 'warning' };
export const HOTEL_BOOKING_STATUS_LABEL: Record<TravelHotelBookingStatus, string> = {
  Requested: 'Requested', Confirmed: 'Confirmed', CheckedIn: 'Checked in', CheckedOut: 'Checked out', NoShow: 'No-show', Cancelled: 'Cancelled'
};
export const HOTEL_BOOKING_STATUS_VARIANT: Record<TravelHotelBookingStatus, BadgeVariant> = {
  Requested: 'pending', Confirmed: 'info', CheckedIn: 'active', CheckedOut: 'success', NoShow: 'warning', Cancelled: 'error'
};
export const MEAL_PLAN_LABEL: Record<TravelMealPlan, string> = { RoomOnly: 'Room only', Breakfast: 'Breakfast', HalfBoard: 'Half board', FullBoard: 'Full board' };
export const REFUND_STATUS_LABEL: Record<TravelRefundStatus, string> = {
  NotApplicable: 'No refund', Pending: 'Pending', ReceivedFromSupplier: 'Received from supplier', PaidToCustomer: 'Paid to customer', Closed: 'Closed'
};
export const REFUND_STATUS_VARIANT: Record<TravelRefundStatus, BadgeVariant> = {
  NotApplicable: 'inactive', Pending: 'warning', ReceivedFromSupplier: 'info', PaidToCustomer: 'active', Closed: 'success'
};
export const FLIGHT_TRANSITION_LABEL: Record<TravelFlightTransitionAction, string> = { Hold: 'Hold / extend', Confirm: 'Confirm', Ticket: 'Mark ticketed' };
export const HOTEL_TRANSITION_LABEL: Record<TravelHotelTransitionAction, string> = { Confirm: 'Confirm', CheckIn: 'Check in', CheckOut: 'Check out', NoShow: 'No-show' };

export interface TravelSupplier {
  id: number; name: string; supplierType: TravelSupplierType; iataCode: string | null; contactName: string | null; phone: string | null;
  email: string | null; currencyCode: string | null; isActive: boolean; notes: string | null; usageCount: number;
}
export type SaveTravelSupplier = Omit<TravelSupplier, 'id' | 'usageCount'>;
export interface TravelAirport { iataCode: string; name: string; city: string; countryCode: string; ianaTimeZone: string; }

export interface TravelBookingListItem {
  id: number; bookingNumber: string; title: string; customerId: number | null; groupId: number | null; ownerName: string;
  status: TravelBookingStatus; firstTravelDate: string | null; lastTravelDate: string | null; flightCount: number; hotelCount: number;
  pnrs: string | null; nextTicketingLimitUtc: string | null; assignedAgentName: string | null; createdAt: string;
}
export interface TravelFlightSegment {
  id: number; flightBookingId: number; sequence: number; airlineCode: string; flightNumber: string; fromAirport: string; fromCity: string;
  toAirport: string; toCity: string; departLocal: string; arriveLocal: string; departUtc: string; arriveUtc: string; cabinClass: TravelCabinClass;
  bookingClass: string | null; baggage: string | null;
}
export interface TravelFlightPassenger {
  id: number; flightBookingId: number; travelerId: number; travelerName: string; groupMemberId: number | null; passengerType: TravelPassengerType;
  passportNumber: string | null; ticketNumber: string | null; ticketStatus: TravelTicketStatus;
}
export interface TravelFlightBooking {
  id: number; supplierId: number | null; supplierName: string | null; direction: TravelFlightDirection; pnr: string | null; airlinePnr: string | null;
  status: TravelFlightStatus; ticketingTimeLimitUtc: string | null; sellAmount: number | null; costAmount: number | null; currencyCode: string | null;
  notes: string | null; allowedActions: TravelFlightAction[]; segments: TravelFlightSegment[]; passengers: TravelFlightPassenger[];
}
export interface TravelHotelBooking {
  id: number; hotelId: number; hotelName: string; city: TravelHotelCity; hotelAddress: string | null; hotelPhone: string | null;
  supplierId: number | null; supplierName: string | null; groupHotelId: number | null; checkIn: string; checkOut: string; nights: number;
  mealPlan: TravelMealPlan; confirmationNumber: string | null; status: TravelHotelBookingStatus; specialRequests: string | null;
  sellAmount: number | null; costAmount: number | null; currencyCode: string | null; allowedActions: TravelHotelAction[];
  rooms: { hotelBookingId: number; roomType: TravelRoomType; quantity: number }[];
  guests: { hotelBookingId: number; travelerId: number; travelerName: string }[];
}
export interface TravelBookingCancellation {
  id: number; serviceType: 'Flight' | 'Hotel'; flightBookingId: number | null; hotelBookingId: number | null; flightPassengerId: number | null;
  serviceLabel: string; reason: string; supplierPenalty: number; customerPenalty: number; refundFromSupplier: number; refundToCustomer: number;
  currencyCode: string | null; refundStatus: TravelRefundStatus; nextRefundStatuses: TravelRefundStatus[]; cancelledAt: string; cancelledByName: string | null;
}
export interface TravelBookingHistory {
  id: number; serviceType: 'Flight' | 'Hotel' | null; serviceId: number | null; eventType: TravelBookingHistoryEvent; fromValue: string | null;
  toValue: string | null; note: string | null; changedByName: string | null; changedAt: string;
}
export interface TravelBookingTotal { currencyCode: string; sell: number; cost: number; margin: number; }
export interface TravelBooking extends TravelBookingListItem {
  customerName: string | null; groupName: string | null; groupNumber: string | null; enquiryId: number | null; enquiryNumber: string | null;
  visaCaseId: number | null; visaCaseNumber: string | null; assignedAgentUserId: number | null; branchId: number | null; branchName: string | null;
  notes: string | null; completedAt: string | null; cancelReason: string | null; allowedActions: TravelBookingAction[];
  flights: TravelFlightBooking[]; hotels: TravelHotelBooking[]; cancellations: TravelBookingCancellation[]; history: TravelBookingHistory[];
  totals: TravelBookingTotal[];
}
export interface TravelBookingSummary { counts: Record<TravelBookingStatus, number>; total: number; ticketingDueNext72Hours: number; }

export interface SaveTravelFlightSegment {
  airlineCode: string; flightNumber: string; fromAirport: string; toAirport: string; departLocal: string; arriveLocal: string;
  cabinClass: TravelCabinClass; bookingClass: string | null; baggage: string | null;
}
export interface SaveTravelFlight {
  supplierId: number | null; direction: TravelFlightDirection; pnr: string | null; airlinePnr: string | null; sellAmount: number | null;
  costAmount: number | null; currencyCode: string | null; notes: string | null; segments: SaveTravelFlightSegment[];
  passengers: { travelerId: number; groupMemberId: number | null; passengerType: TravelPassengerType }[];
}
export interface SaveTravelHotelBooking {
  hotelId: number; supplierId: number | null; groupHotelId: number | null; checkIn: string; checkOut: string; mealPlan: TravelMealPlan;
  confirmationNumber: string | null; specialRequests: string | null; sellAmount: number | null; costAmount: number | null; currencyCode: string | null;
  rooms: { roomType: TravelRoomType; quantity: number }[]; guestTravelerIds: number[];
}
export interface CancelTravelService {
  reason: string; supplierPenalty: number; customerPenalty: number; refundFromSupplier: number; refundToCustomer: number;
  ticketOutcome: 'Voided' | 'Refunded' | null;
}
