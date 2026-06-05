import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  LedgerPaymentUiStatus,
  calculateLedgerRemaining,
  getLedgerPaymentUiStatus
} from '../../utils/booking-ledger.util';

@Component({
  selector: 'stb-booking-ledger-payment-summary',
  standalone: true,
  imports: [CommonModule, RouterModule, MatButtonModule, MatIconModule],
  templateUrl: './booking-ledger-payment-summary.component.html',
  styleUrls: ['./booking-ledger-payment-summary.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BookingLedgerPaymentSummaryComponent {
  @Input({ required: true }) bookingId!: number;
  @Input({ required: true }) totalAmount!: number;
  @Input({ required: true }) totalPaid!: number;

  get balanceDue(): number {
    return calculateLedgerRemaining(this.totalAmount, this.totalPaid);
  }

  get ledgerStatus(): LedgerPaymentUiStatus {
    return getLedgerPaymentUiStatus(this.totalAmount, this.totalPaid);
  }

  badgeClass(): string {
    switch (this.ledgerStatus) {
      case 'PAID':
        return 'ledger-badge-paid';
      case 'PARTIALLY_PAID':
        return 'ledger-badge-partial';
      default:
        return 'ledger-badge-unpaid';
    }
  }

  statusLabel(): string {
    switch (this.ledgerStatus) {
      case 'PAID':
        return 'PAID';
      case 'PARTIALLY_PAID':
        return 'PARTIALLY PAID';
      default:
        return 'UNPAID';
    }
  }

  payRemainingQuery(): Record<string, string | number> {
    const amt = Math.max(1, Math.round(this.balanceDue));
    return { bookingId: this.bookingId, amount: amt };
  }
}
