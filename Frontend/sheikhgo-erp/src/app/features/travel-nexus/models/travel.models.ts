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
  bookingCancel: 'Travel.Booking.Cancel',
  financeView: 'Travel.Finance.View',
  invoiceManage: 'Travel.Invoice.Manage',
  paymentManage: 'Travel.Payment.Manage',
  financeAdjust: 'Travel.Finance.Adjust',
  financeReconcile: 'Travel.Finance.Reconcile',
  supplierBillManage: 'Travel.SupplierBill.Manage',
  supplierPaymentManage: 'Travel.SupplierPayment.Manage',
  commissionView: 'Travel.Commission.View',
  commissionManage: 'Travel.Commission.Manage',
  profitabilityView: 'Travel.Profitability.View',
  reminderView: 'Travel.Reminder.View',
  reminderManage: 'Travel.Reminder.Manage',
  dashboardView: 'Travel.Dashboard.View',
  reportView: 'Travel.Report.View'
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
  receiptPrefix: string;
  creditNotePrefix: string;
  refundPrefix: string;
  defaultPaymentTermsDays: number;
  passportExpiryWarningDays: number;
  /** Currency profitability converts into; null falls back to the default currency. Sending null keeps the stored value. */
  reportingCurrencyCode: string | null;
  supplierBillPrefix: string;
  supplierPaymentPrefix: string;
  supplierCreditPrefix: string;
  supplierRefundPrefix: string;
  payoutPrefix: string;
  /** Customer reminders are not sent between these tenant-local hours (start 21, end 8 = 21:00–08:00). */
  reminderQuietStartHour: number;
  reminderQuietEndHour: number;
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
  /** Sub-agent who referred the customer (default sub-agent on their invoices). */
  subAgentId: number | null;
  subAgentName: string | null;
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
  subAgentId: number | null;
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

// ── Phase 6A: customer finance ──────────────────────────────────────────

export type TravelInvoiceStatus = 'Draft' | 'Issued' | 'Void';
export const TRAVEL_INVOICE_STATUSES: TravelInvoiceStatus[] = ['Draft', 'Issued', 'Void'];
export type TravelInvoicePaymentStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'CreditDue';
export const TRAVEL_INVOICE_PAYMENT_STATUSES: TravelInvoicePaymentStatus[] = ['Unpaid', 'PartiallyPaid', 'Paid', 'CreditDue'];
export type TravelInvoiceSourceType = 'Flight' | 'Hotel' | 'VisaServiceFee' | 'VisaEmbassyFee' | 'GroupMember';
export type TravelPaymentMethod = 'Cash' | 'BankTransfer' | 'Card' | 'Cheque' | 'Online' | 'Other';
export const TRAVEL_PAYMENT_METHODS: TravelPaymentMethod[] = ['Cash', 'BankTransfer', 'Card', 'Cheque', 'Online', 'Other'];
export type TravelPaymentStatus = 'Pending' | 'Completed' | 'Failed' | 'Refunded';
export const TRAVEL_PAYMENT_STATUSES: TravelPaymentStatus[] = ['Pending', 'Completed', 'Failed', 'Refunded'];
export type TravelCreditNoteStatus = 'Issued' | 'Void';
export type TravelInstallmentStatus = 'Paid' | 'PartlyPaid' | 'Due' | 'Overdue';
export type TravelInvoiceAction = 'Edit' | 'Issue' | 'Delete' | 'Void' | 'RecordPayment' | 'CreditNote' | 'Refund';
export type TravelPaymentAction = 'Clear' | 'Fail' | 'Allocate' | 'Refund' | 'Reconcile' | 'Unallocate';
export type TravelFinanceHistoryEvent =
  | 'InvoiceCreated' | 'InvoiceUpdated' | 'InvoiceIssued' | 'InvoiceVoided' | 'PaymentRecorded' | 'PaymentStatus' | 'Allocated' | 'Unallocated'
  | 'CreditNoteIssued' | 'CreditNoteVoided' | 'RefundPaid' | 'Reconciled';
export type TravelStatementEntryType = 'Invoice' | 'CreditNote' | 'Payment' | 'Refund' | 'Advance';

