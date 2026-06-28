export type UiButtonVariant =
  | 'primary'
  | 'secondary'
  | 'success'
  | 'danger'
  | 'outline'
  | 'ghost'
  | 'neutral';

export type UiButtonSize = 'sm' | 'md' | 'lg';

export type UiInputType = 'text' | 'email' | 'password' | 'number' | 'search' | 'tel';

export type UiStatusVariant =
  | 'success'
  | 'warning'
  | 'error'
  | 'info'
  | 'pending'
  | 'active'
  | 'inactive';

export type UiModalSize = 'sm' | 'md' | 'lg';

export type UiDrawerPosition = 'left' | 'right';

export type UiConfirmVariant = 'delete' | 'warning' | 'info';

export interface UiBreadcrumb {
  label: string;
  route?: string;
}

export interface UiSelectOption<T = string> {
  value: T;
  label: string;
  disabled?: boolean;
}

export interface UiTableColumn<T = Record<string, unknown>> {
  key: keyof T & string | string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'center' | 'right';
  width?: string;
}

export interface UiTableSort {
  key: string;
  direction: 'asc' | 'desc';
}

export interface UiTab {
  id: string;
  label: string;
  icon?: string;
  disabled?: boolean;
}

export interface UiConfirmConfig {
  variant?: UiConfirmVariant;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
}
