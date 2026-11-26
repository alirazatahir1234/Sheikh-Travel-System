import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import {
  INVOICE_PAYMENT_STATUS_LABEL, PAYMENT_METHOD_LABEL, TravelBillableItem, TravelCreditNote, TravelInvoice, TravelInvoiceListItem, TravelPayment,
  TravelStatement
} from '../../models/travel.models';

/** Money as "1,234.50 PKR" — the ISO code, never a symbol. */
export function money(amount: number, currency: string): string {
  return `${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
}

const day = (iso: string | null | undefined): string => (iso ? iso.substring(0, 10) : '—');

/** Billable items grouped by currency, then by booking / visa case / group, for the invoice picker. */
export function groupBillable(items: TravelBillableItem[]): { currency: string; contexts: { label: string; type: string; items: TravelBillableItem[] }[] }[] {
  const byCurrency = new Map<string, Map<string, { label: string; type: string; items: TravelBillableItem[] }>>();
  for (const i of items) {
    const contexts = byCurrency.get(i.currencyCode) ?? new Map();
    const key = `${i.contextType}:${i.contextId}`;
    const ctx = contexts.get(key) ?? { label: i.contextLabel, type: i.contextType, items: [] };
    ctx.items.push(i);
    contexts.set(key, ctx);
    byCurrency.set(i.currencyCode, contexts);
  }
  return [...byCurrency.entries()].sort(([a], [b]) => a.localeCompare(b))
    .map(([currency, contexts]) => ({ currency, contexts: [...contexts.values()] }));
}

/**
 * Suggested allocation of a payment across open invoices: oldest due first, never above an invoice's open amount
 * (balance less pending reservations), only invoices in the payment currency unless a rate is given for theirs.
 */
export function suggestAllocations(amount: number, paymentCurrency: string, invoices: TravelInvoiceListItem[],
  rates: Record<string, number | null> = {}): { invoiceId: number; amountFromPayment: number; exchangeRate: number | null }[] {
  let left = round(amount);
  const result: { invoiceId: number; amountFromPayment: number; exchangeRate: number | null }[] = [];
  const ordered = [...invoices].sort((a, b) => (a.nextDueDate ?? a.dueDate ?? '').localeCompare(b.nextDueDate ?? b.dueDate ?? '') || a.id - b.id);
  for (const inv of ordered) {
    if (left <= 0) break;
    const open = round(Math.max(0, inv.balance - inv.pendingPaid));
    if (open <= 0) continue;
    if (inv.currencyCode === paymentCurrency) {
      const take = Math.min(open, left);
      result.push({ invoiceId: inv.id, amountFromPayment: take, exchangeRate: null });
      left = round(left - take);
      continue;
    }
    const rate = rates[inv.currencyCode];
    if (!rate || rate <= 0) continue;
    // Payment units needed to clear the open amount at this rate (rounded down so the applied amount never exceeds it).
    const needed = Math.floor((open / rate) * 100) / 100;
    const take = Math.min(needed, left);
    if (take <= 0) continue;
    result.push({ invoiceId: inv.id, amountFromPayment: take, exchangeRate: rate });
    left = round(left - take);
  }
  return result;
}

export const round = (n: number): number => Math.round((n + Number.EPSILON) * 100) / 100;

// ── PDFs ────────────────────────────────────────────────────────────────

function header(doc: jsPDF, title: string, lines: string[]): number {
  doc.setFontSize(16);
  doc.text(title, 40, 46);
  doc.setFontSize(9);
  lines.forEach((l, i) => doc.text(l, 40, 64 + i * 12));
  return 64 + lines.length * 12 + 10;
}

const lastY = (doc: jsPDF, fallback: number): number => ((doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY ?? fallback);

export function invoiceRows(inv: TravelInvoice): string[][] {
  return inv.lines.map(l => [String(l.sequence), l.description, String(l.quantity), money(l.unitPrice, inv.currencyCode), money(l.lineTotal, inv.currencyCode)]);
}

export function buildInvoicePdf(inv: TravelInvoice): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  const y = header(doc, inv.status === 'Draft' ? 'Invoice (draft)' : inv.status === 'Void' ? 'Invoice (VOID)' : 'Invoice', [
    `${inv.invoiceNumber ?? 'Draft'} · issued ${day(inv.issueDate)} · due ${day(inv.dueDate)}`,
    `Bill to: ${inv.customerName}`,
    ...[inv.bookingNumber ? `Booking ${inv.bookingNumber}` : null, inv.visaCaseNumber ? `Visa case ${inv.visaCaseNumber}` : null,
      inv.groupNumber ? `Umrah group ${inv.groupNumber}` : null].filter((x): x is string => !!x)
  ]);
  autoTable(doc, { startY: y, head: [['#', 'Description', 'Qty', 'Unit price', 'Amount']], body: invoiceRows(inv), styles: { fontSize: 9 },
    headStyles: { fillColor: [15, 118, 110] }, columnStyles: { 0: { cellWidth: 24 }, 2: { cellWidth: 36, halign: 'right' }, 3: { halign: 'right' }, 4: { halign: 'right' } } });
  const totals: string[][] = [['Subtotal', money(inv.subtotal, inv.currencyCode)]];
  if (inv.discountAmount > 0) totals.push(['Discount', `− ${money(inv.discountAmount, inv.currencyCode)}`]);
  totals.push(['Total', money(inv.total, inv.currencyCode)]);
  if (inv.status === 'Issued') {
    if (inv.paid - inv.refunded) totals.push(['Paid', `− ${money(inv.paid - inv.refunded, inv.currencyCode)}`]);
    if (inv.credited) totals.push(['Credited', `− ${money(inv.credited, inv.currencyCode)}`]);
    totals.push([inv.balance < 0 ? 'Credit due to you' : 'Balance due', money(Math.abs(inv.balance), inv.currencyCode)]);
  }
  autoTable(doc, { startY: lastY(doc, y) + 10, body: totals, theme: 'plain', styles: { fontSize: 10 }, margin: { left: 300 },
    columnStyles: { 0: { fontStyle: 'bold' }, 1: { halign: 'right' } } });
  if (inv.installments.length) {
    autoTable(doc, { startY: lastY(doc, y) + 14, head: [['Installment', 'Due', 'Amount', 'Status']],
      body: inv.installments.map(i => [String(i.sequence), day(i.dueDate), money(i.amount, inv.currencyCode), i.status]), styles: { fontSize: 9 } });
  }
  if (inv.notes) doc.text(doc.splitTextToSize(inv.notes, 515), 40, lastY(doc, y) + 24);
  return doc;
}

export function receiptLines(p: TravelPayment): [string, string][] {
  return [
    ['Receipt', p.receiptNumber],
    ['Received from', p.customerName],
    ['Date', day(p.receivedOn)],
    ['Amount', money(p.amount, p.currencyCode)],
    ['Method', PAYMENT_METHOD_LABEL[p.method] + (p.reference ? ` · ${p.reference}` : '')],
    ['Status', p.status === 'Pending' ? 'Received, not yet cleared' : p.status],
    ['Applied', money(p.allocated, p.currencyCode)],
    ['On account', money(p.unallocated, p.currencyCode)]
  ];
}

export function buildReceiptPdf(p: TravelPayment): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  const y = header(doc, 'Payment receipt', []);
  autoTable(doc, { startY: y, body: receiptLines(p), theme: 'plain', styles: { fontSize: 10 }, columnStyles: { 0: { fontStyle: 'bold', cellWidth: 130 } } });
  if (p.allocations.length) {
    autoTable(doc, { startY: lastY(doc, y) + 14, head: [['Invoice', 'From this payment', 'Rate', 'Applied']],
      body: p.allocations.map(a => [a.invoiceNumber ?? '—', money(a.amountFromPayment, a.paymentCurrencyCode),
        a.paymentCurrencyCode === a.invoiceCurrencyCode ? '—' : String(a.exchangeRate), money(a.amountApplied, a.invoiceCurrencyCode)]),
      styles: { fontSize: 9 } });
  }
  return doc;
}

export function buildCreditNotePdf(cn: TravelCreditNote): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  const y = header(doc, cn.status === 'Void' ? 'Credit note (VOID)' : 'Credit note', []);
  autoTable(doc, { startY: y, theme: 'plain', styles: { fontSize: 10 }, columnStyles: { 0: { fontStyle: 'bold', cellWidth: 130 } }, body: [
    ['Credit note', cn.creditNoteNumber], ['Date', day(cn.issuedOn)], ['Customer', cn.customerName], ['Against invoice', cn.invoiceNumber ?? '—'],
    ['Amount', money(cn.amount, cn.currencyCode)], ['Reason', cn.reason]
  ] });
  return doc;
}

export function buildStatementPdf(s: TravelStatement): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });
  const y = header(doc, 'Statement of account', [
    `${s.customerName} · ${s.currencyCode}`, `Period: ${s.from ? day(s.from) : 'start'} to ${s.to ? day(s.to) : 'today'}`,
    `Opening balance: ${money(s.openingBalance, s.currencyCode)}`
  ]);
  autoTable(doc, { startY: y, head: [['Date', 'Reference', 'Description', 'Debit', 'Credit', 'Balance']],
    body: s.entries.map(e => [day(e.date), e.reference, e.description, e.debit ? money(e.debit, s.currencyCode) : '', e.credit ? money(e.credit, s.currencyCode) : '',
      money(e.balance, s.currencyCode)]), styles: { fontSize: 8 }, headStyles: { fillColor: [15, 118, 110] } });
  doc.setFontSize(10);
  doc.text(`Closing balance: ${money(s.closingBalance, s.currencyCode)}${s.closingBalance < 0 ? ' (in your favour)' : ''}`, 40, lastY(doc, y) + 20);
  return doc;
}

export const paymentStatusText = (inv: TravelInvoiceListItem): string =>
  inv.status !== 'Issued' ? inv.status : inv.paymentStatus ? INVOICE_PAYMENT_STATUS_LABEL[inv.paymentStatus] : '';