export const INVOICE_STATUS_VARIANT: Record<TravelInvoiceStatus, BadgeVariant> = { Draft: 'pending', Issued: 'info', Void: 'inactive' };
export const INVOICE_PAYMENT_STATUS_LABEL: Record<TravelInvoicePaymentStatus, string> = {
  Unpaid: 'Unpaid', PartiallyPaid: 'Partly paid', Paid: 'Paid', CreditDue: 'Credit due'
};
export const INVOICE_PAYMENT_STATUS_VARIANT: Record<TravelInvoicePaymentStatus, BadgeVariant> = {
  Unpaid: 'warning', PartiallyPaid: 'info', Paid: 'success', CreditDue: 'active'
};
export const PAYMENT_METHOD_LABEL: Record<TravelPaymentMethod, string> = {
  Cash: 'Cash', BankTransfer: 'Bank transfer', Card: 'Card', Cheque: 'Cheque', Online: 'Online', Other: 'Other'
};
export const PAYMENT_STATUS_LABEL: Record<TravelPaymentStatus, string> = { Pending: 'Not cleared', Completed: 'Cleared', Failed: 'Failed', Refunded: 'Refunded' };
export const PAYMENT_STATUS_VARIANT: Record<TravelPaymentStatus, BadgeVariant> = { Pending: 'pending', Completed: 'success', Failed: 'error', Refunded: 'inactive' };
export const INSTALLMENT_STATUS_VARIANT: Record<TravelInstallmentStatus, BadgeVariant> = { Paid: 'success', PartlyPaid: 'info', Due: 'pending', Overdue: 'error' };
export const SOURCE_TYPE_LABEL: Record<TravelInvoiceSourceType, string> = {
  Flight: 'Flight', Hotel: 'Hotel', VisaServiceFee: 'Visa service fee', VisaEmbassyFee: 'Embassy fee', GroupMember: 'Umrah package'
};
export const FINANCE_EVENT_LABEL: Record<TravelFinanceHistoryEvent, string> = {
  InvoiceCreated: 'Draft created', InvoiceUpdated: 'Draft edited', InvoiceIssued: 'Invoice issued', InvoiceVoided: 'Invoice voided',
  PaymentRecorded: 'Payment recorded', PaymentStatus: 'Payment status', Allocated: 'Payment applied', Unallocated: 'Payment removed',
  CreditNoteIssued: 'Credit note issued', CreditNoteVoided: 'Credit note voided', RefundPaid: 'Refund paid', Reconciled: 'Reconciled'
};

