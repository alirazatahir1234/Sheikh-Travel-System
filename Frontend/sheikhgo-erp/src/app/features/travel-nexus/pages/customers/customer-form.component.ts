import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  CreateTravelCustomer, SaveTravelCustomer, TravelCustomer, TravelCustomerKind, TravelLeadSource, TravelLookups, TravelSubAgent, isMasked
} from '../../models/travel.models';
import { TravelCommissionService } from '../../services/travel-commission.service';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelCustomerService } from '../../services/travel-customer.service';

const PHONE_PATTERN = /^\+?[0-9 \-()]{7,30}$/;
const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

@Component({
  selector: 'app-travel-customer-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="isEdit() ? 'Edit customer' : 'New customer'" eyebrow="TravelNexus" icon="person_add"
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Customers', route: '/travel/customers' }, { label: isEdit() ? 'Edit' : 'New' }]">
      </ui-page-header>

      @if (loadError()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div>
      } @else if (loading()) {
        <div class="tn-card tn-card-body space-y-3">
          @for (s of [1,2,3]; track s) { <div class="h-10 animate-pulse rounded bg-fleet-surface-muted"></div> }
        </div>
      } @else {
        <form class="tn-card tn-card-body space-y-6" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <div class="tn-segment" role="radiogroup" aria-label="Customer type">
            @for (k of ['Individual', 'Corporate']; track k) {
              <button type="button" role="radio" [attr.aria-checked]="kind() === k" [class.active]="kind() === k"
                (click)="setKind($any(k))">{{ k }}</button>
            }
          </div>

          <section class="space-y-3">
            <h3 class="tn-section-title">{{ kind() === 'Corporate' ? 'Company' : 'Customer' }}</h3>
            <div class="tn-grid">
              <div class="tn-field">
                <label class="tn-required" for="fullName">{{ kind() === 'Corporate' ? 'Company name' : 'Full name' }}</label>
                <input id="fullName" class="tn-input" formControlName="fullName" maxlength="100" />
                @if (invalid('fullName')) { <span class="tn-error">Required (max 100 characters).</span> }
              </div>
              @if (kind() === 'Corporate') {
                <div class="tn-field">
                  <label for="contactPerson">Contact person</label>
                  <input id="contactPerson" class="tn-input" formControlName="contactPerson" maxlength="150" />
                </div>
                <div class="tn-field">
                  <label for="taxNumber">NTN / tax number</label>
                  <input id="taxNumber" class="tn-input" formControlName="taxNumber" maxlength="50" />
                </div>
              } @else {
                <div class="tn-field">
                  <label for="cnic">CNIC</label>
                  <input id="cnic" class="tn-input" formControlName="cnic" maxlength="15" autocomplete="off"
                    [placeholder]="cnicMasked() ? storedCnic() : '35202-1234567-1'" />
                  @if (cnicMasked()) { <span class="tn-hint">Stored CNIC is hidden. Leave blank to keep it.</span> }
                </div>
              }
            </div>
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Contact</h3>
            <div class="tn-grid">
              <div class="tn-field">
                <label class="tn-required" for="phone">Phone</label>
                <input id="phone" class="tn-input" formControlName="phone" type="tel" maxlength="30" placeholder="+92 300 1234567" />
                @if (invalid('phone')) { <span class="tn-error">Enter a valid phone number.</span> }
              </div>
              <div class="tn-field">
                <label for="alternatePhone">Alternate phone</label>
                <input id="alternatePhone" class="tn-input" formControlName="alternatePhone" type="tel" maxlength="30" />
              </div>
              <div class="tn-field">
                <label for="email">Email</label>
                <input id="email" class="tn-input" formControlName="email" type="email" maxlength="200" />
                @if (invalid('email')) { <span class="tn-error">Enter a valid email address.</span> }
              </div>
              <div class="tn-field">
                <label for="city">City</label>
                <input id="city" class="tn-input" formControlName="city" maxlength="100" />
              </div>
              <div class="tn-field md:col-span-2">
                <label for="address">Address</label>
                <input id="address" class="tn-input" formControlName="address" maxlength="500" />
              </div>
            </div>
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Sales</h3>
            <div class="tn-grid">
              <div class="tn-field">
                <label for="agent">Assigned agent</label>
                <select id="agent" class="tn-input" formControlName="assignedAgentUserId">
                  <option [ngValue]="null">Unassigned</option>
                  @for (a of lookups().agents; track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select>
              </div>
              <div class="tn-field">
                <label for="branch">Branch</label>
                <select id="branch" class="tn-input" formControlName="branchId">
                  <option [ngValue]="null">No branch</option>
                  @for (b of lookups().branches; track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }
                </select>
              </div>
              <div class="tn-field">
                <label for="lead">Lead source</label>
                <select id="lead" class="tn-input" formControlName="leadSourceId">
                  <option [ngValue]="null">Not specified</option>
                  @for (l of leadSources(); track l.id) { <option [ngValue]="l.id">{{ l.name }}</option> }
                </select>
                @if (!leadSources().length) { <span class="tn-hint">Add lead sources in TravelNexus settings.</span> }
              </div>
              <div class="tn-field">
                <label for="subAgent">Referred by (sub-agent)</label>
                <select id="subAgent" class="tn-input" formControlName="subAgentId">
                  <option [ngValue]="null">No sub-agent</option>
                  @for (a of subAgents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select>
                <span class="tn-hint">Becomes the default sub-agent for commission on this customer's invoices.</span>
              </div>
            </div>
            <div class="tn-field">
              <label for="notes">Notes</label>
              <textarea id="notes" class="tn-input" formControlName="notes" maxlength="2000"></textarea>
            </div>
          </section>

          @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }

          <div class="tn-actions">
            <a mat-stroked-button [routerLink]="cancelLink()">Cancel</a>
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">
              {{ saving() ? 'Saving…' : (isEdit() ? 'Save changes' : 'Create customer') }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelCustomerFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly customers = inject(TravelCustomerService);
  private readonly config = inject(TravelConfigService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly customerId = Number(this.route.snapshot.paramMap.get('id')) || null;
  protected readonly isEdit = signal(this.customerId !== null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly kind = signal<TravelCustomerKind>('Individual');
  protected readonly lookups = signal<TravelLookups>({ branches: [], agents: [] });
  protected readonly leadSources = signal<TravelLeadSource[]>([]);
  protected readonly subAgents = signal<TravelSubAgent[]>([]);
  protected readonly storedCnic = signal<string | null>(null);
  protected readonly cnicMasked = computed(() => isMasked(this.storedCnic()));
  protected readonly cancelLink = computed(() =>
    this.customerId ? ['/travel/customers', this.customerId] : ['/travel/customers']);

  protected readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    alternatePhone: [''],
    email: ['', [Validators.pattern(EMAIL_PATTERN)]],
    city: [''],
    address: [''],
    cnic: [''],
    contactPerson: [''],
    taxNumber: [''],
    assignedAgentUserId: [null as number | null],
    branchId: [null as number | null],
    leadSourceId: [null as number | null],
    subAgentId: [null as number | null],
    notes: ['']
  });

  constructor() {
    forkJoin({
      lookups: this.config.getLookups(),
      leadSources: this.config.getLeadSources(true),
      subAgents: inject(TravelCommissionService).getSubAgents(true).pipe(catchError(() => of([] as TravelSubAgent[]))),
      customer: this.customerId ? this.customers.getCustomer(this.customerId) : of(null)
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ lookups, leadSources, subAgents, customer }) => {
          this.lookups.set(lookups);
          this.leadSources.set(leadSources);
          this.subAgents.set(subAgents);
          if (customer) this.patch(customer);
          this.loading.set(false);
        },
        error: err => {
          this.loadError.set(apiErrorMessage(err, 'Could not load the form.'));
          this.loading.set(false);
        }
      });
  }

  protected setKind(kind: TravelCustomerKind): void { this.kind.set(kind); }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  protected save(): void {
    this.saveError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const blank = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
    const corporate = this.kind() === 'Corporate';

    const cnicInput = blank(v.cnic);
    const payload: SaveTravelCustomer = {
      customerKind: this.kind(),
      fullName: v.fullName!.trim(),
      phone: v.phone!.trim(),
      email: blank(v.email),
      address: blank(v.address),
      city: blank(v.city),
      // Individual: typed value replaces; blank keeps a masked value, or clears a visible one the user emptied.
      cnic: corporate
        ? (this.isEdit() ? '' : null)
        : cnicInput ?? (this.isEdit() && this.storedCnic() && !this.cnicMasked() ? '' : null),
      contactPerson: corporate ? blank(v.contactPerson) : null,
      taxNumber: corporate ? blank(v.taxNumber) : null,
      alternatePhone: blank(v.alternatePhone),
      assignedAgentUserId: v.assignedAgentUserId,
      branchId: v.branchId,
      leadSourceId: v.leadSourceId,
      notes: blank(v.notes),
      subAgentId: v.subAgentId
    };

    this.saving.set(true);
    const request$: Observable<unknown> = this.customerId
      ? this.customers.updateCustomer(this.customerId, payload)
      : this.customers.createCustomer({ ...payload, existingCustomerId: null } satisfies CreateTravelCustomer);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.saving.set(false);
        this.toast.success(this.customerId ? 'Customer updated.' : 'Customer created.');
        const id = this.customerId ?? (result as number);
        this.router.navigate(['/travel/customers', id]);
      },
      error: err => {
        this.saving.set(false);
        this.saveError.set(apiErrorMessage(err, 'Could not save the customer.'));
      }
    });
  }

  private patch(c: TravelCustomer): void {
    this.kind.set(c.customerKind);
    this.storedCnic.set(c.cnic);
    this.form.patchValue({
      fullName: c.fullName,
      phone: c.phone,
      alternatePhone: c.alternatePhone ?? '',
      email: c.email ?? '',
      city: c.city ?? '',
      address: c.address ?? '',
      // A masked CNIC is never placed in an editable field.
      cnic: c.cnic && !isMasked(c.cnic) ? c.cnic : '',
      contactPerson: c.contactPerson ?? '',
      taxNumber: c.taxNumber ?? '',
      assignedAgentUserId: c.assignedAgentUserId,
      branchId: c.branchId,
      leadSourceId: c.leadSourceId,
      subAgentId: c.subAgentId,
      notes: c.notes ?? ''
    });
  }
}
