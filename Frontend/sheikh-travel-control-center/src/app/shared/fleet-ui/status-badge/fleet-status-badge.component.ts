import { Component, Input } from '@angular/core';
import { UiStatusVariant } from '../../components/ui/types/ui.types';

export type FleetStatusTone = 'success' | 'warning' | 'danger' | 'primary' | 'muted';

const TONE_TO_VARIANT: Record<FleetStatusTone, UiStatusVariant> = {
  success: 'success',
  warning: 'warning',
  danger: 'error',
  primary: 'active',
  muted: 'inactive'
};

const TONE_MAP: Record<string, FleetStatusTone> = {
  // Vehicle
  new: 'primary',
  available: 'success',
  assigned: 'warning',
  ontrip: 'warning',
  'on trip': 'warning',
  maintenance: 'danger',
  reserved: 'primary',
  retired: 'muted',
  // Driver
  offduty: 'muted',
  'off duty': 'muted',
  suspended: 'danger',
  // Maintenance
  scheduled: 'primary',
  inprogress: 'warning',
  'in progress': 'warning',
  completed: 'success',
  // Compliance / Inspection
  valid: 'success',
  expiring: 'warning',
  expired: 'danger',
  pass: 'success',
  fail: 'danger',
  pending: 'warning'
};

@Component({
  selector: 'fleet-status-badge',
  template: `<ui-status-badge [status]="status" [label]="label" [variant]="variant"></ui-status-badge>`
})
export class FleetStatusBadgeComponent {
  @Input() status = '';
  @Input() label?: string;
  /** Optional explicit tone override; otherwise inferred from the status text. */
  @Input() toneOverride?: FleetStatusTone;

  get tone(): FleetStatusTone {
    if (this.toneOverride) return this.toneOverride;
    return TONE_MAP[(this.status || '').toLowerCase().trim()] ?? 'muted';
  }

  get variant(): UiStatusVariant {
    return TONE_TO_VARIANT[this.tone];
  }
}
