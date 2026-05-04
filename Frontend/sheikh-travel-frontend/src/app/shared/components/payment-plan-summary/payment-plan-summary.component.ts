import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ConfirmPaymentMessage,
  PaymentPlan,
  UiPaymentStatusKey,
  bookingPaymentWorkflowLabel,
  calculateRemaining,
  getConfirmPaymentMessage,
  getUiPaymentStatus,
  paidAmountForPlan,
  paymentPlanLabel,
  uiPaymentStatusLabel
} from '../../utils/booking-payment-plan.util';

export type PaymentPlanSummaryVariant = 'compact' | 'detailed';

@Component({
  selector: 'stb-payment-plan-summary',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatTooltipModule],
  templateUrl: './payment-plan-summary.component.html',
  styleUrls: ['./payment-plan-summary.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentPlanSummaryComponent {
  @Input({ required: true }) paymentPlan!: PaymentPlan;
  @Input({ required: true }) totalAmount!: number;
  /** Raw partial amount from the form (only used when plan is PARTIAL). */
  @Input() partialAmount: number | null = null;
  @Input() variant: PaymentPlanSummaryVariant = 'compact';

  get total(): number {
    return Number(this.totalAmount) || 0;
  }

  get paidNow(): number {
    return paidAmountForPlan(this.paymentPlan, this.total, this.partialAmount);
  }

  get remaining(): number {
    return calculateRemaining(this.total, this.paidNow);
  }

  get statusKey(): UiPaymentStatusKey {
    return getUiPaymentStatus(this.paymentPlan, this.total, this.partialAmount);
  }

  get statusLabel(): string {
    return uiPaymentStatusLabel(this.statusKey);
  }

  get planLabel(): string {
    return paymentPlanLabel(this.paymentPlan);
  }

  get workflowLabel(): string {
    return bookingPaymentWorkflowLabel(this.paymentPlan, this.total, this.partialAmount);
  }

  get message(): ConfirmPaymentMessage {
    return getConfirmPaymentMessage(this.paymentPlan, this.total, this.partialAmount);
  }

  badgeClass(): string {
    switch (this.statusKey) {
      case 'PAID':
        return 'badge-paid';
      case 'PARTIALLY_PAID':
        return 'badge-partial';
      default:
        return 'badge-unpaid';
    }
  }

  showRemainingAlert(): boolean {
    return this.remaining > 0;
  }
}
