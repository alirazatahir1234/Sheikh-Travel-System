import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  TRAVEL_PERMISSIONS, TravelVisaCaseListItem, TravelVisaCaseStatus, TravelVisaCaseSummary, VISA_PIPELINE_STAGES,
  VISA_STATUS_LABEL, VISA_STATUS_VARIANT
} from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelVisaService } from '../../services/travel-visa.service';
import { TravelDocumentService } from '../../services/travel-document.service';
import { ActivatedRoute } from '@angular/router';
import { TravelMissingDocumentsItem } from '../../models/travel.models';

@Component({
  selector: 'app-travel-visa-case-list',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent,
    UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Visa Management" eyebrow="TravelNexus" icon="assignment_ind"
        subtitle="Visa cases from document collection to embassy decision."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Visa cases' }]">
        <a mat-flat-button color="primary" routerLink="/travel/visa-cases/new" *appHasPermission="perms.visaManage">
          <mat-icon>add</mat-icon> New visa case
        </a>
      </ui-page-header>

      <section aria-label="Visa case pipeline (current, all time)">
        <div class="mb-2 flex items-center justify-between">
          <h3 class="tn-section-title">Pipeline</h3>
          <span class="text-xs text-fleet-text-soft">Current cases, all time · total {{ summary()?.total ?? '–' }}</span>
        </div>
        <div class="tn-pipeline">
          @for (s of stages; track s) {
            <button type="button" [class.active]="status() === s" (click)="setStatus(status() === s ? '' : s)">
              <small>{{ statusLabel[s] }}</small>
              <strong>{{ summary()?.counts?.[s] ?? '–' }}</strong>
            </button>
          }
        </div>
      </section>

      <div class="tn-card">
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
          <div class="relative lg:w-72">
            <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
            <input type="search" class="tn-input pl-10" placeholder="Case no., customer, reference…" aria-label="Search visa cases"
              [ngModel]="search()" (ngModelChange)="searchInput$.next($event)" />
          </div>
          <select class="tn-input lg:w-52" aria-label="Country" [ngModel]="country()" (ngModelChange)="country.set($event); reset()">
            <option value="">All countries</option>
            @for (c of countries(); track c.code) { <option [value]="c.code">{{ c.name }}</option> }
          </select>
          <label class="tn-field lg:w-40"><span class="text-xs">Created from</span>
            <input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Created to</span>
            <input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
          <button mat-stroked-button [color]="missingDocs() ? 'primary' : undefined" (click)="toggleMissing()">
            <mat-icon>{{ missingDocs() ? 'check_box' : 'check_box_outline_blank' }}</mat-icon> Missing documents
          </button>
          @if (status(); as s) { <button mat-stroked-button (click)="setStatus('')"><mat-icon>close</mat-icon> {{ statusLabel[s] }}</button> }
          <span class="text-sm text-fleet-text-muted lg:ml-auto">{{ total() }} shown</span>
        </div>

        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="reload()">Retry</button></div></div>
        } @else if (missingDocs()) {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Case</th><th class="px-5 py-3">Customer</th><th class="px-5 py-3">Visa</th>
                  <th class="px-5 py-3">Status</th><th class="px-5 py-3 text-center">Outstanding</th><th class="px-5 py-3">Travel</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> }
                } @else if (!missingRows().length) {
                  <tr><td colspan="6"><ui-empty-state icon="task_alt" title="Nothing outstanding" description="No open case is waiting for mandatory documents."></ui-empty-state></td></tr>
                } @else {
                  @for (m of missingRows(); track m.visaCaseId) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="openCase(m.visaCaseId)">
                      <td class="px-5 py-3 font-mono text-xs">{{ m.caseNumber }}</td>
                      <td class="px-5 py-3 font-semibold">{{ m.customerName }}</td>
                      <td class="px-5 py-3">{{ m.countryName }} · {{ m.visaTypeName }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="m.status" [label]="statusLabel[m.status]" [variant]="statusVariant[m.status]"></ui-status-badge></td>
                      <td class="px-5 py-3 text-center font-semibold text-red-600">{{ m.mandatoryOutstanding }}</td>
                      <td class="px-5 py-3">{{ m.intendedTravelDate ? (m.intendedTravelDate | date: 'mediumDate': 'UTC') : '—' }}</td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[900px] text-left">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Case</th><th class="px-5 py-3">Customer</th><th class="px-5 py-3">Visa</th>
                  <th class="px-5 py-3">Status</th><th class="px-5 py-3 text-center">Applicants</th>
                  <th class="px-5 py-3">Travel</th><th class="px-5 py-3">Agent</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border text-sm">
                @if (loading()) {
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr>
                  }
                } @else if (!rows().length) {
                  <tr><td colspan="7"><ui-empty-state icon="assignment_ind" title="No visa cases"
                    [description]="hasFilters() ? 'No cases match these filters.' : 'Open a case from a customer or a converted enquiry.'"></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                      <td class="px-5 py-3 font-mono text-xs">{{ r.caseNumber }}
                        @if (r.priority === 'High') { <mat-icon class="!text-[16px] align-middle text-red-500" title="High priority">priority_high</mat-icon> }</td>
                      <td class="px-5 py-3 font-semibold">{{ r.customerName }}</td>
                      <td class="px-5 py-3">{{ r.countryName }} · {{ r.visaTypeName }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [label]="statusLabel[r.status]" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                      <td class="px-5 py-3 text-center">{{ r.applicantCount }}@if (r.approvedCount) { <span class="text-xs text-emerald-600"> ({{ r.approvedCount }} ✓)</span> }</td>
                      <td class="px-5 py-3">{{ r.intendedTravelDate ? (r.intendedTravelDate | date: 'mediumDate': 'UTC') : '—' }}</td>
                      <td class="px-5 py-3">{{ r.assignedAgentName || '—' }}</td>
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
export class TravelVisaCaseListComponent {
  private readonly api = inject(TravelVisaService);
  private readonly config = inject(TravelConfigService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly stages = VISA_PIPELINE_STAGES;
  protected readonly statusLabel = VISA_STATUS_LABEL;
  protected readonly statusVariant = VISA_STATUS_VARIANT;
  protected readonly utc = asUtc;
  protected readonly countries = toSignal(this.config.countries$.pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly rows = signal<TravelVisaCaseListItem[]>([]);
  protected readonly summary = signal<TravelVisaCaseSummary | null>(null);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly page = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelVisaCaseStatus | ''>('');
  protected readonly country = signal('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  private readonly documentsApi = inject(TravelDocumentService);
  protected readonly missingDocs = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('missingDocs') === 'true');
  protected readonly missingRows = signal<TravelMissingDocumentsItem[]>([]);

  protected readonly searchInput$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.missingDocs()
        ? this.documentsApi.getMissing(this.page(), 20).pipe(
            tap(r => this.missingRows.set(r.items)),
            catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load cases with missing documents.')); return of(null); }))
        : this.api.getCases({
        page: this.page(), pageSize: 20, search: this.search(), status: this.status(), countryCode: this.country() || undefined,
        createdFrom: this.from() || undefined, createdTo: this.to() || undefined
      }).pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load visa cases.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => {
      if (r) {
        if (!this.missingDocs()) this.rows.set(r.items as TravelVisaCaseListItem[]);
        this.total.set(r.totalCount);
        this.totalPages.set(r.totalPages);
      }
      this.loading.set(false);
    });

    this.searchInput$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(term => { this.search.set(term ?? ''); this.reset(); });

    this.api.getSummary().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(s => this.summary.set(s));
    this.reload();
  }

  protected setStatus(s: TravelVisaCaseStatus | ''): void { this.status.set(s); this.missingDocs.set(false); this.reset(); }
  protected toggleMissing(): void { this.missingDocs.set(!this.missingDocs()); this.reset(); }
  protected openCase(id: number): void { this.router.navigate(['/travel/visa-cases', id]); }
  protected reset(): void { this.page.set(1); this.reload(); }
  protected goTo(p: number): void { this.page.set(p); this.reload(); }
  protected reload(): void { this.load$.next(); }
  protected open(r: TravelVisaCaseListItem): void { this.router.navigate(['/travel/visa-cases', r.id]); }
  protected hasFilters(): boolean { return !!(this.search() || this.status() || this.country() || this.from() || this.to()); }
}
