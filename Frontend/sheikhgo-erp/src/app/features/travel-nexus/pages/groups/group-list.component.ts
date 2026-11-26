import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  GROUP_STATUS_LABEL, GROUP_STATUS_VARIANT, TRAVEL_GROUP_STATUSES, TRAVEL_PERMISSIONS, TravelGroupListItem, TravelGroupStatus,
  TravelGroupSummary, TravelPackageListItem
} from '../../models/travel.models';
import { TravelGroupService } from '../../services/travel-group.service';
import { TravelPackageService } from '../../services/travel-package.service';

@Component({
  selector: 'app-travel-group-list',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent, UiStatusBadgeComponent,
    UiModalComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Umrah Groups" eyebrow="TravelNexus" icon="groups"
        subtitle="Groups from registration through visa, rooming, departure and closure."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Umrah groups' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.groupManage" (click)="openCreate()"><mat-icon>add</mat-icon> New group</button>
      </ui-page-header>

      <section aria-label="Groups by status (all time)">
        <div class="mb-2 flex items-center justify-between">
          <h3 class="tn-section-title">By status</h3>
          <span class="text-xs text-fleet-text-soft">All groups, all time · total {{ summary()?.total ?? '–' }}</span>
        </div>
        <div class="tn-pipeline">
          @for (s of statuses; track s) {
            <button type="button" [class.active]="status() === s" (click)="setStatus(status() === s ? '' : s)">
              <small>{{ statusLabel[s] }}</small><strong>{{ summary()?.counts?.[s] ?? '–' }}</strong>
            </button>
          }
        </div>
      </section>

      <div class="tn-card">
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
          <div class="relative lg:w-72">
            <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
            <input type="search" class="tn-input pl-10" placeholder="Group no., name, package…" aria-label="Search groups"
              [ngModel]="search()" (ngModelChange)="searchInput$.next($event)" />
          </div>
          <label class="tn-field lg:w-40"><span class="text-xs">Departure from</span>
            <input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Departure to</span>
            <input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
          <span class="text-xs text-fleet-text-muted lg:ml-auto">{{ total() }} group(s)</span>
        </div>
        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="reload()">Retry</button></div></div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[900px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Group</th><th class="px-5 py-3">Name</th><th class="px-5 py-3">Package</th>
                  <th class="px-5 py-3">Dates</th><th class="px-5 py-3 w-48">Pilgrims</th><th class="px-5 py-3">Status</th><th class="px-5 py-3">Leader</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> }
                } @else if (!rows().length) {
                  <tr><td colspan="7"><ui-empty-state icon="groups" title="No groups"
                    [description]="hasFilters() ? 'No groups match these filters.' : 'Create a group from a published package.'"></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r.id)">
                      <td class="px-5 py-3 font-mono text-xs">{{ r.groupNumber }}</td>
                      <td class="px-5 py-3 font-semibold">{{ r.name }}</td>
                      <td class="px-5 py-3">{{ r.packageName }}</td>
                      <td class="px-5 py-3 whitespace-nowrap">{{ r.departureDate | date: 'mediumDate': 'UTC' }} → {{ r.returnDate | date: 'mediumDate': 'UTC' }}</td>
                      <td class="px-5 py-3">
                        <div class="flex items-center gap-2" [title]="r.confirmedMembers + ' confirmed of ' + r.activeMembers + ' registered'">
                          <div class="h-2 flex-1 overflow-hidden rounded-full bg-fleet-surface-muted" role="progressbar"
                            [attr.aria-valuenow]="r.activeMembers" aria-valuemin="0" [attr.aria-valuemax]="r.capacity">
                            <div class="h-full rounded-full bg-fleet-primary" [style.width.%]="fill(r)"></div>
                          </div>
                          <span class="text-xs font-semibold">{{ r.activeMembers }}/{{ r.capacity }}</span>
                        </div>
                      </td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [label]="statusLabel[r.status]" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                      <td class="px-5 py-3">{{ r.leaderDisplayName || '—' }}</td>
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

    <ui-modal [(open)]="createOpen" title="New Umrah group" size="sm">
      <div class="space-y-4">
        <div class="tn-field"><label for="g-pkg" class="tn-required">Package</label>
          <select id="g-pkg" class="tn-input" [ngModel]="packageId()" (ngModelChange)="pickPackage($event)">
            <option [ngValue]="null">{{ packages().length ? 'Choose a published package…' : 'No published packages' }}</option>
            @for (p of packages(); track p.id) { <option [ngValue]="p.id">{{ p.code }} · {{ p.name }} ({{ p.durationNights }} nights)</option> }
          </select></div>
        <div class="tn-field"><label for="g-name" class="tn-required">Group name</label>
          <input id="g-name" class="tn-input" maxlength="150" [(ngModel)]="name" placeholder="e.g. Ramadan Umrah — Batch A" /></div>
        <div class="tn-grid">
          <div class="tn-field"><label for="g-dep" class="tn-required">Departure</label><input id="g-dep" type="date" class="tn-input" [(ngModel)]="departure" /></div>
          <div class="tn-field"><label for="g-cap" class="tn-required">Capacity</label><input id="g-cap" type="number" min="1" max="1000" class="tn-input" [(ngModel)]="capacity" /></div>
        </div>
        <p class="text-xs text-fleet-text-muted">The return date and hotel stays are copied from the package; you can adjust them afterwards.</p>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="createOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !packageId() || !name.trim() || !departure || capacity < 1" (click)="create()">Create group</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelGroupListComponent {
  private readonly api = inject(TravelGroupService);
  private readonly packagesApi = inject(TravelPackageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statuses = TRAVEL_GROUP_STATUSES;
  protected readonly statusLabel = GROUP_STATUS_LABEL;
  protected readonly statusVariant = GROUP_STATUS_VARIANT;

  protected readonly rows = signal<TravelGroupListItem[]>([]);
  protected readonly summary = signal<TravelGroupSummary | null>(null);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly page = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelGroupStatus | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchInput$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  protected readonly createOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly packages = signal<TravelPackageListItem[]>([]);
  protected readonly packageId = signal<number | null>(null);
  protected name = '';
  protected departure = '';
  protected capacity = 40;

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getGroups({
        page: this.page(), pageSize: 20, search: this.search(), status: this.status(),
        departureFrom: this.from() || undefined, departureTo: this.to() || undefined
      }).pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load groups.')); return of(null); }))),
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

  protected fill(r: TravelGroupListItem): number { return r.capacity ? Math.min(100, Math.round((r.activeMembers / r.capacity) * 100)) : 0; }
  protected setStatus(s: TravelGroupStatus | ''): void { this.status.set(s); this.reset(); }
  protected reset(): void { this.page.set(1); this.reload(); }
  protected goTo(p: number): void { this.page.set(p); this.reload(); }
  protected reload(): void { this.load$.next(); }
  protected open(id: number): void { this.router.navigate(['/travel/groups', id]); }
  protected hasFilters(): boolean { return !!(this.search() || this.status() || this.from() || this.to()); }

  protected openCreate(): void {
    this.packageId.set(null);
    this.name = '';
    this.departure = '';
    this.capacity = 40;
    this.dialogError.set(null);
    this.createOpen.set(true);
    this.packagesApi.getPackages('Published').pipe(catchError(() => of([] as TravelPackageListItem[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(p => this.packages.set(p));
  }

  protected pickPackage(id: number | null): void {
    this.packageId.set(id);
    const p = this.packages().find(x => x.id === id);
    if (p && !this.name.trim()) this.name = p.name;
  }

  protected create(): void {
    this.busy.set(true);
    this.dialogError.set(null);
    this.api.create({ packageId: this.packageId()!, name: this.name.trim(), departureDate: this.departure, capacity: Number(this.capacity), branchId: null, notes: null })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: id => { this.busy.set(false); this.createOpen.set(false); this.open(id); },
        error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not create the group.')); }
      });
  }
}
