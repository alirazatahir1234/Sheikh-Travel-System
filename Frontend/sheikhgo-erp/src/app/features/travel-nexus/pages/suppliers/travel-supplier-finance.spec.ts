import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../../environments/environment';
import {
  BENEFICIARY_TYPE_LABEL, COMMISSION_BASIS_LABEL, COMMISSION_STATUS_VARIANT, CONTEXT_TYPE_LABEL, PROFIT_DIMENSION_LABEL, SUPPLIER_BILL_PAYMENT_LABEL,
  SUPPLIER_BILL_STATUS_VARIANT, SUPPLIER_COMMISSION_BASIS_LABEL, SUPPLIER_FINANCE_EVENT_LABEL, SUPPLIER_RATE_TYPE_LABEL, TRAVEL_BENEFICIARY_TYPES,
  TRAVEL_COMMISSION_BASES, TRAVEL_CONTEXT_TYPES, TRAVEL_INVOICE_PAYMENT_STATUSES, TRAVEL_PROFIT_DIMENSIONS, TRAVEL_SUPPLIER_BILL_STATUSES,
  TRAVEL_SUPPLIER_COMMISSION_BASES, TRAVEL_SUPPLIER_RATE_TYPES, TravelBillableCost, TravelSupplierBillListItem
} from '../../models/travel.models';
import { TravelCommissionService } from '../../services/travel-commission.service';
import { TravelProfitabilityService } from '../../services/travel-profitability.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { billPaymentText, billTotals, groupBillableCosts, marginPercentText, marginTone, suggestBillAllocations, sumGroups } from './supplier-finance';

const bill = (id: number, currency: string, balance: number, due: string, pending = 0): TravelSupplierBillListItem => ({
  id, billNumber: `TSB-2027-0000${id}`, supplierId: 4, supplierName: 'Al Haramain', supplierReference: `INV-${id}`, currencyCode: currency,
  billDate: '2027-03-01', dueDate: due, gross: balance, commissionTotal: 0, status: 'Posted', paymentStatus: 'Unpaid', overdue: false,
  createdAt: '2027-03-01', total: balance, paid: 0, pendingPaid: pending, credited: 0, refunded: 0, balance
});

const cost = (sourceId: number, bookingId: number, date: string | null): TravelBillableCost => ({
  sourceType: 'Hotel', sourceId, supplierId: 4, description: `Hotel ${sourceId}`, amount: 8000, currencyCode: 'SAR', bookingId,
  bookingLabel: `BKG-${bookingId}`, serviceDate: date, units: 20, cancelled: false, suggestedCommission: 400, onBillId: null
});