export interface TravelInvoiceMoney { total: number; paid: number; pendingPaid: number; credited: number; refunded: number; balance: number; }
export interface TravelInvoiceListItem extends TravelInvoiceMoney {
  id: number; invoiceNumber: string | null; customerId: number; customerName: string; currencyCode: string; issueDate: string | null;
  dueDate: string | null; status: TravelInvoiceStatus; paymentStatus: TravelInvoicePaymentStatus | null; nextDueDate: string | null;
  overdueAmount: number; bookingId: number | null; bookingNumber: string | null; visaCaseId: number | null; visaCaseNumber: string | null;
  groupId: number | null; groupNumber: string | null; createdAt: string;
}
export interface TravelInvoiceLine {
  id: number; invoiceId: number; sequence: number; description: string; quantity: number; unitPrice: number; lineTotal: number;
  sourceType: TravelInvoiceSourceType | null; sourceId: number | null; currentSourceAmount: number | null; sourceChanged: boolean;
}
export interface TravelInstallment { invoiceId: number; sequence: number; dueDate: string; amount: number; covered: number; status: TravelInstallmentStatus; }
export interface TravelAllocation {
  paymentId: number; receiptNumber: string; receivedOn: string; method: TravelPaymentMethod; paymentStatus: TravelPaymentStatus; invoiceId: number;
  invoiceNumber: string | null; invoiceCurrencyCode: string; amountFromPayment: number; paymentCurrencyCode: string; exchangeRate: number; amountApplied: number;
}
export interface TravelCreditNote {
  id: number; creditNoteNumber: string; invoiceId: number; invoiceNumber: string | null; customerId: number; customerName: string; amount: number;
  currencyCode: string; reason: string; cancellationId: number | null; status: TravelCreditNoteStatus; issuedOn: string; voidReason: string | null;
}
export interface TravelRefund {
  id: number; refundNumber: string; customerId: number; customerName: string; invoiceId: number | null; invoiceNumber: string | null;
  paymentId: number | null; receiptNumber: string | null; amount: number; currencyCode: string; method: TravelPaymentMethod; reference: string | null;
  paidOn: string; cancellationId: number | null; notes: string | null;
}
export interface TravelFinanceHistory {
  id: number; eventType: TravelFinanceHistoryEvent; fromValue: string | null; toValue: string | null; amount: number | null; currencyCode: string | null;
  note: string | null; changedByName: string | null; changedAt: string;
}
export interface TravelInvoice extends TravelInvoiceListItem {
  subtotal: number; discountAmount: number; notes: string | null; issuedAt: string | null; voidedAt: string | null; voidReason: string | null;
  branchId: number | null; branchName: string | null; agentUserId: number | null; agentName: string | null; subAgentId: number | null;
  subAgentName: string | null; allowedActions: TravelInvoiceAction[]; lines: TravelInvoiceLine[];
  installments: TravelInstallment[]; allocations: TravelAllocation[]; creditNotes: TravelCreditNote[]; refunds: TravelRefund[]; history: TravelFinanceHistory[];
}
export interface TravelBillableItem {
  sourceType: TravelInvoiceSourceType; sourceId: number; customerId: number; customerName: string; description: string; amount: number;
  currencyCode: string; contextType: 'Booking' | 'VisaCase' | 'Group'; contextId: number; contextLabel: string; serviceDate: string | null;
  onInvoiceId: number | null;
}
export interface TravelPaymentListItem {
  id: number; receiptNumber: string; customerId: number; customerName: string; receivedOn: string; method: TravelPaymentMethod; reference: string | null;
  amount: number; currencyCode: string; status: TravelPaymentStatus; allocated: number; refunded: number; unallocated: number;
  reconciledAt: string | null; bankReference: string | null; createdAt: string;
}
export interface TravelPayment extends TravelPaymentListItem {
  notes: string | null; clearedAt: string | null; createdByName: string | null; allowedActions: TravelPaymentAction[];
  allocations: TravelAllocation[]; refunds: TravelRefund[]; history: TravelFinanceHistory[];
}
export interface TravelFinanceCurrencySummary {
  currencyCode: string; outstanding: number; overdue: number; creditDue: number; collectedThisMonth: number; unallocatedAdvances: number;
}
export interface TravelFinanceSummary { currencies: TravelFinanceCurrencySummary[]; invoiceCounts: Record<TravelInvoiceStatus, number>; invoiceTotal: number; }
export interface TravelCustomerBalance {
  customerId: number; customerName: string; currencyCode: string; openInvoices: number; invoiced: number; paid: number; credited: number;
  refunded: number; balance: number; overdue: number; unallocatedAdvances: number;
}
export interface TravelStatementEntry {
  date: string; entryType: TravelStatementEntryType; reference: string; description: string; debit: number; credit: number; balance: number;
  invoiceId: number | null; paymentId: number | null;
}
export interface TravelStatement {
  customerId: number; customerName: string; currencyCode: string; from: string | null; to: string | null; openingBalance: number;
  entries: TravelStatementEntry[]; closingBalance: number; currencies: string[];
}
export interface TravelChangedInvoiceLine {
  invoiceId: number; invoiceNumber: string | null; customerName: string; currencyCode: string; description: string; sourceType: TravelInvoiceSourceType;
  invoiced: number; current: number | null;
}
export interface TravelGroupFinanceRow {
  customerId: number; customerName: string; currencyCode: string; pilgrims: number; quoted: number; uninvoicedPilgrims: number; invoiced: number;
  paid: number; credited: number; balance: number;
}

export interface SaveTravelInvoiceLine { description: string; quantity: number; unitPrice: number; sourceType: TravelInvoiceSourceType | null; sourceId: number | null; }
export interface SaveTravelInstallment { dueDate: string; amount: number; }
export interface SaveTravelInvoice {
  issueDate: string | null; dueDate: string | null; discountAmount: number; notes: string | null; lines: SaveTravelInvoiceLine[]; installments: SaveTravelInstallment[];
  /** Create: null defaults to the file's / customer's agent and the customer's "referred by". Update: null keeps the current value. */
  agentUserId?: number | null; subAgentId?: number | null;
}
export interface TravelAllocationInput { invoiceId: number; amountFromPayment: number; exchangeRate: number | null; }
export interface RecordTravelPayment {
  idempotencyKey: string; customerId: number; receivedOn: string; method: TravelPaymentMethod; reference: string | null; amount: number;
  currencyCode: string; cleared: boolean; notes: string | null; allocations: TravelAllocationInput[];
}
export interface RecordTravelRefund {
  idempotencyKey: string; invoiceId: number | null; paymentId: number | null; amount: number; method: TravelPaymentMethod; reference: string | null;
  paidOn: string; cancellationId: number | null; notes: string | null;
}

