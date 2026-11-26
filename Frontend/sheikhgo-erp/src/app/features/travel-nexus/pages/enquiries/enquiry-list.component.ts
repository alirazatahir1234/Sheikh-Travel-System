import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { statusFromQuery } from '../../models/travel-date.util';
import {
  ENQUIRY_STATUS_LABEL, ENQUIRY_STATUS_VARIANT, TRAVEL_ENQUIRY_STATUSES, TRAVEL_PERMISSIONS, TRAVEL_SERVICE_TYPES,
  TravelEnquiryListItem, TravelEnquiryStatus, TravelEnquirySummary, TravelServiceType
} from '../../models/travel.models';
import { asUtc, isDue } from '../../models/travel-date.util';
import { TravelEnquiryService } from '../../services/travel-enquiry.service';

@Component({
  selector: 'app-travel-enquiry-list',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent,
    UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="CRM & Enquiries" eyebrow="TravelNexus" icon="contact_phone"
        subtitle="Leads, follow-ups and conversions."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Enquiries' }]">
        <a mat-flat-button color="primary" routerLink="/travel/enquiries/new" *appHasPermission="perms.enquiryManage">
          <mat-icon>add</mat-icon> New enquiry
        </a>
      </ui-page-header>

      <!-- Current counts per status (server summary, not computed from this page). -->
      <div class="flex flex-wrap gap-2" role="group" aria-label="Filter by status">
        <button type="button" class="tn-chip" [class.active]="!status() && !followUpDue()" (click)="setStatus('')">
          All <span>{{ summary()?.total ?? '–' }}</span>
        </button>
        @for (s of statuses; track s) {
          <button type="button" class="tn-chip" [class.active]="status() === s" (click)="setStatus(s)">
            {{ statusLabel[s] }} <span>{{ summary()?.counts?.[s] ?? '–' }}</span>
          </button>
        }
        <button type="button" class="tn-chip warn" [class.active]="followUpDue()" (click)="toggleDue()">
          <mat-icon class="!text-[16px]">alarm</mat-icon> Follow-ups due <span>{{ summary()?.followUpsDue ?? '–' }}</span>
        </button>
        <span class="self-center text-xs text-fleet-text-soft">Counts: current, all time</span>
      </div>

      <div class="tn-card">
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
          <div class="relative lg:w-72">
            <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
            <input type="search" class="tn-input pl-10" placeholder="Number, name or phone…" aria-label="Search enquiries"
              [ngModel]="search()" (ngModelChange)="searchInput$.next($event)" />
          </div>
          <select class="tn-input lg:w-40" aria-label="Service" [ngModel]="service()" (ngModelChange)="service.set($event); reset()">
            <option value="">All services</option>
            @for (s of services; track s) { <option [value]="s">{{ s }}</option> }
          </select>
          <label class="tn-field lg:w-40"><span class="text-xs">Created from</span>
            <input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Created to</span>
            <input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
          <span class="text-sm text-fleet-text-muted lg:ml-auto">{{ total() }} shown</span>
        </div>

        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="reload()">Retry</button></div></div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[860px] text-left">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Enquiry</th><th class="px-5 py-3">Lead / customer</th><th class="px-5 py-3">Service</th>
                  <th class="px-5 py-3">Status</th><th class="px-5 py-3">Agent</th><th class="px-5 py-3">Next follow-up</th>
                  <th class="px-5 py-3">Created</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border text-sm">
                @if (loading()) {
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr>
                  }
                } @else if (!rows().length) {
                  <tr><td colspan="7"><ui-empty-state icon="contact_phone" title="No enquiries"
                    [description]="hasFilters() ? 'No enquiries match these filters.' : 'Log your first walk-in or phone enquiry.'"></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                      <td class="px-5 py-3 font-mono text-xs">{{ r.enquiryNumber }}
                        @if (r.priority === 'High') { <mat-icon class="!text-[16px] align-middle text-red-500" title="High priority">priority_high</mat-icon> }</td>
                      <td class="px-5 py-3"><div class="font-semibold">{{ r.displayName }}</div>
                        <div class="text-xs text-fleet-text-muted">{{ r.displayPhone }}{{ r.customerId ? '' : ' · lead' }}</div></td>
                      <td class="px-5 py-3">{{ r.serviceType }}{{ r.destinationCountryName ? ' · ' + r.destinationCountryName : '' }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [label]="statusLabel[r.status]" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                      <td class="px-5 py-3">{{ r.assignedAgentName || '—' }}</td>
                      <td class="px-5 py-3" [class.text-red-600]="due(r)" [class.font-semibold]="due(r)">
                        {{ r.nextFollowUpAt ? (utc(r.nextFollowUpAt) | date: 'MMM d, h:mm a') : '—' }}</td>
                      <td class="px-5 py-3 text-fleet-text-muted">{{ utc(r.createdAt) | date: 'mediumDate' }}</td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
          @if (!loading() && totalPages() > 1) {
            <div class="flex items-center justify-between border-t border-fleet-border px-5 py-3 text-sm">
              <span class="text-fleet-text-muted">Page {{ page() }} of {{ totalPages() }}</span>
              <div class="flex gap-2">
                <button mat-stroked-button [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
                <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelEnquiryListComponent {
  private readonly api = inject(TravelEnquiryService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statuses = TRAVEL_ENQUIRY_STATUSES;
  protected readonly services = TRAVEL_SERVICE_TYPES;
  protected readonly statusLabel = ENQUIRY_STATUS_LABEL;
  protected readonly statusVariant = ENQUIRY_STATUS_VARIANT;
  protected readonly utc = asUtc;

  protected readonly rows = signal<TravelEnquiryListItem[]>([]);
  protected readonly summary = signal<TravelEnquirySummary | null>(null);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly page = signal(1);
  protected readonly search = signal('');
  /** `?status=` opens the list already filtered (dashboard pipeline tiles). */
  protected readonly status = signal<TravelEnquiryStatus | ''>(statusFromQuery(inject(ActivatedRoute).snapshot.queryParamMap.get('status'), TRAVEL_ENQUIRY_STATUSES));
  protected readonly service = signal<TravelServiceType | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  /** `?followUpDue=true` opens the list already filtered (dashboard deep link). */
  protected readonly followUpDue = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('followUpDue') === 'true');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly searchInput$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getEnquiries({
        page: this.page(), pageSize: 20, search: this.search(), status: this.status(), serviceType: this.service(),
        createdFrom: this.from() || undefined, createdTo: this.to() || undefined, followUpDue: this.followUpDue()
      }).pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load enquiries.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => {
      if (r) { this.rows.set(r.items); this.total.set(r.totalCount); this.totalPages.set(r.totalPages); }
      this.loading.set(false);
    });

    this.searchInput$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(term => { this.search.set(term ?? ''); this.reset(); });

    this.api.getSummary().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(s => this.summary.set(s));
    this.reload();
  }

  protected setStatus(s: TravelEnquiryStatus | ''): void { this.status.set(s); this.followUpDue.set(false); this.reset(); }
  protected toggleDue(): void { this.followUpDue.set(!this.followUpDue()); this.status.set(''); this.reset(); }
  protected reset(): void { this.page.set(1); this.reload(); }
  protected goTo(p: number): void { this.page.set(p); this.reload(); }
  protected reload(): void { this.load$.next(); }
  protected open(r: TravelEnquiryListItem): void { this.router.navigate(['/travel/enquiries', r.id]); }
  protected due(r: TravelEnquiryListItem): boolean { return isDue(r.nextFollowUpAt) && r.status !== 'Won' && r.status !== 'Lost'; }
  protected hasFilters(): boolean { return !!(this.search() || this.status() || this.service() || this.from() || this.to() || this.followUpDue()); }
}
