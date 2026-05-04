/** Wizard payment plan (matches booking-wizard step 3). */
export type PaymentPlan = 'FULL' | 'PARTIAL' | 'PAY_LATER';

/** UI / workflow labels aligned with business rules (booking row may still be backend Pending until payment posts). */
export type UiPaymentStatusKey = 'PAID' | 'PARTIALLY_PAID' | 'UNPAID';

export function paidAmountForPlan(plan: PaymentPlan, total: number, partialRaw: unknown): number {
  const t = Number(total) || 0;
  if (plan === 'PAY_LATER') return 0;
  if (plan === 'FULL') return t;
  const p = Number(partialRaw);
  if (!Number.isFinite(p) || p <= 0) return 0;
  return Math.min(p, t);
}

export function calculateRemaining(total: number, paid: number): number {
  const r = (Number(total) || 0) - (Number(paid) || 0);
  return r < 0 ? 0 : r;
}

export function getUiPaymentStatus(plan: PaymentPlan, total: number, partialRaw: unknown): UiPaymentStatusKey {
  const t = Number(total) || 0;
  const paid = paidAmountForPlan(plan, t, partialRaw);
  if (plan === 'PAY_LATER') return 'UNPAID';
  if (plan === 'FULL') return paid >= t && t > 0 ? 'PAID' : 'UNPAID';
  if (plan === 'PARTIAL') {
    if (paid <= 0) return 'UNPAID';
    if (paid >= t) return 'PAID';
    return 'PARTIALLY_PAID';
  }
  return 'UNPAID';
}

export function paymentPlanLabel(plan: PaymentPlan): string {
  switch (plan) {
    case 'FULL':
      return 'Full payment';
    case 'PARTIAL':
      return 'Partial payment';
    default:
      return 'Pay later';
  }
}

/** Human-readable workflow line for the confirmation summary. */
export function bookingPaymentWorkflowLabel(plan: PaymentPlan, total: number, partialRaw: unknown): string {
  const key = getUiPaymentStatus(plan, total, partialRaw);
  switch (key) {
    case 'PAID':
      return 'Confirmed — full amount received';
    case 'PARTIALLY_PAID':
      return 'Partially confirmed — balance due before trip';
    default:
      return plan === 'PAY_LATER' ? 'Pending payment' : 'Awaiting payment';
  }
}

export interface ConfirmPaymentMessage {
  headline: string;
  detail: string;
  severity: 'success' | 'warning' | 'info';
}

export function getConfirmPaymentMessage(plan: PaymentPlan, total: number, partialRaw: unknown): ConfirmPaymentMessage {
  const t = Number(total) || 0;
  const paid = paidAmountForPlan(plan, t, partialRaw);
  const remaining = calculateRemaining(t, paid);
  const remStr = remaining.toLocaleString(undefined, { maximumFractionDigits: 0 });

  switch (plan) {
    case 'FULL':
      return {
        headline: 'Payment completed',
        detail: 'Your booking is fully confirmed. No dues remaining.',
        severity: 'success'
      };
    case 'PARTIAL':
      return {
        headline: 'Partial payment done',
        detail: `Remaining amount: PKR ${remStr}. Please pay before the trip date.`,
        severity: 'warning'
      };
    default:
      return {
        headline: 'Payment pending',
        detail: 'Please complete full payment before departure to avoid cancellation, per company policy.',
        severity: 'info'
      };
  }
}

export function uiPaymentStatusLabel(key: UiPaymentStatusKey): string {
  switch (key) {
    case 'PAID':
      return 'PAID';
    case 'PARTIALLY_PAID':
      return 'PARTIALLY PAID';
    default:
      return 'UNPAID';
  }
}
