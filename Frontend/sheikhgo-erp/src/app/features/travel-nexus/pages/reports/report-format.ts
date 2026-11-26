import { ExportColumn } from '../../../../core/services/export.service';
import { FleetReport, FleetReportColumn, FleetReportRow } from '../../../../core/models/fleet-report.model';
import { TravelPipeline } from '../../models/travel.models';

/**
 * Formats one report cell. Money takes its currency from the row's `currency` / `currencyCode` field, else from the report's
 * reporting currency — never a hardcoded symbol or code.
 */
export function formatTravelField(value: unknown, format: string, currency: string | null): string {
  if (value === null || value === undefined || value === '') return '—';
  switch (format) {
    case 'currency': {
      const n = Number(value).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      return currency ? `${n} ${currency}` : n;
    }
    case 'number': return Number(value).toLocaleString('en-US', { maximumFractionDigits: 2 });
    case 'date': {
      const s = String(value);
      const d = new Date(s.length === 10 ? `${s}T00:00:00Z` : s);
      return Number.isNaN(d.getTime()) ? s : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
    }
    default: return String(value);
  }
}

/** The currency a row's money columns are in. */
export function rowCurrency(row: FleetReportRow, report: FleetReport): string | null {
  const own = row.fields['currency'] ?? row.fields['currencyCode'];
  if (typeof own === 'string' && own.length === 3) return own;
  const reporting = report.summary?.['reportingCurrencyCode'];
  return typeof reporting === 'string' ? reporting : null;
}

/** Export columns for the shared ExportService, formatted the same way as the grid. */
export function exportColumns(report: FleetReport): ExportColumn<FleetReportRow>[] {
  return report.columns.map(c => ({
    header: c.label,
    accessor: row => formatTravelField(row.fields[c.key], c.format, rowCurrency(row, report)),
    align: c.format === 'currency' || c.format === 'number' ? 'right' : 'left'
  }));
}

/** Summary totals that match a numeric column, for the grid footer. */
export function footerValue(report: FleetReport, column: FleetReportColumn): string {
  const v = report.summary?.[column.key];
  if (v === null || v === undefined) return '';
  if (column.format === 'currency') {
    const currencies = new Set(report.rows.map(r => rowCurrency(r, report)).filter((c): c is string => !!c));
    const currency = currencies.size === 1 ? [...currencies][0] : typeof report.summary?.['reportingCurrencyCode'] === 'string' ? report.summary['reportingCurrencyCode'] as string : null;
    return formatTravelField(v, 'currency', currency);
  }
  return formatTravelField(v, column.format, null);
}

/** "created · 1 Jan 2027 – 31 Mar 2027" or "as of 1 Apr 2027" — every report says which window it covers. */
export function windowText(report: FleetReport): string {
  const s = report.summary ?? {};
  const label = typeof s['windowLabel'] === 'string' ? s['windowLabel'] : '';
  if (s['windowFrom'] && s['windowTo']) return `${label} · ${formatTravelField(s['windowFrom'], 'date', null)} – ${formatTravelField(s['windowTo'], 'date', null)}`;
  return `${label}${s['asOf'] ? ' · ' + formatTravelField(s['asOf'], 'date', null) : ''}`;
}

/** A pipeline's counts must add up to its total; the UI shows the total only when they do. */
export function pipelineConsistent(p: TravelPipeline): boolean {
  return Object.values(p.counts).reduce((n, v) => n + v, 0) === p.total;
}

/** Bar-chart data from a report: one bar per row for a numeric column. */
export function chartFromReport(report: FleetReport, valueKey: string, colour: string): { labels: string[]; datasets: { label: string; data: number[]; backgroundColor: string }[] } {
  const column = report.columns.find(c => c.key === valueKey);
  return {
    labels: report.rows.map(r => r.label),
    datasets: [{ label: column?.label ?? valueKey, data: report.rows.map(r => Number(r.fields[valueKey] ?? 0)), backgroundColor: colour }]
  };
}
