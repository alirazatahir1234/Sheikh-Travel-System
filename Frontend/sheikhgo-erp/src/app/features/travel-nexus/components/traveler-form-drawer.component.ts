import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { UiDrawerComponent } from '../../../shared/components/ui';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  SaveTravelTraveler, TRAVEL_GENDERS, TRAVEL_MAHRAM_RELATIONS, TravelCountry, TravelGender,
  TravelMahramRelation, TravelTraveler, isMasked
} from '../models/travel.models';
import { TravelCustomerService } from '../services/travel-customer.service';
import { TravelDocumentService } from '../services/travel-document.service';

/** Expiry must be after issue (both yyyy-MM-dd strings). */
function passportDatesValidator(group: AbstractControl): ValidationErrors | null {
  const issue = group.get('passportIssueDate')?.value as string | null;
  const expiry = group.get('passportExpiryDate')?.value as string | null;
  return issue && expiry && expiry <= issue ? { passportDates: true } : null;
}

const toDateInput = (iso: string | null): string => (iso ? iso.substring(0, 10) : '');

@Component({
  selector: 'app-traveler-form-drawer',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [(open)]="open" [title]="traveler() ? 'Edit traveler' : 'Add traveler'" width="560px">
      <form class="space-y-5" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-dashed border-fleet-border p-3">
          <p class="text-xs text-fleet-text-muted">Enter names exactly as printed in the passport, or scan the photo page to fill them in.</p>
          <label class="inline-flex cursor-pointer items-center gap-1 text-sm font-semibold text-fleet-primary">
            <input type="file" class="sr-only" accept=".jpg,.jpeg,.png" [disabled]="scanning()" (change)="scanPassport($event)" />
            <mat-icon class="!text-[18px]">document_scanner</mat-icon> {{ scanning() ? 'Reading…' : 'Scan passport' }}
          </label>
        </div>
        @if (scanMessage()) {
          <div class="tn-banner" [class.error]="scanWarning()" [class.info]="!scanWarning()">
            <mat-icon>{{ scanWarning() ? 'warning' : 'info' }}</mat-icon>{{ scanMessage() }}</div>
        }

        <div class="tn-grid">
          <div class="tn-field">
            <label for="t-given" class="tn-required">Given names</label>
            <input id="t-given" class="tn-input uppercase" formControlName="givenNames" maxlength="100" />
            @if (invalid('givenNames')) { <span class="tn-error">Required.</span> }
          </div>
          <div class="tn-field">
            <label for="t-surname" class="tn-required">Surname</label>
            <input id="t-surname" class="tn-input uppercase" formControlName="surname" maxlength="100" />
            @if (invalid('surname')) { <span class="tn-error">Required.</span> }
          </div>
          <div class="tn-field">
            <label for="t-gender" class="tn-required">Gender</label>
            <select id="t-gender" class="tn-input" formControlName="gender">
              @for (g of genders; track g) { <option [value]="g">{{ g }}</option> }
            </select>
          </div>
          <div class="tn-field">
            <label for="t-dob">Date of birth</label>
            <input id="t-dob" class="tn-input" type="date" formControlName="dateOfBirth" [max]="today" />
          </div>
          <div class="tn-field">
            <label for="t-pob">Place of birth</label>
            <input id="t-pob" class="tn-input" formControlName="placeOfBirth" maxlength="100" />
          </div>
          <div class="tn-field">
            <label for="t-nat">Nationality</label>
            <select id="t-nat" class="tn-input" formControlName="nationalityCode">
              <option [ngValue]="null">—</option>
              @for (c of countries(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
            </select>
          </div>
        </div>

        <h4 class="tn-section-title">Passport</h4>
        <div class="tn-grid">
          <div class="tn-field">
            <label for="t-pp">Passport number</label>
            <input id="t-pp" class="tn-input uppercase" formControlName="passportNumber" maxlength="20" autocomplete="off"
              [placeholder]="passportMasked() ? (traveler()?.passportNumber ?? '') : 'AB1234567'" />
            @if (passportMasked()) { <span class="tn-hint">Stored number is hidden. Leave blank to keep it.</span> }
            @if (invalid('passportNumber')) { <span class="tn-error">5–20 letters or digits.</span> }
          </div>
          <div class="tn-field">
            <label for="t-iss">Issuing country</label>
            <select id="t-iss" class="tn-input" formControlName="passportIssuingCountryCode">
              <option [ngValue]="null">—</option>
              @for (c of countries(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
            </select>
            @if (needsIssuingCountry()) { <span class="tn-error">Required with a passport number.</span> }
          </div>
          <div class="tn-field">
            <label for="t-issue">Issue date</label>
            <input id="t-issue" class="tn-input" type="date" formControlName="passportIssueDate" [max]="today" />
          </div>
          <div class="tn-field">
            <label for="t-exp">Expiry date</label>
            <input id="t-exp" class="tn-input" type="date" formControlName="passportExpiryDate" />
            @if (form.hasError('passportDates')) { <span class="tn-error">Must be after the issue date.</span> }
          </div>
        </div>

        <h4 class="tn-section-title">Identity &amp; contact</h4>
        <div class="tn-grid">
          <div class="tn-field">
            <label for="t-cnic">CNIC</label>
            <input id="t-cnic" class="tn-input" formControlName="cnic" maxlength="15" autocomplete="off"
              [placeholder]="cnicMasked() ? (traveler()?.cnic ?? '') : '35202-1234567-1'" />
            @if (invalid('cnic')) { <span class="tn-error">13 digits, e.g. 35202-1234567-1.</span> }
          </div>
          <div class="tn-field">
            <label for="t-phone">Phone</label>
            <input id="t-phone" class="tn-input" type="tel" formControlName="phone" maxlength="30" />
          </div>
          <div class="tn-field">
            <label for="t-email">Email</label>
            <input id="t-email" class="tn-input" type="email" formControlName="email" maxlength="200" />
          </div>
        </div>

        @if (form.controls.gender.value === 'Female') {
          <h4 class="tn-section-title">Mahram</h4>
          <div class="tn-grid">
            <div class="tn-field">
              <label for="t-mahram">Mahram traveler</label>
              <select id="t-mahram" class="tn-input" formControlName="mahramTravelerId">
                <option [ngValue]="null">None</option>
                @for (m of mahramOptions(); track m.id) { <option [ngValue]="m.id">{{ m.fullName }}</option> }
              </select>
              @if (!mahramOptions().length) { <span class="tn-hint">Add a male traveler to this customer first.</span> }
            </div>
            @if (form.controls.mahramTravelerId.value) {
              <div class="tn-field">
                <label for="t-rel" class="tn-required">Relation</label>
                <select id="t-rel" class="tn-input" formControlName="mahramRelation">
                  <option [ngValue]="null">Choose…</option>
                  @for (r of relations; track r) { <option [ngValue]="r">{{ r }}</option> }
                </select>
                @if (form.hasError('mahramRelation')) { <span class="tn-error">Choose the relation.</span> }
              </div>
            }
          </div>
        }

        <div class="tn-field">
          <label for="t-notes">Notes</label>
          <textarea id="t-notes" class="tn-input" formControlName="notes" maxlength="1000"></textarea>
        </div>

        @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }

        <div class="tn-actions">
          <button mat-stroked-button type="button" (click)="open.set(false)">Cancel</button>
          <button mat-flat-button color="primary" type="submit" [disabled]="saving()">
            {{ saving() ? 'Saving…' : 'Save traveler' }}
          </button>
        </div>
      </form>
    </ui-drawer>
  `,
  styleUrl: '../travel-nexus.scss'
})
export class TravelerFormDrawerComponent {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(TravelCustomerService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  readonly customerId = input.required<number>();
  /** Traveler being edited, or null to add. */
  readonly traveler = input<TravelTraveler | null>(null);
  /** All travelers of the customer (for mahram choices). */
  readonly siblings = input<TravelTraveler[]>([]);
  readonly countries = input<TravelCountry[]>([]);
  readonly saved = output<void>();

  protected readonly genders = TRAVEL_GENDERS;
  protected readonly relations = TRAVEL_MAHRAM_RELATIONS;
  protected readonly today = new Date().toISOString().substring(0, 10);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly scanning = signal(false);
  protected readonly scanMessage = signal<string | null>(null);
  protected readonly scanWarning = signal(false);
  private readonly documents = inject(TravelDocumentService);

  protected readonly passportMasked = computed(() => isMasked(this.traveler()?.passportNumber));
  protected readonly cnicMasked = computed(() => isMasked(this.traveler()?.cnic));
  protected readonly mahramOptions = computed(() =>
    this.siblings().filter(t => t.gender === 'Male' && t.id !== this.traveler()?.id));

  protected readonly form = this.fb.group(
    {
      givenNames: ['', [Validators.required, Validators.maxLength(100)]],
      surname: ['', [Validators.required, Validators.maxLength(100)]],
      gender: ['Male' as TravelGender, Validators.required],
      dateOfBirth: [''],
      placeOfBirth: [''],
      nationalityCode: [null as string | null],
      passportNumber: ['', Validators.pattern(/^[A-Za-z0-9]{5,20}$/)],
      passportIssuingCountryCode: [null as string | null],
      passportIssueDate: [''],
      passportExpiryDate: [''],
      cnic: ['', Validators.pattern(/^\d{5}-?\d{7}-?\d$/)],
      phone: [''],
      email: ['', Validators.email],
      mahramTravelerId: [null as number | null],
      mahramRelation: [null as TravelMahramRelation | null],
      notes: ['']
    },
    {
      validators: [
        passportDatesValidator,
        (g: AbstractControl) => g.get('mahramTravelerId')?.value && !g.get('mahramRelation')?.value ? { mahramRelation: true } : null
      ]
    }
  );

  constructor() {
    // Reset the form whenever the drawer opens.
    effect(() => {
      if (this.open()) this.reset(this.traveler());
    });
  }

  /** Reads the passport MRZ and pre-fills the form; nothing is saved until the user clicks Save. */
  protected scanPassport(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.scanning.set(true);
    this.scanMessage.set(null);
    this.documents.scanPassport(file).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.scanning.set(false);
        this.scanMessage.set(r.message);
        this.scanWarning.set(!r.found || !(r.passport?.checksumValid ?? false));
        const p = r.passport;
        if (!p) return;
        this.form.patchValue({
          givenNames: p.givenNames || this.form.controls.givenNames.value,
          surname: p.surname || this.form.controls.surname.value,
          gender: p.gender ?? this.form.controls.gender.value,
          dateOfBirth: p.dateOfBirth ? p.dateOfBirth.substring(0, 10) : this.form.controls.dateOfBirth.value,
          nationalityCode: p.nationalityCode ?? this.form.controls.nationalityCode.value,
          passportNumber: p.passportNumber || this.form.controls.passportNumber.value,
          passportIssuingCountryCode: p.issuingCountryCode ?? this.form.controls.passportIssuingCountryCode.value,
          passportExpiryDate: p.expiryDate ? p.expiryDate.substring(0, 10) : this.form.controls.passportExpiryDate.value
        });
        this.form.markAsDirty();
      },
      error: err => { this.scanning.set(false); this.scanWarning.set(true); this.scanMessage.set(apiErrorMessage(err, 'Could not read the passport.')); }
    });
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  protected needsIssuingCountry(): boolean {
    const v = this.form.controls;
    const hasPassport = !!v.passportNumber.value?.trim() || this.passportMasked();
    return hasPassport && !v.passportIssuingCountryCode.value && (v.passportNumber.touched || v.passportIssuingCountryCode.touched);
  }

  protected save(): void {
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.needsIssuingCountry()) return;

    const v = this.form.getRawValue();
    const blank = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
    const editing = this.traveler();
    const female = v.gender === 'Female';

    /** Typed value replaces; blank keeps a masked value, or clears a visible one the user emptied. */
    const identity = (typed: string | null | undefined, stored: string | null | undefined): string | null => {
      const t = blank(typed);
      if (t) return t;
      if (!editing || !stored) return null;
      return isMasked(stored) ? null : '';
    };

    const payload: SaveTravelTraveler = {
      title: null,
      givenNames: v.givenNames!.trim(),
      surname: v.surname!.trim(),
      gender: v.gender!,
      dateOfBirth: blank(v.dateOfBirth),
      placeOfBirth: blank(v.placeOfBirth),
      nationalityCode: v.nationalityCode,
      passportNumber: identity(v.passportNumber, editing?.passportNumber),
      passportIssuingCountryCode: v.passportIssuingCountryCode,
      passportIssueDate: blank(v.passportIssueDate),
      passportExpiryDate: blank(v.passportExpiryDate),
      cnic: identity(v.cnic, editing?.cnic),
      phone: blank(v.phone),
      email: blank(v.email),
      mahramTravelerId: female ? v.mahramTravelerId : null,
      mahramRelation: female && v.mahramTravelerId ? v.mahramRelation : null,
      notes: blank(v.notes)
    };

    this.saving.set(true);
    const request$: Observable<unknown> = editing
      ? this.service.updateTraveler(editing.id, payload)
      : this.service.createTraveler(this.customerId(), payload);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(editing ? 'Traveler updated.' : 'Traveler added.');
        this.open.set(false);
        this.saved.emit();
      },
      error: err => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save the traveler.'));
      }
    });
  }

  private reset(t: TravelTraveler | null): void {
    this.error.set(null);
    this.scanMessage.set(null);
    this.form.reset({
      givenNames: t?.givenNames ?? '',
      surname: t?.surname ?? '',
      gender: t?.gender ?? 'Male',
      dateOfBirth: toDateInput(t?.dateOfBirth ?? null),
      placeOfBirth: t?.placeOfBirth ?? '',
      nationalityCode: t?.nationalityCode ?? null,
      // Masked identity values are never placed in editable fields.
      passportNumber: t?.passportNumber && !isMasked(t.passportNumber) ? t.passportNumber : '',
      passportIssuingCountryCode: t?.passportIssuingCountryCode ?? null,
      passportIssueDate: toDateInput(t?.passportIssueDate ?? null),
      passportExpiryDate: toDateInput(t?.passportExpiryDate ?? null),
      cnic: t?.cnic && !isMasked(t.cnic) ? t.cnic : '',
      phone: t?.phone ?? '',
      email: t?.email ?? '',
      mahramTravelerId: t?.mahramTravelerId ?? null,
      mahramRelation: t?.mahramRelation ?? null,
      notes: t?.notes ?? ''
    });
  }
}