// ── Phase 6B: supplier finance, commissions, profitability ─────────────

export type TravelSupplierRateType = 'HotelRoomNight' | 'Transfer' | 'Service';
export const TRAVEL_SUPPLIER_RATE_TYPES: TravelSupplierRateType[] = ['HotelRoomNight', 'Transfer', 'Service'];
export type TravelSupplierCommissionServiceType = 'Flight' | 'Hotel' | 'Other';
export const TRAVEL_SUPPLIER_COMMISSION_SERVICE_TYPES: TravelSupplierCommissionServiceType[] = ['Flight', 'Hotel', 'Other'];
export type TravelSupplierCommissionBasis = 'PercentOfCost' | 'FixedPerUnit';
export const TRAVEL_SUPPLIER_COMMISSION_BASES: TravelSupplierCommissionBasis[] = ['PercentOfCost', 'FixedPerUnit'];
export type TravelSupplierBillStatus = 'Draft' | 'Posted' | 'Void';
export const TRAVEL_SUPPLIER_BILL_STATUSES: TravelSupplierBillStatus[] = ['Draft', 'Posted', 'Void'];
export type TravelSupplierBillSourceType = 'Flight' | 'Hotel';
export type TravelContextType = 'Booking' | 'VisaCase' | 'Group';
export const TRAVEL_CONTEXT_TYPES: TravelContextType[] = ['Booking', 'VisaCase', 'Group'];
export type TravelBeneficiaryType = 'Staff' | 'SubAgent';
export const TRAVEL_BENEFICIARY_TYPES: TravelBeneficiaryType[] = ['Staff', 'SubAgent'];
export type TravelCommissionScope = 'All' | 'Flight' | 'Hotel' | 'Visa' | 'Umrah';
export const TRAVEL_COMMISSION_SCOPES: TravelCommissionScope[] = ['All', 'Flight', 'Hotel', 'Visa', 'Umrah'];
export type TravelCommissionBasis = 'PercentOfMargin' | 'PercentOfSell';
export const TRAVEL_COMMISSION_BASES: TravelCommissionBasis[] = ['PercentOfMargin', 'PercentOfSell'];
export type TravelCommissionStatus = 'Accrued' | 'Payable';
export type TravelProfitDimension = 'Booking' | 'VisaCase' | 'Group' | 'Agent' | 'Branch';
export const TRAVEL_PROFIT_DIMENSIONS: TravelProfitDimension[] = ['Booking', 'VisaCase', 'Group', 'Agent', 'Branch'];
export type TravelSupplierBillAction = 'Edit' | 'Post' | 'Delete' | 'RecordPayment' | 'Credit' | 'Refund' | 'Void';
export type TravelSupplierFinanceEvent =
  | 'BillCreated' | 'BillUpdated' | 'BillPosted' | 'BillVoided' | 'PaymentRecorded' | 'PaymentStatus' | 'Allocated' | 'Unallocated'
  | 'CreditIssued' | 'CreditVoided' | 'RefundReceived' | 'Reconciled' | 'PayoutRecorded' | 'AttributionChanged';
export type TravelSupplierStatementEntryType = 'Bill' | 'SupplierCredit' | 'Payment' | 'RefundReceived' | 'Advance';

