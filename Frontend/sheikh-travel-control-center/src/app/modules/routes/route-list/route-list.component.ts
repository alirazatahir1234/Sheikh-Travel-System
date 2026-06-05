import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { RouteService } from '../../../core/services/route.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { Route } from '../../../core/models/route.model';
import {
  BulkRouteAddDialogComponent,
  BulkRouteAddDialogResult
} from '../bulk-route-add/bulk-route-add-dialog.component';

type ActiveFilter   = 'ALL' | 'ACTIVE' | 'INACTIVE';
type DistanceFilter = 'ALL' | 'SHORT' | 'MEDIUM' | 'LONG';
type PriceFilter    = 'ALL' | 'BUDGET' | 'MID' | 'PREMIUM';

/** Thresholds used by the smart filters (kept here so they stay in sync with the UI copy). */
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
export class RouteListComponent implements OnInit {
  displayedColumns = [
    'select', 'name', 'source', 'destination', 'distance',
    'estimatedMinutes', 'basePrice', 'isActive', 'actions'
  ];

  dataSource = new MatTableDataSource<Route>();
  allRoutes: Route[] = [];

  loading = true;
  deleting = false;
  error: string | null = null;
  totalCount = 0;

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

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private routeService: RouteService,
    private router: Router,
    private snackBar: MatSnackBar,
    private exportService: ExportService,
    private dialog: MatDialog,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void { this.load(); }

  // ---------- Data loading ---------------------------------------------------

  load(page = 1, pageSize = 500): void {
    this.loading = true;
    this.error = null;
    this.selection.clear();
    this.routeService.getAll(page, pageSize).subscribe({
      next: result => {
        this.allRoutes = result.items;
        this.totalCount = result.totalCount;
        this.applyFilters();
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load routes.'; }
    });
  }

  // ---------- Filter state ---------------------------------------------------

  applyFilters(): void {
    this.dataSource.data = this.allRoutes.filter(r => this.matches(r));
    this.selection.clear();
    setTimeout(() => {
      if (this.paginator) {
        this.dataSource.paginator = this.paginator;
      }
    });
  }

  setDistance(value: DistanceFilter): void {
    this.filters.distance = value;
    this.applyFilters();
  }

  resetFilters(): void {
    this.filters = this.emptyFilters();
    this.applyFilters();
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
    const inBand = (r: Route, band: DistanceFilter) => {
      const km = r.distance ?? 0;
      switch (band) {
        case 'SHORT':  return km > 0 && km < SHORT_KM_MAX;
        case 'MEDIUM': return km >= SHORT_KM_MAX && km <= MEDIUM_KM_MAX;
        case 'LONG':   return km > MEDIUM_KM_MAX;
        default:       return true;
      }
    };

    return [
      { label: 'All',                              value: 'ALL',    count: this.allRoutes.length },
      { label: `Short (< ${SHORT_KM_MAX}km)`,      value: 'SHORT',  count: this.allRoutes.filter(r => inBand(r, 'SHORT')).length },
      { label: `Medium (${SHORT_KM_MAX}–${MEDIUM_KM_MAX}km)`,
                                                   value: 'MEDIUM', count: this.allRoutes.filter(r => inBand(r, 'MEDIUM')).length },
      { label: `Long (> ${MEDIUM_KM_MAX}km)`,      value: 'LONG',   count: this.allRoutes.filter(r => inBand(r, 'LONG')).length }
    ];
  }

  // ---------- Selection ------------------------------------------------------

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

  // ---------- Row / bulk actions --------------------------------------------

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
      if (created > 0) this.load();
    });
  }

  // ---------- Export ---------------------------------------------------------

  exportExcel(): void {
    const { columns, meta } = this.buildExport();
    this.exportService.exportExcel(this.dataSource.data, columns, meta);
  }

  exportPdf(): void {
    const { columns, meta } = this.buildExport();
    this.exportService.exportPdf(this.dataSource.data, columns, meta);
  }

  private buildExport(): {
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
          `${this.dataSource.data.length} of ${this.allRoutes.length} route(s)`,
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

  // ---------- Display helpers ------------------------------------------------

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

  // ---------- Internals ------------------------------------------------------

  private matches(r: Route): boolean {
    const f = this.filters;

    if (f.active === 'ACTIVE'   && !r.isActive) return false;
    if (f.active === 'INACTIVE' &&  r.isActive) return false;

    if (!this.matchesDistance(r)) return false;
    if (!this.matchesPrice(r)) return false;

    const term = f.search.trim().toLowerCase();
    if (term) {
      const haystack = [
        r.name ?? '', r.source, r.destination,
        String(r.distance ?? ''), String(r.basePrice ?? '')
      ].join(' ').toLowerCase();
      if (!haystack.includes(term)) return false;
    }
    return true;
  }

  private matchesDistance(r: Route): boolean {
    const km = r.distance ?? 0;
    switch (this.filters.distance) {
      case 'ALL':    return true;
      case 'SHORT':  return km > 0 && km < SHORT_KM_MAX;
      case 'MEDIUM': return km >= SHORT_KM_MAX && km <= MEDIUM_KM_MAX;
      case 'LONG':   return km > MEDIUM_KM_MAX;
    }
  }

  private matchesPrice(r: Route): boolean {
    const p = r.basePrice ?? 0;
    switch (this.filters.price) {
      case 'ALL':     return true;
      case 'BUDGET':  return p <= BUDGET_PRICE;
      case 'MID':     return p > BUDGET_PRICE && p <= MID_PRICE;
      case 'PREMIUM': return p > MID_PRICE;
    }
  }

  private emptyFilters(): RouteFilters {
    return { search: '', active: 'ALL', distance: 'ALL', price: 'ALL' };
  }
}
