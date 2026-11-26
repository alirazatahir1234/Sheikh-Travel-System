import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../../environments/environment';
import {
  INSTALLMENT_STATUS_VARIANT, INVOICE_PAYMENT_STATUS_LABEL, INVOICE_PAYMENT_STATUS_VARIANT, INVOICE_STATUS_VARIANT, PAYMENT_METHOD_LABEL,
  PAYMENT_STATUS_LABEL, PAYMENT_STATUS_VARIANT, SOURCE_TYPE_LABEL, TRAVEL_INVOICE_PAYMENT_STATUSES, TRAVEL_INVOICE_STATUSES, TRAVEL_PAYMENT_METHODS,
  TRAVEL_PAYMENT_STATUSES, TravelBillableItem, TravelInvoice, TravelInvoiceListItem, TravelPayment
} from '../../models/travel.models';
import { TravelFinanceService } from '../../services/travel-finance.service';
import {
  buildCreditNotePdf, buildInvoicePdf, buildReceiptPdf, groupBillable, invoiceRows, money, receiptLines, suggestAllocations
} from './finance-documents';
import { splitEvenly } from './invoice-editor.component';

const open = (id: number, currency: string, balance: number, due: string, pending = 0): TravelInvoiceListItem => ({
  id, invoiceNumber: `TINV-2027-0000${id}`, customerId: 5, customerName: 'Khan family', currencyCode: currency, issueDate: '2027-01-01', dueDate: due,
  status: 'Issued', paymentStatus: 'Unpaid', nextDueDate: due, overdueAmount: 0, bookingId: null, bookingNumber: null, visaCaseId: null,
  visaCaseNumber: null, groupId: null, groupNumber: null, createdAt: '2027-01-01', total: balance, paid: 0, pendingPaid: pending, credited: 0,
  refunded: 0, balance
});

const invoice: TravelInvoice = {
  ...open(31, 'PKR', 60_000, '2027-02-15'), total: 160_000, paid: 100_000, balance: 60_000, paymentStatus: 'PartiallyPaid',
  subtotal: 168_000, discountAmount: 8_000, notes: 'Thank you', issuedAt: '2027-01-01', voidedAt: null, voidReason: null, branchId: null, branchName: null,
  allowedActions: ['RecordPayment'], bookingNumber: 'BKG-2027-00012',
  lines: [
    { id: 1, invoiceId: 31, sequence: 1, description: 'Flight · PNR ABC123 · LHE→JED', quantity: 1, unitPrice: 160_000, lineTotal: 160_000, sourceType: 'Flight',
      sourceId: 300, currentSourceAmount: 160_000, sourceChanged: false },
    { id: 2, invoiceId: 31, sequence: 2, description: 'Insurance', quantity: 2, unitPrice: 4_000, lineTotal: 8_000, sourceType: null, sourceId: null,
      currentSourceAmount: null, sourceChanged: false }
  ],
  installments: [{ invoiceId: 31, sequence: 1, dueDate: '2027-01-15', amount: 100_000, covered: 100_000, status: 'Paid' },
    { invoiceId: 31, sequence: 2, dueDate: '2027-02-15', amount: 60_000, covered: 0, status: 'Due' }],
  allocations: [], creditNotes: [], refunds: [], history: []
};

