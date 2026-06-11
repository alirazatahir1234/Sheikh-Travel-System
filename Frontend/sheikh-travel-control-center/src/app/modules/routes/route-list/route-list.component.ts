import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, Subscription, forkJoin, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map } from 'rxjs/operators';

import { RouteService } from '../../../core/services/route.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { Route, RouteFilter, RouteListStats } from '../../../core/models/route.model';
import {
  BulkRouteAddDialogComponent,
  BulkRouteAddDialogResult
} from '../bulk-route-add/bulk-route-add-dialog.component';

type ActiveFilter   = 'ALL' | 'ACTIVE' | 'INACTIVE';
type DistanceFilter = 'ALL' | 'SHORT' | 'MEDIUM' | 'LONG';
type PriceFilter    = 'ALL' | 'BUDGET' | 'MID' | 'PREMIUM';

const SHORT_KM_MAX    = 150;
const MEDIUM_KM_MAX   = 500;
const BUDGET_PRICE    = 5_000;
const MID_PRICE       = 15_000;

interface RouteFilters {
  search:   string;
  active:   ActiveFilter;
  distance: DistanceFilter;
  price:    PriceFilter;
}

interface QuickChip {
  label: string;
  value: DistanceFilter;
  count: number;
}

@Component({
  selector: 'app-route-list',
  templateUrl: './route-list.component.html',
  styleUrls: ['./route-list.component.scss'],
  providers: [DatePipe]
})
export class RouteListComponent implements OnInit, OnDestroy {
  displayedColumns = [
    'select', 'name', 'source', 'destination', 'distance',
    'estimatedMinutes', 'basePrice', 'isActive', 'actions'
  ];

  dataSource = new MatTableDataSource<Route>();
  distanceStats: RouteListStats = { total: 0, short: 0, medium: 0, long: 0 };

  loading = true;
  deleting = false;
  error: string | null = null;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 25;

  readonly selection = new SelectionModel<Route>(true, []);

