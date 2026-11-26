import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiEmptyStateComponent, UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { FleetReport, FleetReportRow } from '../../../../core/models/fleet-report.model';
import { AuthService } from '../../../../core/services/auth.service';
import { ExportService } from '../../../../core/services/export.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TRAVEL_PERMISSIONS, TravelCountry, TravelLookupItem, TravelReportDefinition, TravelReportGroup } from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelReportFilters, TravelReportService } from '../../services/travel-report.service';
import { chartFromReport, exportColumns, footerValue, formatTravelField, rowCurrency, windowText } from './report-format';

const iso = (d: Date): string => d.toISOString().substring(0, 10);
const CHART_KEYS: Record<string, string> = { 'country-applications': 'cases', 'visa-processing': 'submitted', 'profit-loss': 'margin', 'enquiry-performance': 'won', 'agent-branch-performance': 'invoiced' };

@Component({
  selector: 'app-travel-reports-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiChartComponent, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Reports & Analytics" eyebrow="TravelNexus" icon="analytics"
        subtitle="Twelve reports over your enquiries, visas, groups, bookings, money and margins. Every report states the window it covers."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Reports & Analytics' }]">
      </ui-page-header>

      <div class="grid gap-5 lg:grid-cols-4">
        <nav class="tn-card tn-card-body space-y-1" aria-label="Reports">
          @for (g of groups(); track g.group) {
            <p class="mt-2 text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">{{ g.label }}</p>
            @for (r of g.reports; track r.type) {
              <button type="button" class="w-full rounded px-2 py-1.5 text-left text-sm hover:bg-fleet-surface-alt" [class.bg-fleet-surface-alt]="selected()?.type === r.type"
                [class.font-semibold]="selected()?.type === r.type" (click)="select(r)">{{ r.title }}</button>
            }
          }
          @if (!groups().length) { <p class="text-sm text-fleet-text-muted">You have no report permission.</p> }
        </nav>

        <div class="space-y-4 lg:col-span-3">
          @if (selected(); as r) {
            <div class="tn-card tn-card-body">
              <h3 class="tn-section-title">{{ r.title }}</h3>
              <p class="text-xs text-fleet-text-muted">{{ r.description }}</p>
              <div class="mt-3 flex flex-wrap items-end gap-3">
                @if (r.windowed) {
                  <label class="tn-field w-40"><span class="text-xs">{{ r.windowLabel }} from</span><input type="date" class="tn-input" [(ngModel)]="from" /></label>
                  <label class="tn-field w-40"><span class="text-xs">{{ r.windowLabel }} to</span><input type="date" class="tn-input" [(ngModel)]="to" /></label>
                } @else { <p class="text-xs text-fleet-text-muted self-center">As of today — no date window.</p> }
                @if (r.group !== 'Finance' || r.type === 'pilgrim-payments') {
                  <label class="tn-field w-44"><span class="text-xs">Branch</span>
                    <select class="tn-input" [(ngModel)]="branchId"><option [ngValue]="null">All branches</option>
                      @for (b of branches(); track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }</select></label>
                }
                @if (r.group === 'Operational' || r.type === 'profit-loss') {
                  <label class="tn-field w-44"><span class="text-xs">Agent</span>
                    <select class="tn-input" [(ngModel)]="agentUserId"><option [ngValue]="null">All agents</option>
                      @for (a of agents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></label>
                }
                @if (r.type === 'agent-branch-performance') {
                  <label class="tn-field w-36"><span class="text-xs">Per</span>
                    <select class="tn-input" [(ngModel)]="dimension"><option value="Agent">Agent</option><option value="Branch">Branch</option></select></label>
                }
                @if (r.type === 'visa-processing' || r.type === 'country-applications' || r.type === 'pending-documents') {
                  <label class="tn-field w-48"><span class="text-xs">Country</span>
                    <select class="tn-input" [(ngModel)]="countryCode"><option value="">All countries</option>
                      @for (c of countries(); track c.code) { <option [value]="c.code">{{ c.name }}</option> }</select></label>
                }
                <button mat-flat-button color="primary" [disabled]="loading() || (r.windowed && (!from || !to))" (click)="run()"><mat-icon>play_arrow</mat-icon> Run</button>
              </div>
              @if (r.windowed && from && to && to < from) { <p class="mt-2 text-xs text-red-600">The end date is before the start date — the report will be empty.</p> }
            </div>

            @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
            @if (report(); as rep) {
              <div class="tn-card">
                <div class="flex flex-wrap items-center justify-between gap-2 border-b border-fleet-border px-5 py-4">
                  <div><h3 class="tn-section-title">{{ rep.title }}</h3><p class="text-xs text-fleet-text-muted">{{ window() }} · {{ rep.rows.length }} row(s){{ note() ? ' · ' + note() : '' }}</p></div>
                  <div class="flex gap-2">
                    <button mat-stroked-button [disabled]="!rep.rows.length" (click)="export('pdf')"><mat-icon>picture_as_pdf</mat-icon> PDF</button>
                    <button mat-stroked-button [disabled]="!rep.rows.length" (click)="export('xlsx')"><mat-icon>table_view</mat-icon> Excel</button>
                    <button mat-stroked-button [disabled]="!rep.rows.length" (click)="export('csv')"><mat-icon>download</mat-icon> CSV</button>
                  </div>
                </div>
                @if (missingRates()) { <div class="mx-5 mt-3 tn-banner warn"><mat-icon>currency_exchange</mat-icon>{{ missingRates() }} currency(ies) have no exchange rate and are left out.</div> }
                @if (chart(); as c) { <div class="px-5 pt-4"><ui-chart type="bar" [data]="c" height="220px" /></div> }
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                      @for (c of rep.columns; track c.key) { <th class="px-4 py-2" [class.text-right]="c.format === 'currency' || c.format === 'number'" [class.text-left]="c.format === 'text' || c.format === 'date'">{{ c.label }}</th> }
                    </tr></thead>
                    <tbody class="divide-y divide-fleet-border">
                      @if (!rep.rows.length) { <tr><td [attr.colspan]="rep.columns.length"><ui-empty-state icon="analytics" title="No rows" description="Nothing matches this window and filters."></ui-empty-state></td></tr> }
                      @for (row of rep.rows; track row.key) {
                        <tr>@for (c of rep.columns; track c.key) {
                          <td class="px-4 py-2" [class.text-right]="c.format === 'currency' || c.format === 'number'">{{ cell(row, c.key, c.format) }}</td> }</tr>
                      }
                    </tbody>
                    @if (rep.rows.length && hasFooter()) {
                      <tfoot class="bg-fleet-surface-alt/40 text-sm font-semibold"><tr>
                        @for (c of rep.columns; track c.key; let i = $index) { <td class="px-4 py-2" [class.text-right]="c.format === 'currency' || c.format === 'number'">{{ i === 0 ? 'Total' : footer(c.key) }}</td> }
                      </tr></tfoot>
                    }
                  </table>
                </div>
              </div>
            } @else if (loading()) { <p class="text-sm text-fleet-text-muted">Running…</p> }
          } @else {
            <div class="tn-card"><ui-empty-state icon="analytics" title="Choose a report" description="Pick a report on the left, set the window and run it."></ui-empty-state></div>
          }
        </div>
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelReportsPageComponent {
  private readonly api = inject(TravelReportService);
  private readonly config = inject(TravelConfigService);
  private readonly exporter = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly catalog = signal<TravelReportDefinition[]>([]);
  protected readonly selected = signal<TravelReportDefinition | null>(null);
  protected readonly report = signal<FleetReport | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly branches = signal<TravelLookupItem[]>([]);
  protected readonly agents = signal<TravelLookupItem[]>([]);
  protected readonly countries = signal<TravelCountry[]>([]);

  protected from = iso(new Date(Date.now() - 90 * 86_400_000));
  protected to = iso(new Date());
  protected branchId: number | null = null;
  protected agentUserId: number | null = null;
  protected dimension: 'Agent' | 'Branch' = 'Agent';
  protected countryCode = '';

  /** Only the groups the user may run — the API refuses the rest anyway. */
  protected readonly groups = computed(() => {
    const allowed: Record<TravelReportGroup, boolean> = {
      Operational: this.auth.hasAnyPermission([TRAVEL_PERMISSIONS.reportView]),
      Finance: this.auth.hasAnyPermission([TRAVEL_PERMISSIONS.financeView]),
      Profit: this.auth.hasAnyPermission([TRAVEL_PERMISSIONS.profitabilityView])
    };
    const labels: Record<TravelReportGroup, string> = { Operational: 'Operations', Finance: 'Money', Profit: 'Margins' };
    return (['Operational', 'Finance', 'Profit'] as TravelReportGroup[]).filter(g => allowed[g])
      .map(g => ({ group: g, label: labels[g], reports: this.catalog().filter(r => r.group === g) })).filter(g => g.reports.length);
  });
  protected readonly window = computed(() => this.report() ? windowText(this.report()!) : '');
  protected readonly note = computed(() => { const n = this.report()?.summary?.['note']; return typeof n === 'string' ? n : ''; });
  protected readonly missingRates = computed(() => { const n = this.report()?.summary?.['missingRates']; return typeof n === 'number' && n > 0 ? n : 0; });
  protected readonly chart = computed<ChartData | null>(() => {
    const rep = this.report();
    const key = rep ? CHART_KEYS[rep.reportType] : undefined;
    return rep && key && rep.rows.length > 1 && rep.rows.length <= 40 ? chartFromReport(rep, key, '#0f766e') : null;
  });

  constructor() {
    this.api.getCatalog().pipe(catchError(() => of([] as TravelReportDefinition[])), takeUntilDestroyed(this.destroyRef)).subscribe(c => this.catalog.set(c));
    this.config.getLookups().pipe(catchError(() => of({ branches: [], agents: [] })), takeUntilDestroyed(this.destroyRef))
      .subscribe(l => { this.branches.set(l.branches); this.agents.set(l.agents); });
    this.config.countries$.pipe(catchError(() => of([] as TravelCountry[])), takeUntilDestroyed(this.destroyRef)).subscribe(c => this.countries.set(c));
  }

  protected select(r: TravelReportDefinition): void { this.selected.set(r); this.report.set(null); this.error.set(null); }

  protected run(): void {
    const r = this.selected();
    if (!r) return;
    const f: TravelReportFilters = {
      from: r.windowed ? this.from : undefined, to: r.windowed ? this.to : undefined, branchId: this.branchId, agentUserId: this.agentUserId,
      dimension: r.type === 'agent-branch-performance' ? this.dimension : '', countryCode: this.countryCode || undefined
    };
    this.loading.set(true);
    this.error.set(null);
    this.api.getReport(r.group, r.type, f).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rep => { this.report.set(rep); this.loading.set(false); },
      error: err => { this.loading.set(false); this.error.set(apiErrorMessage(err, 'Could not run the report.')); }
    });
  }

  protected cell(row: FleetReportRow, key: string, format: string): string { return formatTravelField(row.fields[key], format, rowCurrency(row, this.report()!)); }
  protected footer(key: string): string { const rep = this.report()!; const c = rep.columns.find(x => x.key === key)!; return footerValue(rep, c); }
  protected hasFooter(): boolean { const rep = this.report(); return !!rep && rep.columns.some(c => (c.format === 'number' || c.format === 'currency') && rep.summary?.[c.key] !== undefined && rep.summary?.[c.key] !== null); }

  protected async export(kind: 'pdf' | 'xlsx' | 'csv'): Promise<void> {
    const rep = this.report();
    if (!rep) return;
    const meta = { filename: `${rep.reportType}-${this.from}-${this.to}`, title: rep.title, subtitle: this.window(), sheetName: 'Report' };
    try {
      if (kind === 'pdf') await this.exporter.exportPdf(rep.rows, exportColumns(rep), meta);
      else if (kind === 'xlsx') this.exporter.exportExcel(rep.rows, exportColumns(rep), meta);
      else this.exporter.exportCsv(rep.rows, exportColumns(rep), meta);
    } catch { this.toast.error('Could not build the export.'); }
  }
}
