import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, tap, catchError, of } from 'rxjs';
import { UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  TRAVEL_CUSTOMER_KINDS, TRAVEL_PERMISSIONS, TravelCustomerKind, TravelCustomerListItem, TravelCustomerQuery
} from '../../models/travel.models';
import { TravelCustomerService } from '../../services/travel-customer.service';

@Component({
  selector: 'app-travel-customer-list',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule,
    UiPageHeaderComponent, UiEmptyStateComponent, UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Customers" eyebrow="TravelNexus" icon="groups"
        subtitle="Individuals and companies who book travel, visas and Umrah with you."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Customers' }]">
        <a mat-flat-button color="primary" routerLink="/travel/customers/new" *appHasPermission="perms.customerManage">
          <mat-icon>person_add</mat-icon> New customer
        </a>
      </ui-page-header>

      <div class="tn-card">
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 sm:flex-row sm:items-center">
          <div class="relative sm:w-80">
            <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
            <input type="search" class="tn-input pl-10" placeholder="Search name, phone, email…"
              [ngModel]="search()" (ngModelChange)="onSearch($event)" aria-label="Search customers" />
          </div>
          <select class="tn-input sm:w-48" [ngModel]="kind()" (ngModelChange)="onKind($event)" aria-label="Customer type">
            <option value="">All types</option>
            @for (k of kinds; track k) { <option [value]="k">{{ k }}</option> }
          </select>
          <span class="text-sm text-fleet-text-muted sm:ml-auto">{{ total() }} customer{{ total() === 1 ? '' : 's' }}</span>
        </div>

        @if (error()) {
          <div class="flex items-center gap-3 px-5 py-6 text-sm text-red-700">
            <mat-icon>error_outline</mat-icon> {{ error() }}
            <button mat-stroked-button (click)="reload()">Retry</button>
          </div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-left">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Customer</th>
                  <th class="px-5 py-3">Type</th>
                  <th class="px-5 py-3">Phone</th>
                  <th class="px-5 py-3">City</th>
                  <th class="px-5 py-3">Agent</th>
                  <th class="px-5 py-3 text-center">Travelers</th>
                  <th class="px-5 py-3">Next passport expiry</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr>@for (c of [1,2,3,4,5,6,7]; track c) {
                      <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td>
                    }</tr>
                  }
                } @else if (!rows().length) {
                  <tr><td colspan="7">
                    <ui-empty-state icon="groups" title="No travel customers yet"
                      [description]="search() || kind() ? 'No customers match your filters.' : 'Add your first customer to start creating enquiries and visa cases.'">
                    </ui-empty-state>
                  </td></tr>
                } @else {
                  @for (row of rows(); track row.customerId) {
                    <tr class="cursor-pointer transition-colors hover:bg-fleet-surface-alt/40" (click)="open(row)">
                      <td class="px-5 py-4">
                        <div class="font-semibold text-fleet-text">{{ row.fullName }}</div>
                        @if (row.contactPerson) { <div class="text-xs text-fleet-text-muted">{{ row.contactPerson }}</div> }
                      </td>
                      <td class="px-5 py-4"><ui-status-badge [status]="row.customerKind" [variant]="row.customerKind === 'Corporate' ? 'info' : 'active'"></ui-status-badge></td>
                      <td class="px-5 py-4 text-sm">{{ row.phone }}</td>
                      <td class="px-5 py-4 text-sm">{{ row.city || '—' }}</td>
                      <td class="px-5 py-4 text-sm">{{ row.assignedAgentName || '—' }}</td>
                      <td class="px-5 py-4 text-center text-sm font-semibold">{{ row.travelerCount }}</td>
                      <td class="px-5 py-4 text-sm">{{ row.nextPassportExpiry ? (row.nextPassportExpiry | date: 'mediumDate': 'UTC') : '—' }}</td>
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
export class TravelCustomerListComponent {
  private readonly service = inject(TravelCustomerService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly kinds = TRAVEL_CUSTOMER_KINDS;
  private readonly pageSize = 20;

  protected readonly rows = signal<TravelCustomerListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly page = signal(1);
  protected readonly search = signal('');
  protected readonly kind = signal<TravelCustomerKind | ''>('');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  private readonly query$ = new Subject<void>();
  private readonly searchInput$ = new Subject<string>();

  constructor() {
    this.query$
      .pipe(
        tap(() => { this.loading.set(true); this.error.set(null); }),
        switchMap(() => {
          const q: TravelCustomerQuery = { page: this.page(), pageSize: this.pageSize, search: this.search(), customerKind: this.kind() };
          return this.service.getCustomers(q).pipe(
            catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load customers.')); return of(null); })
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(result => {
        if (result) {
          this.rows.set(result.items);
          this.total.set(result.totalCount);
          this.totalPages.set(result.totalPages);
        }
        this.loading.set(false);
      });

    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(term => { this.search.set(term); this.page.set(1); this.reload(); });

    this.reload();
  }

  protected onSearch(term: string): void { this.searchInput$.next(term ?? ''); }
  protected onKind(kind: TravelCustomerKind | ''): void { this.kind.set(kind); this.page.set(1); this.reload(); }
  protected goTo(page: number): void { this.page.set(page); this.reload(); }
  protected reload(): void { this.query$.next(); }
  protected open(row: TravelCustomerListItem): void { this.router.navigate(['/travel/customers', row.customerId]); }
}
