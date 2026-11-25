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
  documentDownload: 'Travel.Document.Download'
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
  customerId: number;
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
  customerId: number;
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
