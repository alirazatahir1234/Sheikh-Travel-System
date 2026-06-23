export type DriverWizardStepId = 'personal' | 'license' | 'organization';

export interface DriverWizardStep {
  id: DriverWizardStepId;
  number: number;
  label: string;
}

export const DRIVER_WIZARD_STEPS: DriverWizardStep[] = [
  { id: 'personal', number: 1, label: 'Personal Information' },
  { id: 'license', number: 2, label: 'License & Documents' },
  { id: 'organization', number: 3, label: 'Org Assignment' }
];

export const DRIVER_NATIONALITY_OPTIONS = [
  'United Arab Emirates',
  'Pakistan',
  'India',
  'Bangladesh',
  'Philippines',
  'Egypt',
  'Jordan',
  'Lebanon',
  'Syria',
  'Sudan',
  'Nepal',
  'Sri Lanka',
  'United Kingdom',
  'United States',
  'Other'
] as const;

export const DRIVER_GENDER_OPTIONS = ['Male', 'Female', 'Other'] as const;

export type DriverDocType = 'DrivingLicense' | 'MedicalCertificate' | 'BackgroundCheck';

export interface DriverDocSlot {
  type: DriverDocType;
  label: string;
  file: File | null;
  previewUrl: string | null;
}

export const DRIVER_DOC_SLOTS: Omit<DriverDocSlot, 'file' | 'previewUrl'>[] = [
  { type: 'DrivingLicense', label: 'Driving License' },
  { type: 'MedicalCertificate', label: 'Medical Certificate' },
  { type: 'BackgroundCheck', label: 'Background Check' }
];

export const DRIVER_WIZARD_DRAFT_KEY = 'driver-wizard-draft';

export interface DriverWizardDraft {
  form: Record<string, unknown>;
  currentStep: DriverWizardStepId;
  savedAt: string;
}