export const SUPPLIER_BILL_STATUS_VARIANT: Record<TravelSupplierBillStatus, BadgeVariant> = { Draft: 'pending', Posted: 'info', Void: 'inactive' };
/** Supplier-side wording of the shared payment status (CreditDue = the supplier owes us). */
export const SUPPLIER_BILL_PAYMENT_LABEL: Record<TravelInvoicePaymentStatus, string> = {
  Unpaid: 'Unpaid', PartiallyPaid: 'Partly paid', Paid: 'Paid', CreditDue: 'Supplier owes us'
};
export const SUPPLIER_RATE_TYPE_LABEL: Record<TravelSupplierRateType, string> = { HotelRoomNight: 'Hotel room-night', Transfer: 'Transfer', Service: 'Service' };
export const SUPPLIER_COMMISSION_BASIS_LABEL: Record<TravelSupplierCommissionBasis, string> = {
  PercentOfCost: '% of cost', FixedPerUnit: 'Fixed per passenger / room-night'
};
export const CONTEXT_TYPE_LABEL: Record<TravelContextType, string> = { Booking: 'Booking file', VisaCase: 'Visa case', Group: 'Umrah group' };
export const BENEFICIARY_TYPE_LABEL: Record<TravelBeneficiaryType, string> = { Staff: 'Staff agent', SubAgent: 'Sub-agent' };
export const COMMISSION_BASIS_LABEL: Record<TravelCommissionBasis, string> = { PercentOfMargin: '% of margin', PercentOfSell: '% of sell' };
export const COMMISSION_STATUS_VARIANT: Record<TravelCommissionStatus, BadgeVariant> = { Accrued: 'pending', Payable: 'success' };
export const PROFIT_DIMENSION_LABEL: Record<TravelProfitDimension, string> = {
  Booking: 'Booking files', VisaCase: 'Visa cases', Group: 'Umrah groups', Agent: 'Agents', Branch: 'Branches'
};
export const SUPPLIER_FINANCE_EVENT_LABEL: Record<TravelSupplierFinanceEvent, string> = {
  BillCreated: 'Draft created', BillUpdated: 'Draft edited', BillPosted: 'Bill posted', BillVoided: 'Bill voided', PaymentRecorded: 'Payment recorded',
  PaymentStatus: 'Payment status', Allocated: 'Payment applied', Unallocated: 'Payment removed', CreditIssued: 'Supplier credit',
  CreditVoided: 'Supplier credit voided', RefundReceived: 'Refund received', Reconciled: 'Reconciled', PayoutRecorded: 'Payout recorded',
  AttributionChanged: 'Commission re-assigned'
};

export interface TravelExchangeRate { id: number; currencyCode: string; rateToReporting: number; effectiveFrom: string; }
export interface SaveTravelExchangeRate { currencyCode: string; rateToReporting: number; effectiveFrom: string; }

export interface TravelSupplierRate {
  id: number; supplierId: number; rateType: TravelSupplierRateType; hotelId: number | null; hotelName: string | null; roomType: TravelRoomType | null;
  description: string | null; validFrom: string; validTo: string; amount: number; currencyCode: string; isActive: boolean;
}
export interface SaveTravelSupplierRate {
  rateType: TravelSupplierRateType; hotelId: number | null; roomType: TravelRoomType | null; description: string | null; validFrom: string; validTo: string;
  amount: number; currencyCode: string; isActive: boolean;
}
export interface TravelRateQuoteRequest { supplierId: number; hotelId: number; checkIn: string; checkOut: string; rooms: { roomType: TravelRoomType; quantity: number }[]; }
export interface TravelRateQuote {
  currencyCode: string | null; total: number; nights: number; missing: { roomType: TravelRoomType; night: string }[]; complete: boolean; mixedCurrencies: boolean;
}
export interface TravelSupplierCommissionRule {
  id: number; supplierId: number; serviceType: TravelSupplierCommissionServiceType; basis: TravelSupplierCommissionBasis; rate: number;
  currencyCode: string | null; validFrom: string; validTo: string | null; isActive: boolean;
}
export interface SaveTravelSupplierCommissionRule {
  serviceType: TravelSupplierCommissionServiceType; basis: TravelSupplierCommissionBasis; rate: number; currencyCode: string | null; validFrom: string;
  validTo: string | null; isActive: boolean;
}

