import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../../environments/environment';
import { FleetReport } from '../../../../core/models/fleet-report.model';
import { statusFromQuery } from '../../models/travel-date.util';
import {
  REMINDER_ENTITY_ROUTE, REMINDER_KIND_LABEL, REMINDER_STATUS_VARIANT, TRAVEL_ENQUIRY_STATUSES, TRAVEL_REMINDER_RULE_TYPES, TRAVEL_REMINDER_STATUSES,
  TravelPipeline
} from '../../models/travel.models';
import { TravelDashboardService } from '../../services/travel-dashboard.service';
import { TravelReminderService } from '../../services/travel-reminder.service';
import { TravelReportService } from '../../services/travel-report.service';
import { addLead, draftProblem } from '../reminders/reminders-page.component';
import { chartFromReport, exportColumns, footerValue, formatTravelField, pipelineConsistent, rowCurrency, windowText } from './report-format';

const report: FleetReport = {
  reportType: 'bookings', title: 'Flight and hotel bookings', totalValue: 3,
  columns: [{ key: 'supplier', label: 'Supplier', format: 'text' }, { key: 'currency', label: 'Currency', format: 'text' },
    { key: 'services', label: 'Services', format: 'number' }, { key: 'sell', label: 'Sell', format: 'currency' }, { key: 'departure', label: 'Departure', format: 'date' }],
  rows: [
    { key: 'a', label: 'PIA', count: 2, totalValue: 2, fields: { supplier: 'PIA', currency: 'PKR', services: 2, sell: 150000, departure: '2027-03-01' } },
    { key: 'b', label: 'Hilton', count: 1, totalValue: 1, fields: { supplier: 'Hilton', currency: 'SAR', services: 1, sell: 8000.5, departure: null } }
  ],
  summary: { services: 3, windowLabel: 'travel', windowFrom: '2027-01-01', windowTo: '2027-03-31', asOf: '2027-04-01', reportingCurrencyCode: 'PKR' }
};

