export interface SettingsCategory {
  id: string;
  label: string;
  icon: string;
  description: string;
  isImplemented: boolean;
}

export type SettingFieldType =
  | 'text'
  | 'email'
  | 'url'
  | 'password'
  | 'textarea'
  | 'number'
  | 'dropdown'
  | 'toggle'
  | 'color'
  | 'readonly';

export interface SettingFieldOption {
  value: string;
  label: string;
}

export interface SettingFieldSchema {
  key: string;
  label: string;
  type: SettingFieldType;
  /** Groups fields into titled cards in the form area. */
  section?: string;
  options?: SettingFieldOption[];
  placeholder?: string;
  hint?: string;
  required?: boolean;
  min?: number;
  max?: number;
}

/** Raw values exchanged with the API: every value is serialized as a string (or null). */
export type SettingsValues = Record<string, string | null>;
