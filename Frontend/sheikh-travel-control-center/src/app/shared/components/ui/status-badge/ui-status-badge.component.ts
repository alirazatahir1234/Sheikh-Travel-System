import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgClass } from '@angular/common';
import { UiStatusVariant } from '../types/ui.types';

const VARIANT_CLASSES: Record<UiStatusVariant, string> = {
  success: 'text-emerald-700 bg-emerald-50',
  warning: 'text-amber-700 bg-amber-50',
  error: 'text-red-700 bg-red-50',
  info: 'text-blue-700 bg-blue-50',
  pending: 'text-amber-700 bg-amber-50',
  active: 'text-fleet-primary bg-fleet-primary-soft',
  inactive: 'text-slate-600 bg-slate-100'
};

/** Maps free-text status strings onto a known variant. */
const STATUS_ALIASES: Record<string, UiStatusVariant> = {
  available: 'success',
  valid: 'success',
  pass: 'success',
  completed: 'success',
  ontime: 'success',
  'in transit': 'active',
  intransit: 'active',
  assigned: 'warning',
  loading: 'warning',
  'on trip': 'warning',
  ontrip: 'warning',
  expiring: 'warning',
  scheduled: 'info',
  reserved: 'info',
  new: 'info',
  maintenance: 'error',
  fail: 'error',
  expired: 'error',
  suspended: 'error',
  retired: 'inactive',
  'off duty': 'inactive',
  offduty: 'inactive',
  active: 'success',
  idle: 'warning',
  offline: 'error',
  'on route': 'info',
  onroute: 'info',
  unknown: 'inactive'
};

@Component({
  selector: 'ui-status-badge',
  standalone: true,
  imports: [NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-bold uppercase tracking-wide"
      [ngClass]="variantClass()">
      <span class="h-1.5 w-1.5 rounded-full bg-current opacity-80"></span>
      {{ label() || status() }}
    </span>
  `
})
export class UiStatusBadgeComponent {
  readonly status = input('');
  readonly label = input<string>();
  /** Optional explicit variant; otherwise inferred from the status text. */
  readonly variant = input<UiStatusVariant>();

  protected readonly resolvedVariant = computed<UiStatusVariant>(() => {
    const explicit = this.variant();
    if (explicit) {
      return explicit;
    }
    const key = (this.status() || '').toLowerCase().trim();
    if (key in VARIANT_CLASSES) {
      return key as UiStatusVariant;
    }
    return STATUS_ALIASES[key] ?? 'inactive';
  });

  protected readonly variantClass = computed(() => VARIANT_CLASSES[this.resolvedVariant()]);
}
