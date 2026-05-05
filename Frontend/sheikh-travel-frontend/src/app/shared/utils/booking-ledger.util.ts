import { BookingStatus } from '../../core/models/booking.model';

/** Derived from booking total vs sum of paid/partially-paid payments (ledger view). */
export type LedgerPaymentUiStatus = 'PAID' | 'PARTIALLY_PAID' | 'UNPAID';

export function calculateLedgerRemaining(totalAmount: number, totalPaid: number): number {
  const r = (Number(totalAmount) || 0) - (Number(totalPaid) || 0);
  return Math.round(r * 100) / 100;
}

export function getLedgerPaymentUiStatus(totalAmount: number, totalPaid: number): LedgerPaymentUiStatus {
  const remaining = calculateLedgerRemaining(totalAmount, totalPaid);
  if (remaining <= 0.009) return 'PAID';
  if ((Number(totalPaid) || 0) > 0.009) return 'PARTIALLY_PAID';
  return 'UNPAID';
}

export type BannerVariant = 'success' | 'warning' | 'danger' | 'info';

export interface BookingDetailBannerVm {
  variant: BannerVariant;
  icon: string;
  title: string;
  subtitle: string;
}

export function getBookingDetailBanner(
  status: BookingStatus,
  totalPaid: number,
  totalAmount: number
): BookingDetailBannerVm {
  const balanceDue = calculateLedgerRemaining(totalAmount, totalPaid);

  if (status === 'Cancelled') {
    return {
      variant: 'danger',
      icon: 'cancel',
      title: 'Booking cancelled',
      subtitle: 'This booking is no longer active. Payment and edit actions are disabled.'
    };
  }

  if (status === 'Completed') {
    return {
      variant: 'success',
      icon: 'flag',
      title: 'Trip completed',
      subtitle: 'This booking is closed. You can still view history and print the invoice.'
    };
  }

  if (balanceDue > 0.009) {
    return {
      variant: 'warning',
      icon: 'payments',
      title: 'Payment pending',
      subtitle: 'Please settle the remaining balance before departure to keep this booking secure.'
    };
  }

  if (status === 'Started') {
    return {
      variant: 'success',
      icon: 'directions_car',
      title: 'Trip in progress',
      subtitle: 'This booking is active. Ledger shows no remaining balance.'
    };
  }

  if (status === 'Confirmed') {
    return {
      variant: 'success',
      icon: 'verified',
      title: 'Booking confirmed',
      subtitle: 'Your trip is scheduled and confirmed. No balance due on the ledger.'
    };
  }

  return {
    variant: 'info',
    icon: 'schedule',
    title: 'Booking pending',
    subtitle: 'Confirm details and collect payment as needed before the trip.'
  };
}
