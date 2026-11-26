import {
  SUPPLIER_BILL_PAYMENT_LABEL, TravelBillableCost, TravelInvoiceListItem, TravelProfitGroup, TravelSupplierBillListItem
} from '../../models/travel.models';
import { round, suggestAllocations } from '../finance/finance-documents';

/** A bill line being edited: amount and commission in the bill currency. */
export interface BillLineDraft { amount: number | null; commissionAmount: number | null; }

/** Gross, supplier commission and net payable of the lines (commission is never above a line's amount). */
export function billTotals(lines: BillLineDraft[]): { gross: number; commission: number; net: number; invalid: boolean } {
  let gross = 0, commission = 0, invalid = false;
  for (const l of lines) {
    const amount = round(Number(l.amount) || 0);
    const c = round(Number(l.commissionAmount) || 0);
    if (amount < 0 || c < 0 || c > amount) invalid = true;
    gross += amount;
    commission += c;
  }
  gross = round(gross);
  commission = round(commission);
  return { gross, commission, net: round(gross - commission), invalid };
}

/** Billable services grouped by booking file, ordered by service date. */
export function groupBillableCosts(items: TravelBillableCost[]): { bookingId: number; label: string; items: TravelBillableCost[] }[] {
  const map = new Map<number, { bookingId: number; label: string; items: TravelBillableCost[] }>();
  for (const i of [...items].sort((a, b) => (a.serviceDate ?? '').localeCompare(b.serviceDate ?? '') || a.sourceId - b.sourceId)) {
    const g = map.get(i.bookingId) ?? { bookingId: i.bookingId, label: i.bookingLabel, items: [] };
    g.items.push(i);
    map.set(i.bookingId, g);
  }
  return [...map.values()];
}

/** Oldest-due-first split of a supplier payment over open bills (same rules as customer receipts). */
export function suggestBillAllocations(amount: number, paymentCurrency: string, bills: TravelSupplierBillListItem[],
  rates: Record<string, number | null> = {}): { invoiceId: number; amountFromPayment: number; exchangeRate: number | null }[] {
  // suggestAllocations reads id, currency, balance, pending and the due date only.
  const asInvoices = bills.map(b => ({ id: b.id, currencyCode: b.currencyCode, balance: b.balance, pendingPaid: b.pendingPaid, dueDate: b.dueDate,
    nextDueDate: null }) as unknown as TravelInvoiceListItem);
  return suggestAllocations(amount, paymentCurrency, asInvoices, rates);
}

export const billPaymentText = (b: TravelSupplierBillListItem): string =>
  b.status !== 'Posted' ? b.status : b.paymentStatus ? SUPPLIER_BILL_PAYMENT_LABEL[b.paymentStatus] : '';

/** "12.5%" or "—" when there is no revenue. */
export const marginPercentText = (p: number | null): string => (p === null || p === undefined ? '—' : `${p.toFixed(1)}%`);

/** Tailwind colour for a margin: red when negative, amber when thin (< 10%), green otherwise. */
export function marginTone(margin: number, percent: number | null): 'text-red-600' | 'text-amber-600' | 'text-emerald-700' {
  if (margin < 0) return 'text-red-600';
  if (percent !== null && percent < 10) return 'text-amber-600';
  return 'text-emerald-700';
}

/** Report totals must equal the sum of the rows they summarise. */
export function sumGroups(rows: { revenue: number; cost: number; commission: number }[]): Pick<TravelProfitGroup, 'revenue' | 'cost' | 'commission' | 'margin'> {
  const revenue = round(rows.reduce((n, r) => n + r.revenue, 0));
  const cost = round(rows.reduce((n, r) => n + r.cost, 0));
  const commission = round(rows.reduce((n, r) => n + r.commission, 0));
  return { revenue, cost, commission, margin: round(revenue - cost - commission) };
}