describe('TravelNexus supplier finance, commissions and profitability', () => {
  it('labels every supplier-finance, commission and profitability value', () => {
    for (const s of TRAVEL_SUPPLIER_BILL_STATUSES) expect(SUPPLIER_BILL_STATUS_VARIANT[s]).toBeTruthy();
    for (const s of TRAVEL_INVOICE_PAYMENT_STATUSES) expect(SUPPLIER_BILL_PAYMENT_LABEL[s]).toBeTruthy();
    for (const t of TRAVEL_SUPPLIER_RATE_TYPES) expect(SUPPLIER_RATE_TYPE_LABEL[t]).toBeTruthy();
    for (const b of TRAVEL_SUPPLIER_COMMISSION_BASES) expect(SUPPLIER_COMMISSION_BASIS_LABEL[b]).toBeTruthy();
    for (const c of TRAVEL_CONTEXT_TYPES) expect(CONTEXT_TYPE_LABEL[c]).toBeTruthy();
    for (const t of TRAVEL_BENEFICIARY_TYPES) expect(BENEFICIARY_TYPE_LABEL[t]).toBeTruthy();
    for (const b of TRAVEL_COMMISSION_BASES) expect(COMMISSION_BASIS_LABEL[b]).toBeTruthy();
    for (const d of TRAVEL_PROFIT_DIMENSIONS) expect(PROFIT_DIMENSION_LABEL[d]).toBeTruthy();
    expect(Object.keys(COMMISSION_STATUS_VARIANT)).toEqual(['Accrued', 'Payable']);
    expect(Object.keys(SUPPLIER_FINANCE_EVENT_LABEL).length).toBe(14);
    expect(SUPPLIER_BILL_PAYMENT_LABEL.CreditDue).toBe('Supplier owes us');
  });

  it('totals a bill: gross − supplier commission = net, and flags commission above the amount', () => {
    expect(billTotals([{ amount: 8000, commissionAmount: 400 }, { amount: 800, commissionAmount: null }])).toEqual({ gross: 8800, commission: 400, net: 8400, invalid: false });
    expect(billTotals([{ amount: 100, commissionAmount: 150 }]).invalid).toBeTrue();
    expect(billTotals([{ amount: 100, commissionAmount: -1 }]).invalid).toBeTrue();
    expect(billTotals([]).net).toBe(0);
  });

  it('groups billable services by booking file in service-date order', () => {
    const groups = groupBillableCosts([cost(3, 71, '2027-04-10'), cost(1, 70, '2027-04-01'), cost(2, 71, '2027-04-05')]);
    expect(groups.map(g => g.bookingId)).toEqual([70, 71]);
    expect(groups[1].items.map(i => i.sourceId)).toEqual([2, 3]);
  });

  it('suggests a supplier payment split oldest due first, never above the open amount', () => {
    const plan = suggestBillAllocations(5000, 'SAR', [bill(2, 'SAR', 4000, '2027-04-01'), bill(1, 'SAR', 3600, '2027-03-15', 600)]);
    expect(plan).toEqual([{ invoiceId: 1, amountFromPayment: 3000, exchangeRate: null }, { invoiceId: 2, amountFromPayment: 2000, exchangeRate: null }]);
    expect(suggestBillAllocations(100, 'SAR', [bill(3, 'USD', 50, '2027-03-01')])).toEqual([]);
    expect(suggestBillAllocations(100, 'SAR', [bill(3, 'USD', 50, '2027-03-01')], { USD: 0.25 })[0].amountFromPayment).toBe(100);
  });

  it('describes bill payment status and margins', () => {
    expect(billPaymentText(bill(1, 'SAR', 10, '2027-03-01'))).toBe('Unpaid');
    expect(billPaymentText({ ...bill(1, 'SAR', 10, '2027-03-01'), status: 'Draft', paymentStatus: null })).toBe('Draft');
    expect(marginPercentText(null)).toBe('—');
    expect(marginPercentText(12.34)).toBe('12.3%');
    expect(marginTone(-1, -5)).toBe('text-red-600');
    expect(marginTone(50, 4)).toBe('text-amber-600');
    expect(marginTone(500, 25)).toBe('text-emerald-700');
  });

  it('report totals equal the sum of their rows', () => {
    const rows = [{ revenue: 1000, cost: 600, commission: 50 }, { revenue: 500.55, cost: 550, commission: 0 }];
    const t = sumGroups(rows);
    expect(t.revenue).toBe(1500.55);
    expect(t.margin).toBe(round2(rows.reduce((n, r) => n + r.revenue - r.cost - r.commission, 0)));
  });

  describe('services', () => {
    let http: HttpTestingController;
    const payables = `${environment.apiUrl}/travel/payables`;
    const commissions = `${environment.apiUrl}/travel/commissions`;
    const profitability = `${environment.apiUrl}/travel/profitability`;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
    });
    afterEach(() => http.verify());

    it('sends bill filters, dropping empty ones', () => {
      TestBed.inject(TravelSupplierFinanceService).getBills({ page: 2, pageSize: 20, search: '  INV ', status: '', supplierId: 4, billFrom: '2027-03-01', billTo: '', overdue: false })
        .subscribe();
      const req = http.expectOne(r => r.url === `${payables}/bills`);
      expect(req.request.params.get('page')).toBe('2');
      expect(req.request.params.get('search')).toBe('INV');
      expect(req.request.params.get('supplierId')).toBe('4');
      expect(req.request.params.get('billFrom')).toBe('2027-03-01');
      expect(req.request.params.has('billTo')).toBeFalse();
      expect(req.request.params.has('status')).toBeFalse();
      expect(req.request.params.has('overdue')).toBeFalse();
      req.flush({ items: [], totalCount: 0, page: 2, pageSize: 20 });
    });

    it('records supplier payments and credits with the dialog request key', () => {
      const api = TestBed.inject(TravelSupplierFinanceService);
      const payment = { idempotencyKey: 'k-1', supplierId: 4, paidOn: '2027-03-10', method: 'BankTransfer' as const, reference: null, amount: 4000,
        currencyCode: 'SAR', cleared: false, notes: null, allocations: [{ invoiceId: 61, amountFromPayment: 4000, exchangeRate: null }] };
      api.recordPayment(payment).subscribe();
      expect(http.expectOne(`${payables}/payments`).request.body).toEqual(payment);
      api.issueCredit({ idempotencyKey: 'k-2', billId: 61, amount: 100, reason: 'Downgrade', cancellationId: null, issuedOn: null }).subscribe();
      expect(http.expectOne(`${payables}/credits`).request.body.idempotencyKey).toBe('k-2');
      api.unallocate(71, 61).subscribe();
      expect(http.expectOne(`${payables}/payments/71/allocations/61`).request.method).toBe('DELETE');
    });

    it('asks for billable services with the bill date for commission suggestions', () => {
      TestBed.inject(TravelSupplierFinanceService).getBillable(4, null, null, '2027-03-10').subscribe();
      const req = http.expectOne(r => r.url === `${payables}/billable`);
      expect(req.request.params.get('billDate')).toBe('2027-03-10');
      expect(req.request.params.has('currencyCode')).toBeFalse();
      req.flush([]);
    });

    it('quotes a hotel cost from rate cards', () => {
      TestBed.inject(TravelSupplierFinanceService).quote({ supplierId: 4, hotelId: 12, checkIn: '2027-03-01', checkOut: '2027-03-11',
        rooms: [{ roomType: 'Double', quantity: 2 }] }).subscribe();
      expect(http.expectOne(`${payables}/rates/quote`).request.method).toBe('POST');
    });

    it('creates and updates exchange rates and rules on the right verbs', () => {
      const api = TestBed.inject(TravelSupplierFinanceService);
      api.saveExchangeRate(null, { currencyCode: 'SAR', rateToReporting: 74.2, effectiveFrom: '2027-03-01' }).subscribe();
      expect(http.expectOne(`${payables}/exchange-rates`).request.method).toBe('POST');
      api.saveExchangeRate(5, { currencyCode: 'SAR', rateToReporting: 74.5, effectiveFrom: '2027-03-01' }).subscribe();
      expect(http.expectOne(`${payables}/exchange-rates/5`).request.method).toBe('PUT');
      TestBed.inject(TravelCommissionService).saveRule(null, { beneficiaryType: 'Staff', userId: null, subAgentId: null, scope: 'All', basis: 'PercentOfMargin',
        rate: 10, validFrom: '2027-01-01', validTo: null, isActive: true }).subscribe();
      expect(http.expectOne(`${commissions}/rules`).request.method).toBe('POST');
    });

    it('filters commission lines and records payouts', () => {
      const api = TestBed.inject(TravelCommissionService);
      api.getLines({ beneficiaryType: '', status: 'Payable', issuedFrom: '2027-01-01', issuedTo: '' }).subscribe();
      const req = http.expectOne(r => r.url === `${commissions}/lines`);
      expect(req.request.params.get('status')).toBe('Payable');
      expect(req.request.params.has('beneficiaryType')).toBeFalse();
      expect(req.request.params.has('issuedTo')).toBeFalse();
      req.flush([]);
      api.updateAttribution(31, 7, null).subscribe();
      expect(http.expectOne(`${commissions}/invoices/31/attribution`).request.body).toEqual({ agentUserId: 7, subAgentId: null });
    });

    it('always sends both travel dates to profitability', () => {
      TestBed.inject(TravelProfitabilityService).getReport('Group', '2027-01-01', '2027-06-30', null, 7).subscribe();
      const req = http.expectOne(r => r.url === profitability);
      expect(req.request.params.get('dimension')).toBe('Group');
      expect(req.request.params.get('travelFrom')).toBe('2027-01-01');
      expect(req.request.params.get('travelTo')).toBe('2027-06-30');
      expect(req.request.params.get('agentUserId')).toBe('7');
      expect(req.request.params.has('branchId')).toBeFalse();
      req.flush(null);
      TestBed.inject(TravelProfitabilityService).getContext('VisaCase', 9).subscribe();
      http.expectOne(`${profitability}/VisaCase/9`).flush(null);
    });
  });
});

function round2(n: number): number { return Math.round((n + Number.EPSILON) * 100) / 100; }
