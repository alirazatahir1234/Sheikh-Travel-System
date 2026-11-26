import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import {
  UiConfirmService, UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelerFormDrawerComponent } from '../../components/traveler-form-drawer.component';
import {
  ENQUIRY_STATUS_LABEL, ENQUIRY_STATUS_VARIANT, PASSPORT_STATUS_LABEL, PASSPORT_STATUS_VARIANT, TRAVEL_PERMISSIONS, TravelCustomer,
  TravelEnquiryListItem, TravelGroupListItem, TravelTraveler, TravelVisaCaseListItem, VISA_STATUS_LABEL, VISA_STATUS_VARIANT,
  GROUP_STATUS_LABEL, GROUP_STATUS_VARIANT
} from '../../models/travel.models';
import { TravelEnquiryService } from '../../services/travel-enquiry.service';
import { TravelVisaService } from '../../services/travel-visa.service';
import { TravelGroupService } from '../../services/travel-group.service';
import { AuthService } from '../../../../core/services/auth.service';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelCustomerService } from '../../services/travel-customer.service';

@Component({
  selector: 'app-travel-customer-detail',
  standalone: true,
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent,
    UiEmptyStateComponent, HasPermissionDirective, TravelerFormDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
          <a mat-stroked-button routerLink="/travel/customers" class="ml-auto">Back to customers</a></div>
      } @else if (!customer()) {
        <div class="tn-card tn-card-body space-y-3">
          @for (s of [1,2,3]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }
        </div>
      } @else {
        @let c = customer()!;
        <ui-page-header [title]="c.fullName" eyebrow="TravelNexus · Customer" [icon]="c.customerKind === 'Corporate' ? 'business' : 'person'"
          [subtitle]="c.customerKind + (c.city ? ' · ' + c.city : '')"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Customers', route: '/travel/customers' }, { label: c.fullName }]">
          <ng-container *appHasPermission="perms.customerManage">
            <button mat-stroked-button color="warn" (click)="archive(c)"><mat-icon>archive</mat-icon> Archive</button>
            <a mat-flat-button color="primary" [routerLink]="['/travel/customers', c.customerId, 'edit']"><mat-icon>edit</mat-icon> Edit</a>
          </ng-container>
        </ui-page-header>

        <div class="tn-card tn-card-body">
          <dl class="tn-kv">
            <div><dt>Phone</dt><dd>{{ c.phone }}</dd></div>
            @if (c.alternatePhone) { <div><dt>Alternate phone</dt><dd>{{ c.alternatePhone }}</dd></div> }
            <div><dt>Email</dt><dd>{{ c.email || '—' }}</dd></div>
            @if (c.customerKind === 'Corporate') {
              <div><dt>Contact person</dt><dd>{{ c.contactPerson || '—' }}</dd></div>
              <div><dt>NTN / tax number</dt><dd>{{ c.taxNumber || '—' }}</dd></div>
            } @else {
              <div><dt>CNIC</dt><dd>{{ c.cnic || '—' }}</dd></div>
            }
            <div><dt>Address</dt><dd>{{ c.address || '—' }}</dd></div>
            <div><dt>Assigned agent</dt><dd>{{ c.assignedAgentName || 'Unassigned' }}</dd></div>
            <div><dt>Branch</dt><dd>{{ c.branchName || '—' }}</dd></div>
            <div><dt>Lead source</dt><dd>{{ c.leadSourceName || '—' }}</dd></div>
            <div><dt>Customer since</dt><dd>{{ c.createdAt | date: 'mediumDate' }}</dd></div>
          </dl>
          @if (c.notes) { <p class="mt-4 whitespace-pre-line text-sm text-fleet-text-muted">{{ c.notes }}</p> }
        </div>

        <div class="tn-card">
          <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
            <div>
              <h3 class="tn-section-title">Travelers</h3>
              <p class="text-xs text-fleet-text-muted">Passport holders travelling under this customer.</p>
            </div>
            <button mat-flat-button color="primary" *appHasPermission="perms.customerManage" (click)="addTraveler()">
              <mat-icon>add</mat-icon> Add traveler
            </button>
          </div>

          @if (travelersError()) {
            <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ travelersError() }}
              <button mat-stroked-button class="ml-auto" (click)="loadTravelers()">Retry</button></div></div>
          } @else if (travelersLoading()) {
            <div class="tn-card-body space-y-2">@for (s of [1,2]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
          } @else if (!travelers().length) {
            <ui-empty-state icon="badge" title="No travelers yet" description="Add the passport holders who will travel."></ui-empty-state>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full min-w-[820px] text-left">
                <thead class="bg-fleet-surface-alt/60">
                  <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="px-5 py-3">Name</th><th class="px-5 py-3">Gender</th><th class="px-5 py-3">Date of birth</th>
                    <th class="px-5 py-3">Passport</th><th class="px-5 py-3">Expiry</th><th class="px-5 py-3">Status</th>
                    <th class="px-5 py-3">Mahram</th><th class="px-5 py-3 text-right" *appHasPermission="perms.customerManage">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-fleet-border text-sm">
                  @for (t of travelers(); track t.id) {
                    <tr>
                      <td class="px-5 py-3 font-semibold">{{ t.fullName }}</td>
                      <td class="px-5 py-3">{{ t.gender }}</td>
                      <td class="px-5 py-3">{{ t.dateOfBirth ? (t.dateOfBirth | date: 'mediumDate': 'UTC') : '—' }}</td>
                      <td class="px-5 py-3 font-mono">{{ t.passportNumber || '—' }}
                        @if (t.passportIssuingCountryCode) { <span class="text-xs text-fleet-text-muted">({{ t.passportIssuingCountryCode }})</span> }</td>
                      <td class="px-5 py-3">{{ t.passportExpiryDate ? (t.passportExpiryDate | date: 'mediumDate': 'UTC') : '—' }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="t.passportStatus" [label]="statusLabel[t.passportStatus]" [variant]="statusVariant[t.passportStatus]"></ui-status-badge></td>
                      <td class="px-5 py-3">{{ t.mahramName ? t.mahramName + ' (' + t.mahramRelation + ')' : '—' }}</td>
                      <td class="px-5 py-3 text-right" *appHasPermission="perms.customerManage">
                        <button mat-icon-button aria-label="Edit traveler" (click)="editTraveler(t)"><mat-icon>edit</mat-icon></button>
                        <button mat-icon-button aria-label="Remove traveler" (click)="removeTraveler(t)"><mat-icon>delete_outline</mat-icon></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>

        <div class="grid gap-5 lg:grid-cols-2">
          <div class="tn-card" *appHasPermission="perms.enquiryView">
            <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
              <h3 class="tn-section-title">Enquiries</h3>
              <a mat-stroked-button *appHasPermission="perms.enquiryManage" routerLink="/travel/enquiries/new"><mat-icon>add</mat-icon> New</a>
            </div>
            @if (!enquiries().length) {
              <p class="px-5 py-6 text-sm text-fleet-text-muted">No enquiries for this customer.</p>
            } @else {
              <ul class="divide-y divide-fleet-border">
                @for (q of enquiries(); track q.id) {
                  <li><a class="flex items-center justify-between px-5 py-3 text-sm hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/enquiries', q.id]">
                    <span><span class="font-mono text-xs">{{ q.enquiryNumber }}</span> · {{ q.serviceType }}{{ q.destinationCountryName ? ' · ' + q.destinationCountryName : '' }}</span>
                    <ui-status-badge [status]="q.status" [label]="enquiryLabel[q.status]" [variant]="enquiryVariant[q.status]"></ui-status-badge></a></li>
                }
              </ul>
            }
          </div>
          <div class="tn-card" *appHasPermission="perms.visaView">
            <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
              <h3 class="tn-section-title">Visa cases</h3>
              <a mat-stroked-button *appHasPermission="perms.visaManage" routerLink="/travel/visa-cases/new" [queryParams]="{ customerId: c.customerId }"><mat-icon>add</mat-icon> New</a>
            </div>
            @if (!visaCases().length) {
              <p class="px-5 py-6 text-sm text-fleet-text-muted">No visa cases for this customer.</p>
            } @else {
              <ul class="divide-y divide-fleet-border">
                @for (v of visaCases(); track v.id) {
                  <li><a class="flex items-center justify-between px-5 py-3 text-sm hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/visa-cases', v.id]">
                    <span><span class="font-mono text-xs">{{ v.caseNumber }}</span> · {{ v.countryName }} · {{ v.visaTypeName }} · {{ v.applicantCount }} applicant(s)</span>
                    <ui-status-badge [status]="v.status" [label]="visaLabel[v.status]" [variant]="visaVariant[v.status]"></ui-status-badge></a></li>
                }
              </ul>
            }
          </div>
          <div class="tn-card" *appHasPermission="perms.groupView">
            <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Umrah groups</h3></div>
            @if (!groups().length) {
              <p class="px-5 py-6 text-sm text-fleet-text-muted">This customer has no pilgrims in a group.</p>
            } @else {
              <ul class="divide-y divide-fleet-border">
                @for (g of groups(); track g.id) {
                  <li><a class="flex items-center justify-between px-5 py-3 text-sm hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/groups', g.id]">
                    <span><span class="font-mono text-xs">{{ g.groupNumber }}</span> · {{ g.name }} · {{ g.departureDate | date: 'mediumDate': 'UTC' }}</span>
                    <ui-status-badge [status]="g.status" [label]="groupLabel[g.status]" [variant]="groupVariant[g.status]"></ui-status-badge></a></li>
                }
              </ul>
            }
          </div>
        </div>

        <app-traveler-form-drawer [(open)]="drawerOpen" [customerId]="c.customerId" [traveler]="editing()"
          [siblings]="travelers()" [countries]="countries()" (saved)="onTravelerSaved()"></app-traveler-form-drawer>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelCustomerDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(TravelCustomerService);
  private readonly config = inject(TravelConfigService);
  private readonly confirm = inject(UiConfirmService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusLabel = PASSPORT_STATUS_LABEL;
  protected readonly statusVariant = PASSPORT_STATUS_VARIANT;
  protected readonly enquiryLabel = ENQUIRY_STATUS_LABEL;
  protected readonly enquiryVariant = ENQUIRY_STATUS_VARIANT;
  protected readonly visaLabel = VISA_STATUS_LABEL;
  protected readonly visaVariant = VISA_STATUS_VARIANT;
  private readonly enquiryApi = inject(TravelEnquiryService);
  private readonly visaApi = inject(TravelVisaService);
  private readonly auth = inject(AuthService);
  protected readonly enquiries = signal<TravelEnquiryListItem[]>([]);
  protected readonly visaCases = signal<TravelVisaCaseListItem[]>([]);
  private readonly groupApi = inject(TravelGroupService);
  protected readonly groups = signal<TravelGroupListItem[]>([]);
  protected readonly groupLabel = GROUP_STATUS_LABEL;
  protected readonly groupVariant = GROUP_STATUS_VARIANT;
  private readonly customerId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly customer = signal<TravelCustomer | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly travelers = signal<TravelTraveler[]>([]);
  protected readonly travelersLoading = signal(true);
  protected readonly travelersError = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editing = signal<TravelTraveler | null>(null);
  protected readonly countries = toSignal(this.config.countries$.pipe(catchError(() => of([]))), { initialValue: [] });

  constructor() {
    this.loadCustomer();
    this.loadTravelers();
    // Related records are optional panels: only fetched with the matching View permission; failures leave them empty.
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.enquiryView)) {
      this.enquiryApi.getByCustomer(this.customerId).pipe(catchError(() => of([])), takeUntilDestroyed(this.destroyRef))
        .subscribe(r => this.enquiries.set(r));
    }
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.visaView)) {
      this.visaApi.getByCustomer(this.customerId).pipe(catchError(() => of([])), takeUntilDestroyed(this.destroyRef))
        .subscribe(r => this.visaCases.set(r));
    }
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.groupView)) {
      this.groupApi.getByCustomer(this.customerId).pipe(catchError(() => of([])), takeUntilDestroyed(this.destroyRef))
        .subscribe(r => this.groups.set(r));
    }
  }

  protected loadTravelers(): void {
    this.travelersLoading.set(true);
    this.travelersError.set(null);
    this.service.getTravelers(this.customerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.travelers.set(rows); this.travelersLoading.set(false); },
      error: err => { this.travelersError.set(apiErrorMessage(err, 'Could not load travelers.')); this.travelersLoading.set(false); }
    });
  }

  protected addTraveler(): void { this.editing.set(null); this.drawerOpen.set(true); }
  protected editTraveler(t: TravelTraveler): void { this.editing.set(t); this.drawerOpen.set(true); }

  protected onTravelerSaved(): void {
    this.loadTravelers();
    this.loadCustomer();
  }

  protected async removeTraveler(t: TravelTraveler): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Remove ${t.fullName} from this customer?`, 'Remove traveler'))) return;
    this.service.deleteTraveler(t.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Traveler removed.'); this.loadTravelers(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not remove the traveler.'))
    });
  }

  protected async archive(c: TravelCustomer): Promise<void> {
    const ok = await this.confirm.open({
      variant: 'warning',
      title: 'Archive customer',
      message: `Archive ${c.fullName} from TravelNexus? The customer record stays in Nexus.`,
      confirmText: 'Archive'
    });
    if (!ok) return;
    this.service.archiveCustomer(c.customerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Customer archived.'); this.router.navigate(['/travel/customers']); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not archive the customer.'))
    });
  }

  private loadCustomer(): void {
    this.service.getCustomer(this.customerId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: c => this.customer.set(c),
      error: err => this.error.set(apiErrorMessage(err, 'Customer not found.'))
    });
  }
}
