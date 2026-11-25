import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import {
  SaveTravelEnquiry, TRAVEL_PRIORITIES, TRAVEL_SERVICE_TYPES, TravelCustomerListItem, TravelEnquiry, TravelLeadSource,
  TravelLookups, TravelPriority, TravelServiceType, TravelVisaType
} from '../../models/travel.models';
import { fromDateTimeLocal, toDateInput, toDateTimeLocal } from '../../models/travel-date.util';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelEnquiryService } from '../../services/travel-enquiry.service';

const PHONE = /^\+?[0-9 \-()]{7,30}$/;

@Component({
  selector: 'app-travel-enquiry-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="id ? 'Edit enquiry' : 'New enquiry'" eyebrow="TravelNexus" icon="contact_phone"
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Enquiries', route: '/travel/enquiries' }, { label: id ? 'Edit' : 'New' }]">
      </ui-page-header>

      @if (loadError()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div>
      } @else if (loading()) {
        <div class="tn-card tn-card-body space-y-3">@for (s of [1,2,3]; track s) { <div class="h-10 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else {
        <form class="tn-card tn-card-body space-y-6" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <section class="space-y-3">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <h3 class="tn-section-title">Who is enquiring?</h3>
              <div class="tn-segment" role="radiogroup" aria-label="Lead or customer">
                <button type="button" role="radio" [attr.aria-checked]="mode() === 'lead'" [class.active]="mode() === 'lead'" (click)="mode.set('lead')">New lead</button>
                <button type="button" role="radio" [attr.aria-checked]="mode() === 'customer'" [class.active]="mode() === 'customer'" (click)="mode.set('customer')">Existing customer</button>
              </div>
            </div>
            @if (mode() === 'customer') {
              <div class="tn-field">
                <label for="customer-picker" class="tn-required">Customer</label>
                <app-travel-customer-picker [(selected)]="customer" />
                @if (submitted() && !customer()) { <span class="tn-error">Choose a customer.</span> }
              </div>
            } @else {
              <div class="tn-grid">
                <div class="tn-field"><label for="e-name" class="tn-required">Name</label>
                  <input id="e-name" class="tn-input" formControlName="leadName" maxlength="100" />
                  @if (leadInvalid('leadName')) { <span class="tn-error">Required.</span> }</div>
                <div class="tn-field"><label for="e-phone" class="tn-required">Phone</label>
                  <input id="e-phone" class="tn-input" type="tel" formControlName="leadPhone" maxlength="30" placeholder="+92 300 1234567" />
                  @if (leadInvalid('leadPhone')) { <span class="tn-error">Enter a valid phone number.</span> }</div>
                <div class="tn-field"><label for="e-email">Email</label>
                  <input id="e-email" class="tn-input" type="email" formControlName="leadEmail" maxlength="200" /></div>
              </div>
            }
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Requirement</h3>
            <div class="tn-grid">
              <div class="tn-field"><label for="e-svc">Service</label>
                <select id="e-svc" class="tn-input" formControlName="serviceType">
                  @for (s of services; track s) { <option [value]="s">{{ s }}</option> }
                </select></div>
              <div class="tn-field"><label for="e-country">Destination</label>
                <select id="e-country" class="tn-input" formControlName="destinationCountryCode">
                  <option [ngValue]="null">—</option>
                  @for (c of countries(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
                </select></div>
              @if (form.controls.serviceType.value === 'Visa' || form.controls.serviceType.value === 'Umrah') {
                <div class="tn-field"><label for="e-vt">Visa type</label>
                  <select id="e-vt" class="tn-input" formControlName="visaTypeId">
                    <option [ngValue]="null">—</option>
                    @for (v of visaTypesForCountry(); track v.id) { <option [ngValue]="v.id">{{ v.name }}</option> }
                  </select>
                  @if (form.controls.destinationCountryCode.value && !visaTypesForCountry().length) {
                    <span class="tn-hint">No visa types configured for this country.</span> }</div>
              }
              <div class="tn-field"><label for="e-from">Travel from</label><input id="e-from" class="tn-input" type="date" formControlName="travelDateFrom" /></div>
              <div class="tn-field"><label for="e-to">Travel to</label><input id="e-to" class="tn-input" type="date" formControlName="travelDateTo" />
                @if (form.hasError('dates')) { <span class="tn-error">Must be on or after the departure date.</span> }</div>
              <div class="tn-field"><label for="e-ad">Adults</label><input id="e-ad" class="tn-input" type="number" min="0" formControlName="adults" /></div>
              <div class="tn-field"><label for="e-ch">Children</label><input id="e-ch" class="tn-input" type="number" min="0" formControlName="children" /></div>
              <div class="tn-field"><label for="e-in">Infants</label><input id="e-in" class="tn-input" type="number" min="0" formControlName="infants" />
                @if (form.hasError('pax')) { <span class="tn-error">At least one traveler.</span> }</div>
              <div class="tn-field"><label for="e-budget">Budget</label><input id="e-budget" class="tn-input" type="number" min="0" step="0.01" formControlName="budget" /></div>
              <div class="tn-field"><label for="e-cur">Currency (ISO)</label>
                <input id="e-cur" class="tn-input uppercase" formControlName="currencyCode" maxlength="3" [placeholder]="defaultCurrency() ?? 'PKR'" />
                @if (form.hasError('budgetCurrency')) { <span class="tn-error">A budget needs a 3-letter currency.</span> }</div>
            </div>
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Follow-up</h3>
            <div class="tn-grid">
              <div class="tn-field"><label for="e-pri">Priority</label>
                <select id="e-pri" class="tn-input" formControlName="priority">
                  @for (p of priorities; track p) { <option [value]="p">{{ p }}</option> }
                </select></div>
              <div class="tn-field"><label for="e-next">Next follow-up</label>
                <input id="e-next" class="tn-input" type="datetime-local" formControlName="nextFollowUpAt" /></div>
              <div class="tn-field"><label for="e-agent">Assigned agent</label>
                <select id="e-agent" class="tn-input" formControlName="assignedAgentUserId">
                  <option [ngValue]="null">Unassigned</option>
                  @for (a of lookups().agents; track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select></div>
              <div class="tn-field"><label for="e-branch">Branch</label>
                <select id="e-branch" class="tn-input" formControlName="branchId">
                  <option [ngValue]="null">{{ mode() === 'customer' ? "Customer's branch" : 'No branch' }}</option>
                  @for (b of lookups().branches; track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }
                </select></div>
              <div class="tn-field"><label for="e-src">Lead source</label>
                <select id="e-src" class="tn-input" formControlName="leadSourceId">
                  <option [ngValue]="null">Not specified</option>
                  @for (l of leadSources(); track l.id) { <option [ngValue]="l.id">{{ l.name }}</option> }
                </select></div>
            </div>
            <div class="tn-field"><label for="e-notes">Notes</label><textarea id="e-notes" class="tn-input" formControlName="notes" maxlength="2000"></textarea></div>
          </section>

          @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
          <div class="tn-actions">
            <a mat-stroked-button [routerLink]="id ? ['/travel/enquiries', id] : ['/travel/enquiries']">Cancel</a>
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : (id ? 'Save changes' : 'Create enquiry') }}</button>
          </div>
        </form>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelEnquiryFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly api = inject(TravelEnquiryService);
  private readonly config = inject(TravelConfigService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly id = Number(this.route.snapshot.paramMap.get('id')) || null;
  protected readonly services = TRAVEL_SERVICE_TYPES;
  protected readonly priorities = TRAVEL_PRIORITIES;
  protected readonly mode = signal<'lead' | 'customer'>('lead');
  protected readonly customer = signal<TravelCustomerListItem | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly lookups = signal<TravelLookups>({ branches: [], agents: [] });
  protected readonly leadSources = signal<TravelLeadSource[]>([]);
  protected readonly visaTypes = signal<TravelVisaType[]>([]);
  protected readonly defaultCurrency = signal<string | null>(null);
  protected readonly countries = toSignal(this.config.countries$.pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly form = this.fb.group({
    leadName: ['', Validators.maxLength(100)],
    leadPhone: ['', Validators.pattern(PHONE)],
    leadEmail: ['', Validators.email],
    serviceType: ['Visa' as TravelServiceType],
    destinationCountryCode: [null as string | null],
    visaTypeId: [null as number | null],
    travelDateFrom: [''],
    travelDateTo: [''],
    adults: [1, [Validators.min(0)]],
    children: [0, [Validators.min(0)]],
    infants: [0, [Validators.min(0)]],
    budget: [null as number | null, Validators.min(0)],
    currencyCode: [''],
    priority: ['Normal' as TravelPriority],
    nextFollowUpAt: [''],
    assignedAgentUserId: [null as number | null],
    branchId: [null as number | null],
    leadSourceId: [null as number | null],
    notes: ['']
  }, {
    validators: g => {
      const v = g.getRawValue();
      const errors: Record<string, boolean> = {};
      if (v.travelDateFrom && v.travelDateTo && v.travelDateTo < v.travelDateFrom) errors['dates'] = true;
      if ((+v.adults || 0) + (+v.children || 0) + (+v.infants || 0) < 1) errors['pax'] = true;
      const cur = (v.currencyCode ?? '').trim();
      if ((v.budget !== null && v.budget !== ('' as unknown)) && !/^[A-Za-z]{3}$/.test(cur || this.defaultCurrency() || '')) errors['budgetCurrency'] = true;
      return Object.keys(errors).length ? errors : null;
    }
  });

  private readonly countryValue = toSignal(this.form.controls.destinationCountryCode.valueChanges, { initialValue: null as string | null });
  protected readonly visaTypesForCountry = computed(() => {
    const c = this.countryValue();
    return this.visaTypes().filter(v => v.isActive && (!c || v.countryCode === c));
  });

  constructor() {
    forkJoin({
      lookups: this.config.getLookups(),
      leadSources: this.config.getLeadSources(true),
      visaTypes: this.config.getVisaTypes(),
      settings: this.config.getSettings().pipe(catchError(() => of(null))),
      enquiry: this.id ? this.api.getEnquiry(this.id) : of(null)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.lookups.set(r.lookups);
        this.leadSources.set(r.leadSources);
        this.visaTypes.set(r.visaTypes);
        this.defaultCurrency.set(r.settings?.defaultCurrencyCode ?? null);
        if (r.enquiry) this.patch(r.enquiry);
        this.loading.set(false);
      },
      error: err => { this.loadError.set(apiErrorMessage(err, 'Could not load the form.')); this.loading.set(false); }
    });
  }

  protected leadInvalid(name: 'leadName' | 'leadPhone'): boolean {
    const c = this.form.controls[name];
    return this.mode() === 'lead' && (this.submitted() || c.touched) && (!c.value?.trim() || c.invalid);
  }

  protected save(): void {
    this.submitted.set(true);
    this.saveError.set(null);
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    const leadMode = this.mode() === 'lead';
    if (this.form.invalid || (leadMode && (!v.leadName?.trim() || !v.leadPhone?.trim())) || (!leadMode && !this.customer())) return;

    const blank = (s: string | null | undefined) => (s && String(s).trim() ? String(s).trim() : null);
    const budget = v.budget === null || (v.budget as unknown) === '' ? null : Number(v.budget);
    const dto: SaveTravelEnquiry = {
      customerId: leadMode ? null : this.customer()!.customerId,
      leadName: leadMode ? blank(v.leadName) : null,
      leadPhone: leadMode ? blank(v.leadPhone) : null,
      leadEmail: leadMode ? blank(v.leadEmail) : null,
      serviceType: v.serviceType!,
      destinationCountryCode: v.destinationCountryCode,
      visaTypeId: v.visaTypeId,
      travelDateFrom: blank(v.travelDateFrom),
      travelDateTo: blank(v.travelDateTo),
      adults: Number(v.adults) || 0,
      children: Number(v.children) || 0,
      infants: Number(v.infants) || 0,
      budget,
      currencyCode: budget === null ? null : (blank(v.currencyCode)?.toUpperCase() ?? this.defaultCurrency()),
      leadSourceId: v.leadSourceId,
      assignedAgentUserId: v.assignedAgentUserId,
      branchId: v.branchId,
      priority: v.priority!,
      nextFollowUpAt: fromDateTimeLocal(v.nextFollowUpAt),
      notes: blank(v.notes)
    };

    this.saving.set(true);
    const request$: Observable<unknown> = this.id ? this.api.update(this.id, dto) : this.api.create(dto);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.saving.set(false);
        this.toast.success(this.id ? 'Enquiry updated.' : 'Enquiry created.');
        this.router.navigate(['/travel/enquiries', this.id ?? (r as number)]);
      },
      error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the enquiry.')); }
    });
  }

  private patch(e: TravelEnquiry): void {
    if (e.customerId) {
      this.mode.set('customer');
      this.customer.set({
        customerId: e.customerId, fullName: e.displayName, phone: e.displayPhone ?? '', customerKind: 'Individual',
        contactPerson: null, email: null, city: null, assignedAgentUserId: null, assignedAgentName: null, branchId: null,
        branchName: null, leadSourceName: null, travelerCount: 0, nextPassportExpiry: null, createdAt: e.createdAt
      });
    }
    this.form.reset({
      leadName: e.leadName ?? '', leadPhone: e.leadPhone ?? '', leadEmail: e.leadEmail ?? '',
      serviceType: e.serviceType, destinationCountryCode: e.destinationCountryCode, visaTypeId: e.visaTypeId,
      travelDateFrom: toDateInput(e.travelDateFrom), travelDateTo: toDateInput(e.travelDateTo),
      adults: e.adults, children: e.children, infants: e.infants, budget: e.budget, currencyCode: e.currencyCode ?? '',
      priority: e.priority, nextFollowUpAt: toDateTimeLocal(e.nextFollowUpAt), assignedAgentUserId: e.assignedAgentUserId,
      branchId: e.branchId, leadSourceId: e.leadSourceId, notes: e.notes ?? ''
    });
  }
}