export interface TravelSupplierBillListItem extends TravelInvoiceMoney {
  id: number; billNumber: string | null; supplierId: number; supplierName: string; supplierReference: string | null; currencyCode: string;
  billDate: string | null; dueDate: string | null; gross: number; commissionTotal: number; status: TravelSupplierBillStatus;
  paymentStatus: TravelInvoicePaymentStatus | null; overdue: boolean; createdAt: string;
}
export interface TravelSupplierBillLine {
  id: number; billId: number; sequence: number; description: string; sourceType: TravelSupplierBillSourceType | null; sourceId: number | null;
  contextType: TravelContextType | null; contextId: number | null; contextLabel: string | null; amount: number; commissionAmount: number; netAmount: number;
}
export interface TravelSupplierAllocation {
  paymentId: number; paymentNumber: string; paidOn: string; method: TravelPaymentMethod; paymentStatus: TravelPaymentStatus; billId: number;
  billNumber: string | null; billCurrencyCode: string; amountFromPayment: number; paymentCurrencyCode: string; exchangeRate: number; amountApplied: number;
}
export interface TravelSupplierCredit {
  id: number; creditNumber: string; billId: number; billNumber: string | null; supplierId: number; supplierName: string; amount: number; currencyCode: string;
  reason: string; cancellationId: number | null; status: TravelCreditNoteStatus; issuedOn: string;
}
export interface TravelSupplierRefund {
  id: number; refundNumber: string; supplierId: number; supplierName: string; billId: number | null; billNumber: string | null; paymentId: number | null;
  paymentNumber: string | null; amount: number; currencyCode: string; method: TravelPaymentMethod; reference: string | null; receivedOn: string;
  cancellationId: number | null;
}
export interface TravelSupplierFinanceHistory {
  id: number; eventType: TravelSupplierFinanceEvent; fromValue: string | null; toValue: string | null; amount: number | null; currencyCode: string | null;
  note: string | null; changedByName: string | null; changedAt: string;
}
export interface TravelSupplierBill extends TravelSupplierBillListItem {
  notes: string | null; postedAt: string | null; voidReason: string | null; branchId: number | null; allowedActions: TravelSupplierBillAction[];
  lines: TravelSupplierBillLine[]; allocations: TravelSupplierAllocation[]; credits: TravelSupplierCredit[]; refunds: TravelSupplierRefund[];
  history: TravelSupplierFinanceHistory[];
}
export interface TravelBillableCost {
  sourceType: TravelSupplierBillSourceType; sourceId: number; supplierId: number; description: string; amount: number; currencyCode: string;
  bookingId: number; bookingLabel: string; serviceDate: string | null; units: number; cancelled: boolean; suggestedCommission: number; onBillId: number | null;
}
export interface TravelSupplierPaymentListItem {
  id: number; paymentNumber: string; supplierId: number; supplierName: string; paidOn: string; method: TravelPaymentMethod; reference: string | null;
  amount: number; currencyCode: string; status: TravelPaymentStatus; allocated: number; refunded: number; unallocated: number; reconciledAt: string | null;
  bankReference: string | null; createdAt: string;
}
export interface TravelSupplierPayment extends TravelSupplierPaymentListItem {
  notes: string | null; clearedAt: string | null; allowedActions: TravelPaymentAction[]; allocations: TravelSupplierAllocation[];
  refunds: TravelSupplierRefund[]; history: TravelSupplierFinanceHistory[];
}
export interface TravelPayablesCurrencySummary {
  currencyCode: string; outstanding: number; overdue: number; supplierCreditDue: number; paidThisMonth: number; unallocatedAdvances: number;
}
export interface TravelPayablesSummary { currencies: TravelPayablesCurrencySummary[]; billCounts: Record<TravelSupplierBillStatus, number>; billTotal: number; }
export interface TravelSupplierBalance {
  supplierId: number; supplierName: string; currencyCode: string; openBills: number; billed: number; paid: number; credited: number; balance: number;
  overdue: number; unallocatedAdvances: number;
}
export interface TravelSupplierStatement {
  supplierId: number; supplierName: string; currencyCode: string; from: string | null; to: string | null; openingBalance: number;
  entries: (Omit<TravelStatementEntry, 'entryType'> & { entryType: TravelSupplierStatementEntryType })[]; closingBalance: number; currencies: string[];
}
export interface SaveTravelSupplierBillLine {
  description: string; sourceType: TravelSupplierBillSourceType | null; sourceId: number | null; contextType: TravelContextType | null;
  contextId: number | null; amount: number; commissionAmount: number;
}
export interface SaveTravelSupplierBill {
  supplierReference: string | null; billDate: string | null; dueDate: string | null; notes: string | null; lines: SaveTravelSupplierBillLine[];
}
export interface RecordTravelSupplierPayment {
  idempotencyKey: string; supplierId: number; paidOn: string; method: TravelPaymentMethod; reference: string | null; amount: number;
  currencyCode: string; cleared: boolean; notes: string | null; allocations: TravelAllocationInput[];
}
export interface IssueTravelSupplierCredit {
  idempotencyKey: string; billId: number; amount: number; reason: string; cancellationId: number | null; issuedOn: string | null;
}
export interface RecordTravelSupplierRefund {
  idempotencyKey: string; billId: number | null; paymentId: number | null; amount: number; method: TravelPaymentMethod; reference: string | null;
  receivedOn: string; cancellationId: number | null; notes: string | null;
}

