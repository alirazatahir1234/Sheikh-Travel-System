import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input
} from '@angular/core';
import { PortalPayState } from '../../core/models/portal.models';
import {
  bookingStatusBadgeClass,
  bookingStatusLabel,
  payStateBadgeClass,
  payStateLabel
} from '../../core/utils/portal-display.util';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (payState() != null) {
      <span
        class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset"
        [class]="payClass()"
        >{{ payText() }}</span
      >
    } @else if (bookingStatus() != null) {
      <span
        class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset"
        [class]="bookClass()"
        >{{ bookText() }}</span
      >
    }
  `
})
export class StatusBadgeComponent {
  readonly payState = input<PortalPayState | null>(null);
  readonly bookingStatus = input<number | null>(null);

  readonly payText = computed(() => {
    const s = this.payState();
    return s != null ? payStateLabel(s) : '';
  });

  readonly payClass = computed(() => {
    const s = this.payState();
    return s != null ? payStateBadgeClass(s) : '';
  });

  readonly bookText = computed(() => {
    const s = this.bookingStatus();
    return s != null ? bookingStatusLabel(s) : '';
  });

  readonly bookClass = computed(() => {
    const s = this.bookingStatus();
    return s != null ? bookingStatusBadgeClass(s) : '';
  });
}