  readonly activeOptions: { value: ActiveFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'ACTIVE',   label: 'Active' },
    { value: 'INACTIVE', label: 'Inactive' }
  ];
  readonly priceOptions: { value: PriceFilter; label: string }[] = [
    { value: 'ALL',     label: 'All prices' },
    { value: 'BUDGET',  label: `Budget (≤ ${BUDGET_PRICE.toLocaleString('en-PK')})` },
    { value: 'MID',     label: `Mid (${BUDGET_PRICE.toLocaleString('en-PK')}–${MID_PRICE.toLocaleString('en-PK')})` },
    { value: 'PREMIUM', label: `Premium (> ${MID_PRICE.toLocaleString('en-PK')})` }
  ];

  filters: RouteFilters = this.emptyFilters();

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;

  constructor(
    private routeService: RouteService,
    private router: Router,
    private snackBar: MatSnackBar,
    private exportService: ExportService,
    private dialog: MatDialog,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    this.searchSub = this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.filters.search = term;
      this.load(true);
    });
    this.load();
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  load(resetPage = false): void {
    if (resetPage) {
      this.pageIndex = 0;
      this.selection.clear();
    }

    this.loading = true;
    this.error = null;

    forkJoin({
      page: this.routeService.getAll(this.pageIndex + 1, this.pageSize, this.buildFilter()),
      stats: this.routeService.getStats(this.buildStatsFilter()).pipe(
        catchError(() => of({ total: 0, short: 0, medium: 0, long: 0 }))
      )
    }).subscribe({
      next: ({ page, stats }) => {
        this.dataSource.data = page.items;
        this.totalCount = page.totalCount;
        this.distanceStats = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load routes.';
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }

  applyFilters(): void {
    this.load(true);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.selection.clear();
    this.load();
  }

  setDistance(value: DistanceFilter): void {
    this.filters.distance = value;
    this.load(true);
  }

  resetFilters(): void {
    this.filters = this.emptyFilters();
    this.load(true);
  }

  get activeFilterCount(): number {
    const f = this.filters;
    let n = 0;
    if (f.search.trim())      n++;
    if (f.active   !== 'ALL') n++;
    if (f.distance !== 'ALL') n++;
    if (f.price    !== 'ALL') n++;
    return n;
  }

  get distanceChips(): QuickChip[] {
    const s = this.distanceStats;
    return [
      { label: 'All',                              value: 'ALL',    count: s.total },
      { label: `Short (< ${SHORT_KM_MAX}km)`,      value: 'SHORT',  count: s.short },
      { label: `Medium (${SHORT_KM_MAX}–${MEDIUM_KM_MAX}km)`, value: 'MEDIUM', count: s.medium },
      { label: `Long (> ${MEDIUM_KM_MAX}km)`,      value: 'LONG',   count: s.long }
    ];
  }

  isAllSelected(): boolean {
    return this.dataSource.data.length > 0 &&
      this.dataSource.data.every(r => this.selection.isSelected(r));
  }

  masterToggle(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.selection.select(...this.dataSource.data);
    }
  }

  edit(id: number): void { this.router.navigate(['/routes', id, 'edit']); }

  delete(id: number): void {
    if (!confirm('Delete this route?')) return;
    this.routeService.delete(id).subscribe({
      next: () => { this.snackBar.open('Route deleted', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  bulkDelete(): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;
    if (!confirm(`Delete ${selected.length} selected route${selected.length > 1 ? 's' : ''}? This cannot be undone.`)) return;

    this.deleting = true;
    const calls = selected.map(r =>
      this.routeService.delete(r.id).pipe(
        map(() => ({ id: r.id, ok: true as const })),
        catchError(() => of({ id: r.id, ok: false as const }))
      )
    );

    forkJoin(calls).subscribe(results => {
      this.deleting = false;
      const ok = results.filter(x => x.ok).length;
      const failed = results.length - ok;
      const msg = failed === 0
        ? `Deleted ${ok} route${ok > 1 ? 's' : ''}.`
        : `Deleted ${ok}, failed ${failed}.`;
      this.snackBar.open(msg, 'Close', { duration: 3500 });
      this.load();
    });
  }

  openBulkAdd(): void {
    const ref = this.dialog.open(BulkRouteAddDialogComponent, {
      width: '720px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable',
      restoreFocus: true
    });

    ref.afterClosed().subscribe((result?: BulkRouteAddDialogResult) => {
      if (!result) return;
      const { created, failed } = result;
      const msg = failed === 0
        ? `Added ${created} route${created > 1 ? 's' : ''}.`
        : `Added ${created}, failed ${failed}. Check the dialog for details.`;
      this.snackBar.open(msg, 'Close', { duration: 4000 });
      if (created > 0) this.load(true);
    });
  }

  exportExcel(): void {
    this.fetchForExport(rows => {
      const { columns, meta } = this.buildExport(rows);
      this.exportService.exportExcel(rows, columns, meta);
    });
  }

  exportPdf(): void {
    this.fetchForExport(rows => {
      const { columns, meta } = this.buildExport(rows);
      this.exportService.exportPdf(rows, columns, meta);
    });
  }

  formatDuration(minutes?: number | null): string {
    if (minutes == null) return '—';
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    if (h === 0) return `${m}m`;
    if (m === 0) return `${h}h`;
    return `${h}h ${m}m`;
  }

  formatKm(km?: number | null): string {
    if (km == null) return '—';
    return km.toLocaleString('en-PK', { maximumFractionDigits: 1 });
  }

  formatPrice(value: number): string {
    return value.toLocaleString('en-PK', { maximumFractionDigits: 0 });
  }

  trackById(_index: number, item: Route): number {
    return item.id;
  }

  private fetchForExport(onReady: (rows: Route[]) => void): void {
    this.routeService.getAll(1, 10000, this.buildFilter()).subscribe({
      next: r => onReady(r.items),
      error: () => this.snackBar.open('Failed to load routes for export.', 'Close', { duration: 3000 })
    });
  }

  private buildExport(rows: Route[]): {
    columns: ExportColumn<Route>[];
    meta: { filename: string; title: string; subtitle: string; sheetName: string };
  } {
    const columns: ExportColumn<Route>[] = [
      { header: 'Name',        accessor: r => r.name || '—',                                  excelWidth: 28, pdfWeight: 2.2 },
      { header: 'From',        accessor: r => r.source,                                       excelWidth: 22, pdfWeight: 1.6 },
      { header: 'To',          accessor: r => r.destination,                                  excelWidth: 22, pdfWeight: 1.6 },
      { header: 'Distance (km)', accessor: r => this.formatKm(r.distance),    align: 'right', excelWidth: 14, pdfWeight: 1   },
      { header: 'Duration',    accessor: r => this.formatDuration(r.estimatedMinutes), align: 'center',
                                                                                               excelWidth: 12, pdfWeight: 0.9 },
      { header: 'Base Price',  accessor: r => this.formatPrice(r.basePrice),  align: 'right', excelWidth: 14, pdfWeight: 1.1 },
      { header: 'Active',      accessor: r => r.isActive ? 'Yes' : 'No',      align: 'center', excelWidth: 10, pdfWeight: 0.6 },
      { header: 'Created',     accessor: r => this.datePipe.transform(r.createdAt, 'mediumDate') ?? '',
                                                                                               excelWidth: 16, pdfWeight: 1.1 }
    ];

    const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
    const scope = this.activeFilterCount > 0 ? 'filtered' : 'all';
    const filterSummary = this.describeActiveFilters();

    return {
      columns,
      meta: {
        filename: `routes-${scope}-${stamp}`,
        title: 'Routes',
        subtitle: [
          `${rows.length} of ${this.totalCount} route(s)`,
          filterSummary
        ].filter(Boolean).join(' · '),
        sheetName: 'Routes'
      }
    };
  }

  private describeActiveFilters(): string {
    const f = this.filters;
    const parts: string[] = [];

    if (f.active   !== 'ALL') parts.push(`Active: ${this.activeOptions.find(o => o.value === f.active)?.label}`);
    if (f.distance !== 'ALL') parts.push(`Distance: ${this.distanceChips.find(c => c.value === f.distance)?.label}`);
    if (f.price    !== 'ALL') parts.push(`Price: ${this.priceOptions.find(o => o.value === f.price)?.label}`);
    if (f.search.trim())      parts.push(`Search: "${f.search.trim()}"`);

    return parts.length ? `Filters — ${parts.join('; ')}` : '';
  }

  private buildFilter(): RouteFilter {
    const f = this.filters;
    return {
      search: f.search.trim() || undefined,
      isActive: f.active === 'ALL' ? undefined : f.active === 'ACTIVE',
      distanceBand: f.distance === 'ALL' ? undefined : f.distance,
      priceBand: f.price === 'ALL' ? undefined : f.price
    };
  }

  private buildStatsFilter(): Omit<RouteFilter, 'distanceBand'> {
    const { distanceBand: _distance, ...statsFilter } = this.buildFilter();
    return statsFilter;
  }

  private emptyFilters(): RouteFilters {
    return { search: '', active: 'ALL', distance: 'ALL', price: 'ALL' };
  }
}