export interface TravelSubAgent { id: number; name: string; phone: string | null; email: string | null; notes: string | null; isActive: boolean; referredCustomers: number; }
export interface SaveTravelSubAgent { name: string; phone: string | null; email: string | null; notes: string | null; isActive: boolean; }
export interface TravelCommissionRule {
  id: number; beneficiaryType: TravelBeneficiaryType; userId: number | null; subAgentId: number | null; beneficiaryName: string | null;
  scope: TravelCommissionScope; basis: TravelCommissionBasis; rate: number; validFrom: string; validTo: string | null; isActive: boolean;
}
export interface SaveTravelCommissionRule {
  beneficiaryType: TravelBeneficiaryType; userId: number | null; subAgentId: number | null; scope: TravelCommissionScope; basis: TravelCommissionBasis;
  rate: number; validFrom: string; validTo: string | null; isActive: boolean;
}
export interface TravelCommissionLine {
  invoiceId: number; invoiceNumber: string | null; issueDate: string; customerId: number; customerName: string; beneficiaryType: TravelBeneficiaryType;
  beneficiaryId: number; beneficiaryName: string; currencyCode: string; basisAmount: number; basis: TravelCommissionBasis; rate: number; amount: number;
  status: TravelCommissionStatus; missingRate: boolean;
}
export interface TravelCommissionBalance {
  beneficiaryType: TravelBeneficiaryType; beneficiaryId: number; beneficiaryName: string; currencyCode: string; accrued: number; payable: number;
  paid: number; balance: number;
}
export interface TravelCommissionPayout {
  id: number; payoutNumber: string; beneficiaryType: TravelBeneficiaryType; beneficiaryId: number; beneficiaryName: string; paidOn: string;
  method: TravelPaymentMethod; reference: string | null; amount: number; currencyCode: string; notes: string | null;
}
export interface RecordTravelCommissionPayout {
  idempotencyKey: string; beneficiaryType: TravelBeneficiaryType; beneficiaryId: number; paidOn: string; method: TravelPaymentMethod;
  reference: string | null; amount: number; currencyCode: string; notes: string | null;
}

export interface TravelContextProfit {
  contextType: TravelContextType; contextId: number; label: string; travelDate: string | null; branchId: number | null; branchName: string | null;
  agentUserId: number | null; agentName: string | null; revenue: number; cost: number; estimatedCost: number; commission: number; margin: number;
  marginPercent: number | null; hasEstimates: boolean; missingRateCount: number;
}
export interface TravelProfitLine {
  kind: 'Revenue' | 'Cost' | 'Commission'; description: string; reference: string; date: string; amount: number; currencyCode: string;
  converted: number | null; estimated: boolean;
}
export interface TravelContextProfitDetail extends TravelContextProfit { reportingCurrencyCode: string; lines: TravelProfitLine[]; }
export interface TravelProfitGroup {
  key: string; label: string; contexts: number; revenue: number; cost: number; estimatedCost: number; commission: number; margin: number; marginPercent: number | null;
}
export interface TravelMissingRate { currencyCode: string; earliestDate: string; amounts: number; }
export interface TravelProfitabilityReport {
  reportingCurrencyCode: string; dimension: TravelProfitDimension; contexts: TravelContextProfit[]; groups: TravelProfitGroup[]; totals: TravelProfitGroup;
  missingRates: TravelMissingRate[];
}

// ── Phase 7: reminders, dashboard, reports ──────────────────────────────

export type TravelReminderRuleType =
  | 'PassportExpiry' | 'VisaAppointment' | 'MissingDocuments' | 'VisaDecisionOverdue' | 'VisaStatusChanged' | 'TicketingDeadline'
  | 'FlightDeparture' | 'HotelCheckIn' | 'InvoiceDue' | 'InvoiceOverdue' | 'EnquiryFollowUp' | 'GroupDeparture';
export const TRAVEL_REMINDER_RULE_TYPES: TravelReminderRuleType[] =
  ['PassportExpiry', 'VisaAppointment', 'MissingDocuments', 'VisaDecisionOverdue', 'VisaStatusChanged', 'TicketingDeadline',
   'FlightDeparture', 'HotelCheckIn', 'InvoiceDue', 'InvoiceOverdue', 'EnquiryFollowUp', 'GroupDeparture'];
export type TravelReminderKind = 'Upcoming' | 'Overdue' | 'Event';
export type TravelReminderEntityType = 'Traveler' | 'VisaCase' | 'FlightBooking' | 'HotelBooking' | 'Invoice' | 'Enquiry' | 'Group';
export type TravelReminderAudience = 'Staff' | 'Customer';
export const TRAVEL_REMINDER_AUDIENCES: TravelReminderAudience[] = ['Staff', 'Customer'];
export type TravelReminderStatus = 'Pending' | 'Sent' | 'Skipped' | 'Failed';
export const TRAVEL_REMINDER_STATUSES: TravelReminderStatus[] = ['Pending', 'Sent', 'Skipped', 'Failed'];
export type TravelReportGroup = 'Operational' | 'Finance' | 'Profit';

