import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { skip } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { ClientTrackingLink, ClientTrackingLinkCreated } from '../../../core/models/gps-tracking.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { GpsScopeFilterService } from '../gps-scope-filter.service';

type LinkStatusFilter = 'all' | 'active' | 'expired' | 'revoked';

@Component({
  standalone: false,
  selector: 'app-client-tracking-links-page',
  templateUrl: './client-tracking-links-page.component.html',
  styleUrls: ['./client-tracking-links-page.component.scss']
})
export class ClientTrackingLinksPageComponent implements OnInit, OnDestroy {
  loading = false;
  creating = false;
  revokingId: number | null = null;
  loadError: string | null = null;
  rows: ClientTrackingLink[] = [];

  tripIdInput: number | null = null;
  hoursValid = 48;
  created: ClientTrackingLinkCreated | null = null;
  createError: string | null = null;
  copyMessage: string | null = null;

  searchQuery = '';
  statusFilter: LinkStatusFilter = 'all';
  pageSize = 25;
  pageIndex = 0;

  /** Session-only: list API never returns raw tokens (security). Map after create. */
  private readonly knownUrls = new Map<number, string>();
  private scopeSub: Subscription | null = null;
  private loadSub: Subscription | null = null;
  private copyTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private gps: GpsTrackingService,
    private scopeFilter: GpsScopeFilterService
  ) {}

  ngOnInit(): void {
    this.load();
    this.scopeSub = this.scopeFilter.scope$.pipe(skip(1)).subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.scopeSub?.unsubscribe();
    this.loadSub?.unsubscribe();
    if (this.copyTimer) clearTimeout(this.copyTimer);
  }

  get needsCompanySelection(): boolean {
    return !this.scopeFilter.isFleetScopeReady;
  }

  get kpiActive(): number {
    return this.rows.filter(r => this.linkStatus(r) === 'active').length;
  }

  get kpiViews(): number {
    return this.rows.reduce((sum, r) => sum + (r.viewCount ?? 0), 0);
  }

  get kpiExpired(): number {
    return this.rows.filter(r => this.linkStatus(r) === 'expired').length;
  }

  get kpiRevoked(): number {
    return this.rows.filter(r => this.linkStatus(r) === 'revoked').length;
  }

  get filteredRows(): ClientTrackingLink[] {
    const q = this.searchQuery.trim().toLowerCase();
    return this.rows.filter(r => {
      if (this.statusFilter !== 'all' && this.linkStatus(r) !== this.statusFilter) return false;
      if (!q) return true;
      const trip = (r.tripNumber ?? `trip #${r.tripId}`).toLowerCase();
      const url = (this.displayUrl(r) ?? '').toLowerCase();
      return (
        trip.includes(q) ||
        url.includes(q) ||
        String(r.tripId).includes(q) ||
        String(r.id).includes(q)
      );
    });
  }

  get totalFiltered(): number {
    return this.filteredRows.length;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalFiltered / this.pageSize));
  }

  get pagedRows(): ClientTrackingLink[] {
    const start = this.pageIndex * this.pageSize;
    return this.filteredRows.slice(start, start + this.pageSize);
  }

  get pageStart(): number {
    if (!this.totalFiltered) return 0;
    return this.pageIndex * this.pageSize + 1;
  }

  get pageEnd(): number {
    return Math.min(this.totalFiltered, (this.pageIndex + 1) * this.pageSize);
  }

  load(): void {
    if (this.needsCompanySelection) {
      this.rows = [];
      this.loading = false;
      this.loadError = null;
      return;
    }

    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadError = null;
    this.loadSub = this.gps.listClientTrackingLinks(100).subscribe({
      next: rows => {
        this.rows = rows ?? [];
        for (const r of this.rows) {
          if (this.isOpenablePublicPath(r.publicPath)) {
            this.knownUrls.set(r.id, this.absoluteUrl(r.publicPath));
          }
        }
        this.loading = false;
        this.clampPage();
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  onSearchChange(): void {
    this.pageIndex = 0;
  }

  onStatusFilterChange(): void {
    this.pageIndex = 0;
  }

  onPageSizeChange(): void {
    this.pageIndex = 0;
  }

  prevPage(): void {
    if (this.pageIndex > 0) this.pageIndex--;
  }

  nextPage(): void {
    if (this.pageIndex < this.totalPages - 1) this.pageIndex++;
  }

  createLink(): void {
    if (this.creating) return;
    if (!this.tripIdInput || this.tripIdInput < 1) {
      this.createError = 'Enter a valid trip ID.';
      return;
    }
    const hours = Math.round(Number(this.hoursValid));
    if (!Number.isFinite(hours) || hours < 1 || hours > 168) {
      this.createError = 'Valid duration must be between 1 and 168 hours.';
      return;
    }

    this.createError = null;
    this.created = null;
    this.creating = true;
    this.gps.createClientTrackingLink(this.tripIdInput, hours).subscribe({
      next: res => {
        this.created = res;
        this.creating = false;
        if (res?.linkId != null && res.publicUrl) {
          this.knownUrls.set(res.linkId, this.absoluteUrl(res.publicUrl));
        }
        this.load();
      },
      error: err => {
        this.createError = apiErrorMessage(err);
        this.creating = false;
      }
    });
  }

  revoke(row: ClientTrackingLink): void {
    if (!row.isActive || this.revokingId != null) return;
    if (!confirm(`Revoke tracking link for ${this.tripLabel(row)}? Customers will lose access immediately.`)) {
      return;
    }
    this.revokingId = row.id;
    this.gps.revokeClientTrackingLink(row.id).subscribe({
      next: () => {
        this.revokingId = null;
        this.knownUrls.delete(row.id);
        if (this.created?.linkId === row.id) this.created = null;
        // Optimistic UI: mark revoked immediately, then refresh from API
        const idx = this.rows.findIndex(r => r.id === row.id);
        if (idx >= 0) {
          this.rows[idx] = {
            ...this.rows[idx],
            isActive: false,
            revokedAt: new Date().toISOString()
          };
          this.rows = [...this.rows];
        }
        this.load();
      },
      error: err => {
        this.revokingId = null;
        this.loadError = apiErrorMessage(err);
      }
    });
  }

  linkStatus(row: ClientTrackingLink): 'active' | 'expired' | 'revoked' {
    if (row.revokedAt) return 'revoked';
    if (row.isActive) return 'active';
    return 'expired';
  }

  tripLabel(row: ClientTrackingLink): string {
    return row.tripNumber?.trim() || `Trip #${row.tripId}`;
  }

  /**
   * Absolute public tracking URL.
   * Prefer create-session map, then list API publicPath when it contains a real token
   * (not the `/t/{token}` placeholder). Never invent tokens.
   */
  displayUrl(row: ClientTrackingLink): string | null {
    const known = this.knownUrls.get(row.id);
    if (known) return known;
    if (this.created?.linkId === row.id && this.created.publicUrl) {
      return this.absoluteUrl(this.created.publicUrl);
    }
    const path = row.publicPath?.trim();
    if (path && this.isOpenablePublicPath(path)) {
      return this.absoluteUrl(path);
    }
    return null;
  }

  /** True when path is a real `/t/...` link, not the secure placeholder. */
  isOpenablePublicPath(path: string | null | undefined): boolean {
    if (!path) return false;
    const p = path.trim();
    if (p.includes('{token}')) return false;
    return /^\/t\/[^/\s?#]+$/i.test(p) || /^https?:\/\/.+/i.test(p);
  }

  createdAbsoluteUrl(): string | null {
    if (!this.created?.publicUrl) return null;
    return this.absoluteUrl(this.created.publicUrl);
  }

  copyUrl(url: string | null | undefined): void {
    if (!url || typeof navigator === 'undefined') {
      this.flashCopy('No shareable URL available for this link.');
      return;
    }
    void navigator.clipboard?.writeText(url).then(
      () => this.flashCopy('Link copied to clipboard.'),
      () => this.flashCopy('Could not copy link.')
    );
  }

  copyCreated(): void {
    this.copyUrl(this.createdAbsoluteUrl());
  }

  openLink(row: ClientTrackingLink, event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    const url = this.displayUrl(row);
    if (!url || typeof window === 'undefined') {
      this.flashCopy('No openable tracking URL for this link.');
      return;
    }
    const opened = window.open(url, '_blank', 'noopener,noreferrer');
    if (!opened) {
      // Popup blocked — fall back to same-tab navigation of the public path
      window.location.assign(url);
    }
  }

  expiresRelative(row: ClientTrackingLink): string {
    const end = new Date(row.expiresAt).getTime();
    if (!Number.isFinite(end)) return '';
    const diffMs = end - Date.now();
    if (diffMs <= 0) return 'expired';
    const hours = Math.round(diffMs / 3_600_000);
    if (hours < 1) return 'in under an hour';
    if (hours < 48) return `in ${hours} hour${hours === 1 ? '' : 's'}`;
    const days = Math.round(hours / 24);
    return `in ${days} day${days === 1 ? '' : 's'}`;
  }

  exportCsv(): void {
    const rows = this.filteredRows;
    if (!rows.length) return;
    const header = ['Id', 'TripId', 'TripNumber', 'Status', 'Views', 'CreatedAt', 'ExpiresAt', 'RevokedAt', 'PublicUrl'];
    const lines = [
      header.join(','),
      ...rows.map(r =>
        [
          r.id,
          r.tripId,
          csvEscape(r.tripNumber ?? ''),
          this.linkStatus(r),
          r.viewCount ?? 0,
          csvEscape(r.createdAt),
          csvEscape(r.expiresAt),
          csvEscape(r.revokedAt ?? ''),
          csvEscape(this.displayUrl(r) ?? '')
        ].join(',')
      )
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `client-tracking-links-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  trackById(_: number, row: ClientTrackingLink): number {
    return row.id;
  }

  private absoluteUrl(publicPath: string): string {
    if (/^https?:\/\//i.test(publicPath)) return publicPath;
    if (typeof window === 'undefined') return publicPath;
    const path = publicPath.startsWith('/') ? publicPath : `/${publicPath}`;
    return `${window.location.origin}${path}`;
  }

  private flashCopy(msg: string): void {
    this.copyMessage = msg;
    if (this.copyTimer) clearTimeout(this.copyTimer);
    this.copyTimer = setTimeout(() => {
      this.copyMessage = null;
      this.copyTimer = null;
    }, 3200);
  }

  private clampPage(): void {
    if (this.pageIndex >= this.totalPages) {
      this.pageIndex = Math.max(0, this.totalPages - 1);
    }
  }
}

function csvEscape(value: string | null | undefined): string {
  const s = value ?? '';
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}