describe('TravelNexus Phase 7 (reminders, dashboard, reports)', () => {
  it('labels every reminder status, kind and entity route', () => {
    for (const s of TRAVEL_REMINDER_STATUSES) expect(REMINDER_STATUS_VARIANT[s]).toBeTruthy();
    expect(Object.keys(REMINDER_KIND_LABEL)).toEqual(['Upcoming', 'Overdue', 'Event']);
    expect(Object.keys(REMINDER_ENTITY_ROUTE)).toEqual(['Traveler', 'VisaCase', 'FlightBooking', 'HotelBooking', 'Invoice', 'Enquiry', 'Group']);
    expect(TRAVEL_REMINDER_RULE_TYPES).toHaveSize(12);
  });

  it('formats report cells with the row currency, never a hardcoded code', () => {
    expect(formatTravelField(150000, 'currency', 'PKR')).toBe('150,000.00 PKR');
    expect(formatTravelField(8000.5, 'currency', 'SAR')).toBe('8,000.50 SAR');
    expect(formatTravelField(8000.5, 'currency', null)).toBe('8,000.50');
    expect(formatTravelField(null, 'currency', 'PKR')).toBe('—');
    expect(formatTravelField('2027-03-01', 'date', null)).toBe('1 Mar 2027');
    expect(formatTravelField(12.345, 'number', null)).toBe('12.35');
    expect(rowCurrency(report.rows[1], report)).toBe('SAR');
    expect(rowCurrency({ ...report.rows[1], fields: { ...report.rows[1].fields, currency: undefined } }, report)).toBe('PKR');
  });

  it('exports every column formatted like the grid and states the window', () => {
    const cols = exportColumns(report);
    expect(cols.map(c => c.header)).toEqual(['Supplier', 'Currency', 'Services', 'Sell', 'Departure']);
    expect(cols[3].accessor(report.rows[1])).toBe('8,000.50 SAR');
    expect(cols[3].align).toBe('right');
    expect(windowText(report)).toBe('travel · 1 Jan 2027 – 31 Mar 2027');
    expect(windowText({ ...report, summary: { windowLabel: 'as of today', asOf: '2027-04-01' } })).toBe('as of today · 1 Apr 2027');
  });

  it('footer totals only sum what the API summarised and respect mixed currencies', () => {
    expect(footerValue(report, report.columns[2])).toBe('3');
    expect(footerValue(report, report.columns[3])).toBe('', 'no summary for sell across PKR and SAR');
    const single = { ...report, rows: [report.rows[0]], summary: { ...report.summary, sell: 150000 } };
    expect(footerValue(single, single.columns[3])).toBe('150,000.00 PKR');
    const chart = chartFromReport(report, 'services', '#000');
    expect(chart.labels).toEqual(['PIA', 'Hilton']);
    expect(chart.datasets[0].data).toEqual([2, 1]);
  });

  it('a pipeline shows its total only when the parts add up', () => {
    const ok: TravelPipeline = { counts: { New: 2, Won: 3 }, total: 5 };
    const bad: TravelPipeline = { counts: { New: 2, Won: 3 }, total: 6 };
    expect(pipelineConsistent(ok)).toBeTrue();
    expect(pipelineConsistent(bad)).toBeFalse();
  });

  it('only trusts a status query value that is a known status', () => {
    expect(statusFromQuery('Won', TRAVEL_ENQUIRY_STATUSES)).toBe('Won');
    expect(statusFromQuery('DROP TABLE', TRAVEL_ENQUIRY_STATUSES)).toBe('');
    expect(statusFromQuery(null, TRAVEL_ENQUIRY_STATUSES)).toBe('');
  });

  it('reminder lead rows are added once, in order, within range, and the draft is validated', () => {
    const base = [{ leadDays: 30, isEnabled: true, staffInApp: true, staffEmail: false, customerEmail: true, customerSms: false, customerWhatsApp: false }];
    const next = addLead(base, 7, true, true)!;
    expect(next.map(l => l.leadDays)).toEqual([7, 30]);
    expect(next[0].customerEmail).toBeTrue();
    expect(addLead(base, 30, true, true)).toBeNull();
    expect(addLead(base, 400, true, true)).toBeNull();
    expect(addLead(base, 2.5, true, true)).toBeNull();
    expect(addLead(base, 0, true, false)![0].customerEmail).toBeFalse();
    expect(draftProblem({ leads: [], customerTemplate: '' }, 'Upcoming')).toContain('at least one');
    expect(draftProblem({ leads: next, customerTemplate: '' }, 'Overdue')).toContain('lead time of 0');
    expect(draftProblem({ leads: base, customerTemplate: 'x'.repeat(1001) }, 'Upcoming')).toContain('too long');
    expect(draftProblem({ leads: base, customerTemplate: 'Pay {{Amount}}' }, 'Upcoming')).toBeNull();
  });

  describe('services', () => {
    let http: HttpTestingController;
    const api = environment.apiUrl;
    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
    });
    afterEach(() => http.verify());

    it('routes each report group to its permission route and drops empty filters', () => {
      const svc = TestBed.inject(TravelReportService);
      svc.getReport('Operational', 'visa-processing', { from: '2027-01-01', to: '2027-03-31', branchId: null, countryCode: '' }).subscribe();
      const op = http.expectOne(r => r.url === `${api}/travel/reports/visa-processing`);
      expect(op.request.params.get('from')).toBe('2027-01-01');
      expect(op.request.params.has('branchId')).toBeFalse();
      expect(op.request.params.has('countryCode')).toBeFalse();
      op.flush(report);
      svc.getReport('Finance', 'customer-balances', {}).subscribe();
      http.expectOne(`${api}/travel/reports/finance/customer-balances`).flush(report);
      svc.getReport('Profit', 'profit-loss', { from: '2027-01-01', to: '2027-12-31', agentUserId: 7 }).subscribe();
      expect(http.expectOne(r => r.url === `${api}/travel/reports/profit/profit-loss`).request.params.get('agentUserId')).toBe('7');
    });

    it('sends reminder log filters and saves a rule on its type', () => {
      const svc = TestBed.inject(TravelReminderService);
      svc.getLog({ page: 1, pageSize: 25, status: 'Failed', ruleType: '', audience: 'Customer', from: '2027-03-01', to: '' }).subscribe();
      const req = http.expectOne(r => r.url === `${api}/travel/reminders/log`);
      expect(req.request.params.get('status')).toBe('Failed');
      expect(req.request.params.get('audience')).toBe('Customer');
      expect(req.request.params.has('ruleType')).toBeFalse();
      expect(req.request.params.has('to')).toBeFalse();
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 25 });
      svc.saveRule('InvoiceDue', { leads: [], customerTemplate: null }).subscribe();
      expect(http.expectOne(`${api}/travel/reminders/rules/InvoiceDue`).request.method).toBe('PUT');
      svc.run().subscribe();
      expect(http.expectOne(`${api}/travel/reminders/run`).request.method).toBe('POST');
    });

    it('dashboard upcoming carries the day window', () => {
      TestBed.inject(TravelDashboardService).getUpcoming(14).subscribe();
      expect(http.expectOne(r => r.url === `${api}/travel/dashboard/upcoming`).request.params.get('days')).toBe('14');
    });
  });
});
