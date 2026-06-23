import {
  Component,
  ChangeDetectionStrategy,
  input,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { BranchStatus } from '../../models/organization.models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="badgeClasses()">
      <span [class]="dotClasses()"></span>
      {{ status() }}
    </span>
  `,
})
export class StatusBadgeComponent {
  readonly status = input.required<BranchStatus>();

  readonly badgeClasses = computed(() => {
    const base = 'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium';
    switch (this.status()) {
      case 'Active':
        return `${base} bg-emerald-50 text-emerald-700`;
      case 'Inactive':
        return `${base} bg-gray-100 text-gray-600`;
      case 'Maintenance':
        return `${base} bg-amber-50 text-amber-700`;
      case 'Closed':
        return `${base} bg-red-50 text-red-700`;
      default:
        return `${base} bg-gray-100 text-gray-600`;
    }
  });

  readonly dotClasses = computed(() => {
    const base = 'w-1.5 h-1.5 rounded-full';
    switch (this.status()) {
      case 'Active':
        return `${base} bg-emerald-500`;
      case 'Inactive':
        return `${base} bg-gray-400`;
      case 'Maintenance':
        return `${base} bg-amber-500`;
      case 'Closed':
        return `${base} bg-red-500`;
      default:
        return `${base} bg-gray-400`;
    }
  });
}