export const REMINDER_STATUS_VARIANT: Record<TravelReminderStatus, BadgeVariant> = { Pending: 'pending', Sent: 'success', Skipped: 'warning', Failed: 'error' };
export const REMINDER_AUDIENCE_LABEL: Record<TravelReminderAudience, string> = { Staff: 'Staff', Customer: 'Customer' };
export const REMINDER_KIND_LABEL: Record<TravelReminderKind, string> = { Upcoming: 'Before a date', Overdue: 'While overdue', Event: 'On change' };
/** Where a reminder's entity opens in the ERP. */
export const REMINDER_ENTITY_ROUTE: Record<TravelReminderEntityType, string> = {
  Traveler: '/travel/customers', VisaCase: '/travel/visa-cases', FlightBooking: '/travel/bookings', HotelBooking: '/travel/bookings',
  Invoice: '/travel/finance/invoices', Enquiry: '/travel/enquiries', Group: '/travel/groups'
};

export interface TravelReminderLead {
  leadDays: number; isEnabled: boolean; staffInApp: boolean; staffEmail: boolean; customerEmail: boolean; customerSms: boolean; customerWhatsApp: boolean;
}
export interface TravelReminderRule {
  ruleType: TravelReminderRuleType; name: string; description: string; entityType: TravelReminderEntityType; kind: TravelReminderKind;
  supportsStaff: boolean; supportsCustomer: boolean; isCustomised: boolean; leads: TravelReminderLead[]; customerTemplate: string | null; placeholders: string[];
}
export interface SaveTravelReminderRule { leads: TravelReminderLead[]; customerTemplate: string | null; }
export interface TravelReminderLogItem {
  id: number; ruleType: TravelReminderRuleType; leadDays: number; entityType: TravelReminderEntityType; entityId: number; contextLabel: string; dueAt: string;
  audience: TravelReminderAudience; recipientUserId: number | null; recipientUserName: string | null; recipientCustomerId: number | null;
  recipientCustomerName: string | null; channels: string; status: TravelReminderStatus; failureReason: string | null; notificationId: number | null;
  title: string; body: string; createdAt: string;
}
export interface TravelReminderRunResult { candidates: number; sent: number; skipped: number; failed: number; held: number; }

export interface TravelPipeline { counts: Record<string, number>; total: number; }
export interface TravelCurrencyAmount { currencyCode: string; amount: number; }
export interface TravelAttention {
  followUpsDue: number; missingDocumentCases: number; ticketingDueNext72Hours: number; passportsExpiring: number; passportWarningDays: number;
  appointmentsNext7Days: number; departuresNext7Days: number; overdueInvoices: number; overdueInvoiceAmounts: TravelCurrencyAmount[]; overdueSupplierBills: number;
  remindersFailedLast7Days: number; remindersSkippedNoAgentLast7Days: number;
}
export interface TravelTrendPoint { periodStart: string; label: string; total: number; won: number; }
export interface TravelMoneyTrendPoint { periodStart: string; label: string; currencyCode: string; issued: number; collected: number; }
export interface TravelDecisionTrendPoint { periodStart: string; label: string; approved: number; rejected: number; }
export interface TravelTrends {
  enquiries: TravelTrendPoint[]; invoicing: TravelMoneyTrendPoint[]; visaDecisions: TravelDecisionTrendPoint[]; enquiryWeeks: number; months: number;
}
export interface TravelDashboard {
  asOf: string; enquiries: TravelPipeline; visaCases: TravelPipeline; groups: TravelPipeline; bookings: TravelPipeline; attention: TravelAttention;
  receivables: TravelFinanceCurrencySummary[]; payables: TravelPayablesCurrencySummary[]; trends: TravelTrends;
}
export interface TravelDashboardProfit { reportingCurrencyCode: string; thisMonth: TravelProfitGroup; lastMonth: TravelProfitGroup; missingRates: TravelMissingRate[]; }
export interface TravelUpcomingItem {
  ruleType: TravelReminderRuleType; entityType: TravelReminderEntityType; entityId: number; label: string; reference: string; dueAt: string;
  staffUserId: number | null; customerName: string | null; amount: number | null; currencyCode: string | null;
}
export interface TravelUpcoming { days: number; items: TravelUpcomingItem[]; }

export interface TravelReportDefinition { type: string; title: string; group: TravelReportGroup; windowed: boolean; windowLabel: string; description: string; }