describe('TravelNexus finance', () => {
  it('maps every invoice, payment, installment and source value', () => {
    for (const s of TRAVEL_INVOICE_STATUSES) expect(INVOICE_STATUS_VARIANT[s]).toBeTruthy();
    for (const s of TRAVEL_INVOICE_PAYMENT_STATUSES) { expect(INVOICE_PAYMENT_STATUS_LABEL[s]).toBeTruthy(); expect(INVOICE_PAYMENT_STATUS_VARIANT[s]).toBeTruthy(); }
    for (const s of TRAVEL_PAYMENT_STATUSES) { expect(PAYMENT_STATUS_LABEL[s]).toBeTruthy(); expect(PAYMENT_STATUS_VARIANT[s]).toBeTruthy(); }
    for (const m of TRAVEL_PAYMENT_METHODS) expect(PAYMENT_METHOD_LABEL[m]).toBeTruthy();
    expect(Object.keys(INSTALLMENT_STATUS_VARIANT)).toEqual(['Paid', 'PartlyPaid', 'Due', 'Overdue']);
    expect(Object.keys(SOURCE_TYPE_LABEL)).toEqual(['Flight', 'Hotel', 'VisaServiceFee', 'VisaEmbassyFee', 'GroupMember']);
    expect(TRAVEL_PAYMENT_STATUSES).toEqual(['Pending', 'Completed', 'Failed', 'Refunded']);
  });

  it('formats money with the ISO code, never a symbol', () => {
    expect(money(1234.5, 'PKR')).toBe('1,234.50 PKR');
    expect(money(0, 'SAR')).toBe('0.00 SAR');
  });

  it('groups billable items by currency and by booking / visa case / group', () => {
    const item = (type: TravelBillableItem['sourceType'], id: number, currency: string, ctx: TravelBillableItem['contextType'], ctxId: number): TravelBillableItem => ({
      sourceType: type, sourceId: id, customerId: 5, customerName: 'Khan', description: `${type} ${id}`, amount: 100, currencyCode: currency,
      contextType: ctx, contextId: ctxId, contextLabel: `${ctx} ${ctxId}`, serviceDate: null, onInvoiceId: null
    });
    const grouped = groupBillable([item('Flight', 1, 'PKR', 'Booking', 70), item('Hotel', 2, 'SAR', 'Booking', 70), item('Hotel', 3, 'PKR', 'Booking', 70),
      item('VisaServiceFee', 9, 'PKR', 'VisaCase', 9)]);
    expect(grouped.map(g => g.currency)).toEqual(['PKR', 'SAR']);
    expect(grouped[0].contexts.map(c => c.label)).toEqual(['Booking 70', 'VisaCase 9']);
    expect(grouped[0].contexts[0].items.map(i => i.sourceId)).toEqual([1, 3]);
  });

  it('suggests allocations oldest due first, never above an open balance', () => {
    const invoices = [open(2, 'PKR', 60_000, '2027-02-15'), open(1, 'PKR', 100_000, '2027-01-15'), open(3, 'PKR', 50_000, '2027-03-01', 50_000)];
    expect(suggestAllocations(120_000, 'PKR', invoices)).toEqual([
      { invoiceId: 1, amountFromPayment: 100_000, exchangeRate: null },
      { invoiceId: 2, amountFromPayment: 20_000, exchangeRate: null }
    ]);
    // Invoice 3 is fully reserved by a pending payment.
    expect(suggestAllocations(500_000, 'PKR', invoices).map(a => a.invoiceId)).toEqual([1, 2]);
  });

  it('suggests cross-currency allocations only with a rate and never over-applies', () => {
    const sar = [open(4, 'SAR', 4_000, '2027-01-20')];
    expect(suggestAllocations(300_000, 'PKR', sar)).toEqual([]);
    const plan = suggestAllocations(400_000, 'PKR', sar, { SAR: 0.0133 });
    expect(plan.length).toBe(1);
    expect(plan[0].exchangeRate).toBe(0.0133);
    expect(Math.round(plan[0].amountFromPayment * 0.0133 * 100) / 100).toBeLessThanOrEqual(4_000);
    expect(plan[0].amountFromPayment).toBeLessThan(400_000);
  });

  it('splits installments evenly with the rounding on the last one', () => {
    const rows = splitEvenly(100_000, 3, '2027-01-31');
    expect(rows.map(r => r.amount)).toEqual([33_333.33, 33_333.33, 33_333.34]);
    expect(rows.reduce((n, r) => n + r.amount, 0)).toBeCloseTo(100_000, 2);
    expect(rows.map(r => r.dueDate)).toEqual(['2027-01-31', '2027-02-28', '2027-03-31']);
    expect(splitEvenly(0, 3, '2027-01-01')).toEqual([]);
  });

  it('builds invoice, receipt and credit note documents from the API data', () => {
    expect(invoiceRows(invoice)).toEqual([
      ['1', 'Flight · PNR ABC123 · LHE→JED', '1', '160,000.00 PKR', '160,000.00 PKR'],
      ['2', 'Insurance', '2', '4,000.00 PKR', '8,000.00 PKR']
    ]);
    expect(buildInvoicePdf(invoice).getNumberOfPages()).toBeGreaterThan(0);

    const payment: TravelPayment = {
      id: 41, receiptNumber: 'TRCP-2027-00001', customerId: 5, customerName: 'Khan family', receivedOn: '2027-01-10', method: 'Cheque', reference: 'CHQ 1001',
      amount: 100_000, currencyCode: 'PKR', status: 'Pending', allocated: 80_000, refunded: 0, unallocated: 20_000, reconciledAt: null, bankReference: null,
      createdAt: '2027-01-10', notes: null, clearedAt: null, createdByName: null, allowedActions: [], refunds: [], history: [],
      allocations: [{ paymentId: 41, receiptNumber: 'TRCP-2027-00001', receivedOn: '2027-01-10', method: 'Cheque', paymentStatus: 'Pending', invoiceId: 31,
        invoiceNumber: 'TINV-2027-00031', invoiceCurrencyCode: 'PKR', amountFromPayment: 80_000, paymentCurrencyCode: 'PKR', exchangeRate: 1, amountApplied: 80_000 }]
    };
    const lines = receiptLines(payment);
    expect(lines).toContain(['Status', 'Received, not yet cleared']);
    expect(lines).toContain(['On account', '20,000.00 PKR']);
    expect(buildReceiptPdf(payment).getNumberOfPages()).toBeGreaterThan(0);
    expect(buildCreditNotePdf({ id: 1, creditNoteNumber: 'TCN-2027-00001', invoiceId: 31, invoiceNumber: 'TINV-2027-00031', customerId: 5,
      customerName: 'Khan family', amount: 20_000, currencyCode: 'PKR', reason: 'Flight cancelled', cancellationId: 55, status: 'Issued', issuedOn: '2027-02-01',
      voidReason: null }).getNumberOfPages()).toBeGreaterThan(0);
  });

  describe('service', () => {
    let http: HttpTestingController;
    let api: TravelFinanceService;
    const base = `${environment.apiUrl}/travel/finance`;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
      api = TestBed.inject(TravelFinanceService);
    });
    afterEach(() => http.verify());

    it('sends invoice filters including date ranges and overdue, omitting empty ones', () => {
      api.getInvoices({ page: 1, pageSize: 20, search: ' ', status: '', paymentStatus: 'Unpaid', issueFrom: '2027-01-01', issueTo: '2027-01-31', overdue: true }).subscribe();
      const req = http.expectOne(r => r.url === `${base}/invoices`);
      expect(req.request.params.get('issueFrom')).toBe('2027-01-01');
      expect(req.request.params.get('issueTo')).toBe('2027-01-31');
      expect(req.request.params.get('overdue')).toBe('true');
      expect(req.request.params.get('paymentStatus')).toBe('Unpaid');
      expect(req.request.params.has('status')).toBeFalse();
      expect(req.request.params.has('search')).toBeFalse();
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    });

    it('sends payment date ranges and the reconciled filter', () => {
      api.getPayments({ page: 1, pageSize: 100, reconciled: false, receivedFrom: '2027-01-01', receivedTo: '2027-01-31' }).subscribe();
      const req = http.expectOne(r => r.url === `${base}/payments`);
      expect(req.request.params.get('reconciled')).toBe('false');
      expect(req.request.params.get('receivedFrom')).toBe('2027-01-01');
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    });

    it('posts receipts, credit notes and refunds with their request key', () => {
      const payment = { idempotencyKey: 'k-1', customerId: 5, receivedOn: '2027-01-10', method: 'Cash' as const, reference: null, amount: 300_000,
        currencyCode: 'PKR', cleared: true, notes: null, allocations: [{ invoiceId: 4, amountFromPayment: 300_000, exchangeRate: 0.0133 }] };
      api.recordPayment(payment).subscribe();
      expect(http.expectOne(`${base}/payments`).request.body).toEqual(payment);
      api.issueCreditNote({ idempotencyKey: 'k-2', invoiceId: 31, amount: 20_000, reason: 'Cancelled', cancellationId: 55, issuedOn: null }).subscribe();
      expect(http.expectOne(`${base}/credit-notes`).request.body.idempotencyKey).toBe('k-2');
      api.recordRefund({ idempotencyKey: 'k-3', invoiceId: 31, paymentId: null, amount: 20_000, method: 'BankTransfer', reference: null, paidOn: '2027-02-02',
        cancellationId: 55, notes: null }).subscribe();
      expect(http.expectOne(`${base}/refunds`).request.body.cancellationId).toBe(55);
      api.issueInvoice(31, null, null).subscribe();
      expect(http.expectOne(`${base}/invoices/31/issue`).request.method).toBe('POST');
      api.unallocate(41, 31).subscribe();
      expect(http.expectOne(`${base}/payments/41/allocations/31`).request.method).toBe('DELETE');
      api.getStatement(5, 'PKR', '2027-01-01', null).subscribe();
      const st = http.expectOne(r => r.url === `${base}/customers/5/statement`);
      expect(st.request.params.get('from')).toBe('2027-01-01');
      expect(st.request.params.has('to')).toBeFalse();
      st.flush({});
    });
  });
});
