import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import {
  TRAVEL_PRIORITIES, TravelCustomerListItem, TravelLookups, TravelPriority, TravelVisaCase, TravelVisaType, UpdateTravelVisaCase
} from '../../models/travel.models';
import { fromDateTimeLocal, toDateInput, toDateTimeLocal } from '../../models/travel-date.util';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelCustomerService } from '../../services/travel-customer.service';
import { TravelVisaService } from '../../services/travel-visa.service';

@Component({
  selector: 'app-travel-visa-case-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="id ? 'Edit visa case' : 'New visa case'" eyebrow="TravelNexus" icon="assignment_ind"
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Visa cases', route: '/travel/visa-cases' }, { label: id ? 'Edit' : 'New' }]">
      </ui-page-header>

      @if (loadError()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div>
      } @else if (loading()) {
        <div class="tn-card tn-card-body space-y-3">@for (s of [1,2,3]; track s) { <div class="h-10 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else {
        <form class="tn-card tn-card-body space-y-6" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <section class="space-y-3">
            <h3 class="tn-section-title">Case</h3>
            @if (existing(); as c) {
              <p class="text-sm"><strong>{{ c.caseNumber }}</strong> · {{ c.customerName }} · {{ c.countryName }} — {{ c.visaTypeName }}</p>
            } @else {
              <div class="tn-grid">
                <div class="tn-field md:col-span-2"><label for="customer-picker" class="tn-required">Customer</label>
                  <app-travel-customer-picker [(selected)]="customer" />
                  @if (submitted() && !customer()) { <span class="tn-error">Choose a customer.</span> }</div>
                <div class="tn-field"><label for="v-country" class="tn-required">Country</label>
                  <select id="v-country" class="tn-input" [value]="country()" (change)="setCountry($any($event.target).value)">
                    <option value="">Choose…</option>
                    @for (c of visaCountries(); track c.code) { <option [value]="c.code">{{ c.name }}</option> }
                  </select>
                  @if (!visaCountries().length) { <span class="tn-hint">Configure visa types in TravelNexus settings first.</span> }</div>
                <div class="tn-field"><label for="v-type" class="tn-required">Visa type</label>
                  <select id="v-type" class="tn-input" formControlName="visaTypeId">
                    <option [ngValue]="null">Choose…</option>
                    @for (v of typesForCountry(); track v.id) { <option [ngValue]="v.id">{{ v.name }}</option> }
                  </select>
                  @if (submitted() && !form.controls.visaTypeId.value) { <span class="tn-error">Choose a visa type.</span> }</div>
              </div>
            }
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Schedule</h3>
            <div class="tn-grid">
              <div class="tn-field"><label for="v-travel">Intended travel date</label><input id="v-travel" class="tn-input" type="date" formControlName="intendedTravelDate" /></div>
              <div class="tn-field"><label for="v-appt">Appointment</label><input id="v-appt" class="tn-input" type="datetime-local" formControlName="appointmentAt" /></div>
              <div class="tn-field"><label for="v-exp">Expected decision</label><input id="v-exp" class="tn-input" type="date" formControlName="expectedDecisionDate" /></div>
              <div class="tn-field"><label for="v-pri">Priority</label>
                <select id="v-pri" class="tn-input" formControlName="priority">@for (p of priorities; track p) { <option [value]="p">{{ p }}</option> }</select></div>
              <div class="tn-field"><label for="v-agent">Assigned agent</label>
                <select id="v-agent" class="tn-input" formControlName="assignedAgentUserId">
                  <option [ngValue]="null">Unassigned</option>
                  @for (a of lookups().agents; track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select></div>
            </div>
          </section>

          <section class="space-y-3">
            <h3 class="tn-section-title">Quoted fees <span class="text-xs font-normal text-fleet-text-muted">(for information; invoicing comes later)</span></h3>
            <div class="tn-grid">
              <div class="tn-field"><label for="v-svc">Service fee</label><input id="v-svc" class="tn-input" type="number" min="0" step="0.01" formControlName="quotedServiceFee" /></div>
              <div class="tn-field"><label for="v-emb">Embassy fee</label><input id="v-emb" class="tn-input" type="number" min="0" step="0.01" formControlName="quotedEmbassyFee" /></div>
              <div class="tn-field"><label for="v-cur">Currency (ISO)</label>
                <input id="v-cur" class="tn-input uppercase" formControlName="currencyCode" maxlength="3" />
                @if (form.hasError('feeCurrency')) { <span class="tn-error">Fees need a 3-letter currency.</span> }</div>
            </div>
            <div class="tn-field"><label for="v-notes">Notes</label><textarea id="v-notes" class="tn-input" formControlName="notes" maxlength="2000"></textarea></div>
          </section>

          @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
          <div class="tn-actions">
            <a mat-stroked-button [routerLink]="id ? ['/travel/visa-cases', id] : ['/travel/visa-cases']">Cancel</a>
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : (id ? 'Save changes' : 'Create case') }}</button>
          </div>
        </form>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelVisaCaseFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly api = inject(TravelVisaService);
  private readonly config = inject(TravelConfigService);
  private readonly customers = inject(TravelCustomerService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly id = Number(this.route.snapshot.paramMap.get('id')) || null;
  private readonly presetCustomerId = Number(this.route.snapshot.queryParamMap.get('customerId')) || null;
  protected readonly priorities = TRAVEL_PRIORITIES;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly existing = signal<TravelVisaCase | null>(null);
  protected readonly customer = signal<TravelCustomerListItem | null>(null);
  protected readonly lookups = signal<TravelLookups>({ branches: [], agents: [] });
  protected readonly visaTypes = signal<TravelVisaType[]>([]);
  protected readonly country = signal('');

  protected readonly visaCountries = computed(() => {
    const seen = new Map<string, string>();
    for (const v of this.visaTypes()) if (!seen.has(v.countryCode)) seen.set(v.countryCode, v.countryName);
    return [...seen.entries()].map(([code, name]) => ({ code, name })).sort((a, b) => a.name.localeCompare(b.name));
  });
  protected readonly typesForCountry = computed(() => this.visaTypes().filter(v => v.countryCode === this.country()));

  protected readonly form = this.fb.group({
    visaTypeId: [null as number | null],
    intendedTravelDate: [''],
    appointmentAt: [''],
    expectedDecisionDate: [''],
    priority: ['Normal' as TravelPriority, Validators.required],
    assignedAgentUserId: [null as number | null],
    quotedServiceFee: [null as number | null, Validators.min(0)],
    quotedEmbassyFee: [null as number | null, Validators.min(0)],
    currencyCode: [''],
    notes: ['']
  }, {
    validators: g => {
      const v = g.getRawValue();
      const hasFee = (x: unknown) => x !== null && x !== '';
      return (hasFee(v.quotedServiceFee) || hasFee(v.quotedEmbassyFee)) && !/^[A-Za-z]{3}$/.test((v.currencyCode ?? '').trim())
        ? { feeCurrency: true } : null;
    }
  });

  constructor() {
    const preset$ = this.presetCustomerId
      ? this.customers.getCustomer(this.presetCustomerId).pipe(map(c => c as TravelCustomerListItem), catchError(() => of(null)))
      : of(null);
    forkJoin({
      lookups: this.config.getLookups(),
      visaTypes: this.config.getVisaTypes(undefined, true),
      settings: this.config.getSettings().pipe(catchError(() => of(null))),
      visaCase: this.id ? this.api.getCase(this.id) : of(null),
      preset: preset$
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.lookups.set(r.lookups);
        this.visaTypes.set(r.visaTypes);
        if (r.preset) this.customer.set(r.preset);
        if (r.visaCase) this.patch(r.visaCase);
        else this.form.controls.currencyCode.setValue(r.settings?.defaultCurrencyCode ?? '');
        this.loading.set(false);
      },
      error: err => { this.loadError.set(apiErrorMessage(err, 'Could not load the form.')); this.loading.set(false); }
    });
  }

  protected setCountry(code: string): void {
    this.country.set(code);
    this.form.controls.visaTypeId.setValue(null);
  }

  protected save(): void {
    this.submitted.set(true);
    this.saveError.set(null);
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    if (this.form.invalid || (!this.id && (!this.customer() || !v.visaTypeId))) return;

    const num = (x: number | null) => (x === null || (x as unknown) === '' ? null : Number(x));
    const blank = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
    const serviceFee = num(v.quotedServiceFee);
    const embassyFee = num(v.quotedEmbassyFee);
    const details: UpdateTravelVisaCase = {
      intendedTravelDate: blank(v.intendedTravelDate),
      appointmentAt: fromDateTimeLocal(v.appointmentAt),
      expectedDecisionDate: blank(v.expectedDecisionDate),
      priority: v.priority!,
      assignedAgentUserId: v.assignedAgentUserId,
      quotedServiceFee: serviceFee,
      quotedEmbassyFee: embassyFee,
      currencyCode: serviceFee === null && embassyFee === null ? null : blank(v.currencyCode)?.toUpperCase() ?? null,
      notes: blank(v.notes)
    };

    this.saving.set(true);
    const request$: Observable<unknown> = this.id
      ? this.api.update(this.id, details)
      : this.api.create({ ...details, customerId: this.customer()!.customerId, visaTypeId: v.visaTypeId!, enquiryId: null });
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.saving.set(false);
        this.toast.success(this.id ? 'Visa case updated.' : 'Visa case created.');
        this.router.navigate(['/travel/visa-cases', this.id ?? (r as number)]);
      },
      error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the visa case.')); }
    });
  }

  private patch(c: TravelVisaCase): void {
    this.existing.set(c);
    this.form.reset({
      visaTypeId: c.visaTypeId,
      intendedTravelDate: toDateInput(c.intendedTravelDate),
      appointmentAt: toDateTimeLocal(c.appointmentAt),
      expectedDecisionDate: toDateInput(c.expectedDecisionDate),
      priority: c.priority,
      assignedAgentUserId: c.assignedAgentUserId,
      quotedServiceFee: c.quotedServiceFee,
      quotedEmbassyFee: c.quotedEmbassyFee,
      currencyCode: c.currencyCode ?? '',
      notes: c.notes ?? ''
    });
  }
}
