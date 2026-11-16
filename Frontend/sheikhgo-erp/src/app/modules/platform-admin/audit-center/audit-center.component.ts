import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  AuditEventDetail,
  AuditEventListItem,
  Tenant
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-audit-center',
  templateUrl: './audit-center.component.html',
  styleUrls: ['./audit-center.component.scss']
})
export class AuditCenterComponent implements OnInit, OnDestroy {
  loading = false;
  exporting = false;
  selectedTenant: Tenant | null = null;
  /** SG-06 / SG-07: all-companies mode (AuditReadAll) — no tenantId on search. */
  isAllCompanies = false;
  items: AuditEventListItem[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  readonly pageSizeOptions = [10, 20, 50, 100];

  categoryFilter = '';
  severityFilter = '';
  successFilter: '' | 'true' | 'false' = '';
  eventKeyFilter = '';
  entityTypeFilter = '';
  searchFilter = '';
  fromDate = '';
  toDate = '';

  categories: string[] = [];
  drawerOpen = false;
  detail: AuditEventDetail | null = null;
  detailError = '';
  detailLoading = false;
  retentionLabel = '';
  retentionDays: number | null = null;
  auditEnabled = true;

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: TenantContextService,
    private platform: PlatformService
  ) {}

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const tenantIdParam = q.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) this.tenantContext.selectTenantById(id);
    }
    this.categoryFilter = q.get('category') || '';
    this.eventKeyFilter = q.get('eventKey') || '';
    this.entityTypeFilter = q.get('entity') || q.get('entityType') || '';
    this.severityFilter = q.get('severity') || '';
    const successQ = q.get('success');
    if (successQ === 'true' || successQ === 'false') this.successFilter = successQ;
    this.fromDate = q.get('from') || '';
    this.toDate = q.get('to') || '';
    this.searchFilter = q.get('search') || '';

    this.platform
      .getAuditCatalog(true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: rows => {
          this.categories = [...new Set((rows ?? []).map(r => r.category))].sort((a, b) =>
            a.localeCompare(b)
          );
        },
        error: () => (this.categories = [])
      });

    this.tenantContext.tenant$.pipe(takeUntil(this.destroy$)).subscribe(t => (this.selectedTenant = t));
    this.tenantContext.isAllCompanies$.pipe(takeUntil(this.destroy$)).subscribe(all => {
      this.isAllCompanies = all;
      if (all) {
        this.page = 1;
        this.loadRetention(null);
        this.load();
      }
    });
    this.tenantContext.tenantId$.pipe(takeUntil(this.destroy$)).subscribe(id => {
      if (this.tenantContext.isAllCompanies) return;
      if (id) {
        this.page = 1;
        this.loadRetention(id);
        this.load();
      } else {
        this.items = [];
        this.totalCount = 0;
        this.retentionLabel = '';
        this.retentionDays = null;
      }
    });
  }

  /** Concrete company or all-companies with AuditReadAll. */
  get canLoad(): boolean {
    return this.isAllCompanies || !!this.selectedTenant;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.drawerOpen) this.closeDrawer();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get hasActiveFilters(): boolean {
    return !!(
      this.categoryFilter ||
      this.severityFilter ||
      this.successFilter ||
      this.eventKeyFilter ||
      this.entityTypeFilter ||
      this.searchFilter ||
      this.fromDate ||
      this.toDate
    );
  }

  get pageFailedCount(): number {
    return this.items.filter(i => !i.success).length;
  }

  get pageCriticalCount(): number {
    return this.items.filter(i => (i.severity || '').toLowerCase() === 'critical').length;
  }

  get dateRangeInvalid(): boolean {
    return !!(this.fromDate && this.toDate && this.fromDate > this.toDate);
  }

  applyFilters(): void {
    if (this.dateRangeInvalid) {
      this.toast.error('From date must be on or before To date.');
      return;
    }
    this.page = 1;
    this.load();
  }

  clearFilters(): void {
    this.categoryFilter = '';
    this.severityFilter = '';
    this.successFilter = '';
    this.eventKeyFilter = '';
    this.entityTypeFilter = '';
    this.searchFilter = '';
    this.fromDate = '';
    this.toDate = '';
    this.page = 1;
    this.load();
  }

  setQuickRange(days: number): void {
    const to = new Date();
    const from = new Date();
    from.setDate(to.getDate() - (days - 1));
    this.toDate = this.toInputDate(to);
    this.fromDate = this.toInputDate(from);
    this.applyFilters();
  }

  onPageSizeChange(): void {
    this.page = 1;
    this.load();
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private loadRetention(tenantId: number | null): void {
    this.platform
      .getAuditRetention(tenantId ?? undefined)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          if (!r) {
            this.retentionLabel = this.isAllCompanies ? 'All companies' : '';
            this.retentionDays = null;
            this.auditEnabled = true;
            return;
          }
          this.retentionDays = r.retentionDays;
          this.auditEnabled = r.auditEnabled;
          const scope = this.isAllCompanies ? 'All companies · ' : '';
          this.retentionLabel = `${scope}Retention ${r.retentionDays}d · Level ${r.auditLevel}${
            r.auditEnabled ? '' : ' (disabled)'
          }`;
        },
        error: () => {
          this.retentionLabel = this.isAllCompanies ? 'All companies' : '';
          this.retentionDays = null;
        }
      });
  }

  private buildFilterPayload() {
    return {
      tenantId: this.isAllCompanies ? undefined : this.selectedTenant?.id,
      category: this.categoryFilter || undefined,
      eventKey: this.eventKeyFilter || undefined,
      entityType: this.entityTypeFilter.trim() || undefined,
      severity: this.severityFilter || undefined,
      success:
        this.successFilter === ''
          ? undefined
          : this.successFilter === 'true',
      fromDate: this.fromDate ? new Date(`${this.fromDate}T00:00:00`).toISOString() : undefined,
      toDate: this.toDate ? new Date(`${this.toDate}T23:59:59`).toISOString() : undefined,
      search: this.searchFilter.trim() || undefined
    };
  }

  load(): void {
    if (!this.canLoad) return;
    if (this.dateRangeInvalid) {
      this.toast.error('From date must be on or before To date.');
      return;
    }
    this.loading = true;
    this.platform
      .searchAuditEvents({
        page: this.page,
        pageSize: this.pageSize,
        ...this.buildFilterPayload()
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.items = res?.items ?? [];
          this.totalCount = res?.totalCount ?? 0;
          this.loading = false;
        },
        error: err => {
          this.loading = false;
          this.toast.error(apiErrorMessage(err, 'Failed to load audit events.'));
        }
      });
  }

  openDetail(row: AuditEventListItem): void {
    this.drawerOpen = true;
    this.detail = null;
    this.detailError = '';
    this.detailLoading = true;
    const tenantId = this.isAllCompanies ? undefined : this.selectedTenant?.id;
    this.platform
      .getAuditEventById(row.id, tenantId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: d => {
          this.detail = d;
          this.detailLoading = false;
        },
        error: err => {
          this.detailLoading = false;
          this.detailError = apiErrorMessage(err, 'Failed to load event detail.');
          this.toast.error(this.detailError);
        }
      });
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.detail = null;
    this.detailError = '';
    this.detailLoading = false;
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page--;
    this.load();
  }

  nextPage(): void {
    if (this.page >= this.totalPages) return;
    this.page++;
    this.load();
  }

  exportCsv(): void {
    if (!this.canLoad) return;
    if (this.dateRangeInvalid) {
      this.toast.error('From date must be on or before To date.');
      return;
    }
    this.exporting = true;
    this.platform
      .exportAuditEvents(this.buildFilterPayload(), 'csv')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: async blob => {
          this.exporting = false;
          if (await this.isJsonErrorBlob(blob)) {
            this.toast.error('Export failed.');
            return;
          }
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          const scope = this.isAllCompanies ? 'all' : String(this.selectedTenant!.id);
          a.download = `audit-export-${scope}-${Date.now()}.csv`;
          a.click();
          URL.revokeObjectURL(url);
          this.toast.success('Export downloaded.');
        },
        error: async err => {
          this.exporting = false;
          const fromBlob = await this.messageFromErrorBlob(err);
          this.toast.error(fromBlob || apiErrorMessage(err, 'Export failed.'));
        }
      });
  }

  /** Detect JSON error envelopes mistakenly returned as blobs. */
  private async isJsonErrorBlob(blob: Blob): Promise<boolean> {
    const type = (blob.type || '').toLowerCase();
    if (type.includes('application/json') || type.includes('text/json')) return true;
    if (blob.size > 0 && blob.size < 4096 && (!type || type.includes('text/plain'))) {
      try {
        const text = await blob.slice(0, 64).text();
        return text.trimStart().startsWith('{') && text.includes('"success"');
      } catch {
        return false;
      }
    }
    return false;
  }

  private async messageFromErrorBlob(err: unknown): Promise<string | null> {
    const blob =
      err && typeof err === 'object' && 'error' in err && (err as { error: unknown }).error instanceof Blob
        ? ((err as { error: Blob }).error as Blob)
        : null;
    if (!blob) return null;
    try {
      const text = await blob.text();
      const parsed = JSON.parse(text) as { message?: string };
      return parsed?.message?.trim() || null;
    } catch {
      return null;
    }
  }

  formatDate(value: string): string {
    try {
      return new Date(value).toLocaleString();
    } catch {
      return value;
    }
  }

  formatJson(value: string | null | undefined): string {
    if (!value) return '—';
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  severityClass(severity: string | null | undefined): string {
    switch ((severity || '').toLowerCase()) {
      case 'critical':
        return 'sev-critical';
      case 'error':
        return 'sev-error';
      case 'warning':
        return 'sev-warning';
      case 'information':
        return 'sev-info';
      default:
        return 'sev-default';
    }
  }

  back(): void {
    this.router.navigate(['/platform']);
  }
}
