import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
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
  items: AuditEventListItem[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;

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
  retentionLabel = '';

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
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
    this.tenantContext.tenantId$.pipe(takeUntil(this.destroy$)).subscribe(id => {
      if (id) {
        this.page = 1;
        this.loadRetention(id);
        this.load();
      } else {
        this.items = [];
        this.totalCount = 0;
      }
    });
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

  applyFilters(): void {
    this.page = 1;
    this.load();
  }

  private loadRetention(tenantId: number): void {
    this.platform
      .getAuditRetention(tenantId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.retentionLabel = r
            ? `Retention ${r.retentionDays}d · Level ${r.auditLevel}${r.auditEnabled ? '' : ' (disabled)'}`
            : '';
        },
        error: () => (this.retentionLabel = '')
      });
  }

  load(): void {
    if (!this.selectedTenant) return;
    this.loading = true;
    this.platform
      .searchAuditEvents({
        page: this.page,
        pageSize: this.pageSize,
        tenantId: this.selectedTenant.id,
        category: this.categoryFilter || undefined,
        eventKey: this.eventKeyFilter || undefined,
        entityType: this.entityTypeFilter || undefined,
        severity: this.severityFilter || undefined,
        success:
          this.successFilter === ''
            ? undefined
            : this.successFilter === 'true',
        fromDate: this.fromDate ? new Date(`${this.fromDate}T00:00:00`).toISOString() : undefined,
        toDate: this.toDate ? new Date(`${this.toDate}T23:59:59`).toISOString() : undefined,
        search: this.searchFilter || undefined
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
    this.platform
      .getAuditEventById(row.id, this.selectedTenant?.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: d => (this.detail = d),
        error: err => this.toast.error(apiErrorMessage(err, 'Failed to load event detail.'))
      });
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.detail = null;
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
    if (!this.selectedTenant) return;
    this.exporting = true;
    this.platform
      .exportAuditEvents(
        {
          tenantId: this.selectedTenant.id,
          category: this.categoryFilter || undefined,
          eventKey: this.eventKeyFilter || undefined,
          entityType: this.entityTypeFilter || undefined,
          severity: this.severityFilter || undefined,
          success:
            this.successFilter === ''
              ? undefined
              : this.successFilter === 'true',
          fromDate: this.fromDate ? new Date(`${this.fromDate}T00:00:00`).toISOString() : undefined,
          toDate: this.toDate ? new Date(`${this.toDate}T23:59:59`).toISOString() : undefined,
          search: this.searchFilter || undefined
        },
        'csv'
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: blob => {
          this.exporting = false;
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `audit-export-${Date.now()}.csv`;
          a.click();
          URL.revokeObjectURL(url);
          this.toast.success('Export downloaded.');
        },
        error: err => {
          this.exporting = false;
          this.toast.error(apiErrorMessage(err, 'Export failed.'));
        }
      });
  }

  formatDate(value: string): string {
    try {
      return new Date(value).toLocaleString();
    } catch {
      return value;
    }
  }

  back(): void {
    this.router.navigate(['/platform']);
  }
}
