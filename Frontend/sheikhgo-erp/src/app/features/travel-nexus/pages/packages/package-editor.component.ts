import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiConfirmService, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  PACKAGE_STATUS_VARIANT, SaveTravelPackage, TRAVEL_PERMISSIONS, TRAVEL_ROOM_TYPES, TravelHotel, TravelPackage, TravelPackageType,
  TravelRoomType, TravelVisaType
} from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelPackageService } from '../../services/travel-package.service';

/** Create / edit a package. Drafts and published packages are editable; archived packages are read-only. */
@Component({
  selector: 'app-travel-package-editor',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="pkg()?.name || 'New package'" eyebrow="TravelNexus · Packages" icon="inventory_2"
        [subtitle]="pkg() ? pkg()!.code : 'Nights, hotels and room prices'"
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Packages', route: '/travel/packages' }, { label: pkg()?.code || 'New' }]">
        @if (pkg(); as p) {
          <ui-status-badge [status]="p.status" [variant]="statusVariant[p.status]"></ui-status-badge>
          <ng-container *appHasPermission="perms.packageManage">
            @if (p.status === 'Draft') { <button mat-flat-button color="primary" [disabled]="busy()" (click)="publish()"><mat-icon>publish</mat-icon> Publish</button> }
            @if (p.status !== 'Archived') { <button mat-stroked-button [disabled]="busy()" (click)="archive()"><mat-icon>archive</mat-icon> Archive</button> }
            @if (!p.groupCount) { <button mat-stroked-button [disabled]="busy()" (click)="remove()"><mat-icon>delete_outline</mat-icon> Delete</button> }
          </ng-container>
        }
      </ui-page-header>

      @if (loadError()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div>
      } @else {
        <form class="space-y-5" [formGroup]="form" (ngSubmit)="save()" novalidate>
          @if (readOnly()) { <div class="tn-banner"><mat-icon>lock</mat-icon>Archived packages are read-only.</div> }
          <fieldset class="tn-card tn-card-body space-y-4" [disabled]="readOnly()">
            <h3 class="tn-section-title">Details</h3>
            <div class="tn-grid">
              <div class="tn-field"><label for="p-code" class="tn-required">Code</label><input id="p-code" class="tn-input uppercase" formControlName="code" maxlength="20" /></div>
              <div class="tn-field"><label for="p-name" class="tn-required">Name</label><input id="p-name" class="tn-input" formControlName="name" maxlength="150" /></div>
              <div class="tn-field"><label for="p-type">Type</label>
                <select id="p-type" class="tn-input" formControlName="packageType"><option value="Umrah">Umrah</option><option value="Hajj">Hajj</option></select></div>
              <div class="tn-field"><label for="p-visa">Visa type</label>
                <select id="p-visa" class="tn-input" formControlName="visaTypeId">
                  <option [ngValue]="null">Choose… (required to publish)</option>
                  @for (v of visaTypes(); track v.id) { <option [ngValue]="v.id">{{ v.countryName }} · {{ v.name }}</option> }
                </select></div>
              <div class="tn-field"><label for="p-n" class="tn-required">Total nights</label><input id="p-n" class="tn-input" type="number" min="1" formControlName="durationNights" /></div>
              <div class="tn-field"><label for="p-mk">Nights Makkah</label><input id="p-mk" class="tn-input" type="number" min="0" formControlName="nightsMakkah" /></div>
              <div class="tn-field"><label for="p-md">Nights Madinah</label><input id="p-md" class="tn-input" type="number" min="0" formControlName="nightsMadinah" /></div>
              <div class="tn-field"><label for="p-cur" class="tn-required">Currency (ISO)</label><input id="p-cur" class="tn-input uppercase" formControlName="currencyCode" maxlength="3" /></div>
            </div>
            @if (nightsMismatch()) { <span class="tn-error">Makkah + Madinah nights cannot exceed the total.</span> }
            <div class="tn-grid">
              <div class="tn-field"><label for="p-inc">Inclusions</label><textarea id="p-inc" class="tn-input" rows="3" formControlName="inclusions" maxlength="2000"></textarea></div>
              <div class="tn-field"><label for="p-exc">Exclusions</label><textarea id="p-exc" class="tn-input" rows="3" formControlName="exclusions" maxlength="2000"></textarea></div>
            </div>
          </fieldset>

          <fieldset class="tn-card tn-card-body space-y-3" [disabled]="readOnly()">
            <div class="flex items-center justify-between">
              <div><h3 class="tn-section-title">Default hotel stays</h3>
                <p class="text-xs text-fleet-text-muted">In travel order. Groups copy these with dates from the departure day. Total {{ stayNights() }} nights.</p></div>
              <button mat-stroked-button type="button" (click)="addStay()"><mat-icon>add</mat-icon> Add stay</button>
            </div>
            @if (!hotels().length) { <p class="text-sm text-fleet-text-muted">No active hotels — add them in Settings → Hotels.</p> }
            <div formArrayName="stays" class="space-y-2">
              @for (s of stays.controls; track s; let i = $index) {
                <div class="flex flex-wrap items-end gap-3" [formGroupName]="i">
                  <span class="w-6 pb-2 text-sm font-semibold text-fleet-text-muted">{{ i + 1 }}.</span>
                  <div class="tn-field min-w-[240px] flex-1"><label [for]="'st-h-' + i">Hotel</label>
                    <select [id]="'st-h-' + i" class="tn-input" formControlName="hotelId">
                      <option [ngValue]="null">Choose…</option>
                      @for (h of hotels(); track h.id) { <option [ngValue]="h.id">{{ h.city }} · {{ h.name }}</option> }
                    </select></div>
                  <div class="tn-field w-28"><label [for]="'st-n-' + i">Nights</label><input [id]="'st-n-' + i" class="tn-input" type="number" min="1" formControlName="nights" /></div>
                  <button mat-icon-button type="button" aria-label="Move up" [disabled]="i === 0" (click)="moveStay(i, -1)"><mat-icon>arrow_upward</mat-icon></button>
                  <button mat-icon-button type="button" aria-label="Remove stay" (click)="stays.removeAt(i)"><mat-icon>delete_outline</mat-icon></button>
                </div>
              }
            </div>
          </fieldset>

          <fieldset class="tn-card tn-card-body space-y-3" [disabled]="readOnly()">
            <div class="flex items-center justify-between">
              <div><h3 class="tn-section-title">Prices per person ({{ form.controls.currencyCode.value || '—' }})</h3>
                <p class="text-xs text-fleet-text-muted">One row per room type offered. A pilgrim's quoted price is taken from this table at registration.</p></div>
              <button mat-stroked-button type="button" [disabled]="unusedRoomTypes().length === 0" (click)="addPrice()"><mat-icon>add</mat-icon> Add room type</button>
            </div>
            <div formArrayName="prices" class="space-y-2">
              @for (p of prices.controls; track p; let i = $index) {
                <div class="flex flex-wrap items-end gap-3" [formGroupName]="i">
                  <div class="tn-field w-32"><label [for]="'pr-t-' + i">Room</label>
                    <select [id]="'pr-t-' + i" class="tn-input" formControlName="roomType">
                      @for (t of roomTypes; track t) { <option [value]="t">{{ t }}</option> }
                    </select></div>
                  <div class="tn-field w-36"><label [for]="'pr-a-' + i">Adult</label><input [id]="'pr-a-' + i" class="tn-input" type="number" min="0" step="0.01" formControlName="adultPrice" /></div>
                  <div class="tn-field w-36"><label [for]="'pr-c-' + i">Child</label><input [id]="'pr-c-' + i" class="tn-input" type="number" min="0" step="0.01" formControlName="childPrice" /></div>
                  <div class="tn-field w-36"><label [for]="'pr-i-' + i">Infant</label><input [id]="'pr-i-' + i" class="tn-input" type="number" min="0" step="0.01" formControlName="infantPrice" /></div>
                  <button mat-icon-button type="button" aria-label="Remove price" (click)="prices.removeAt(i)"><mat-icon>delete_outline</mat-icon></button>
                </div>
              }
            </div>
            @if (duplicateRoomTypes()) { <span class="tn-error">Each room type can appear once.</span> }
          </fieldset>

          @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
          @if (!readOnly()) {
            <div class="tn-actions" *appHasPermission="perms.packageManage">
              <button mat-stroked-button type="button" (click)="back()">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Save' }}</button>
            </div>
          }
        </form>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelPackageEditorComponent {
  private readonly api = inject(TravelPackageService);
  private readonly config = inject(TravelConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = PACKAGE_STATUS_VARIANT;
  protected readonly roomTypes = TRAVEL_ROOM_TYPES;
  private readonly id = Number(this.route.snapshot.paramMap.get('id')) || null;

  protected readonly pkg = signal<TravelPackage | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly readOnly = computed(() => this.pkg()?.status === 'Archived');
  protected readonly hotels = toSignal(this.api.getHotels(undefined, true).pipe(catchError(() => of([] as TravelHotel[]))), { initialValue: [] as TravelHotel[] });
  protected readonly visaTypes = toSignal(this.config.getVisaTypes().pipe(catchError(() => of([] as TravelVisaType[]))), { initialValue: [] as TravelVisaType[] });

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    packageType: ['Umrah' as TravelPackageType],
    visaTypeId: [null as number | null],
    durationNights: [14 as number | null, [Validators.required, Validators.min(1)]],
    nightsMakkah: [0 as number | null, Validators.min(0)],
    nightsMadinah: [0 as number | null, Validators.min(0)],
    currencyCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    inclusions: [''],
    exclusions: [''],
    stays: this.fb.array([] as ReturnType<TravelPackageEditorComponent['stayGroup']>[]),
    prices: this.fb.array([] as ReturnType<TravelPackageEditorComponent['priceGroup']>[])
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly nightsMismatch = computed(() => {
    const v = this.value();
    return Number(v.nightsMakkah ?? 0) + Number(v.nightsMadinah ?? 0) > Number(v.durationNights ?? 0);
  });
  protected readonly stayNights = computed(() => (this.value().stays ?? []).reduce((n, s) => n + Number(s?.nights ?? 0), 0));
  private readonly usedRoomTypes = computed(() => (this.value().prices ?? []).map(p => p?.roomType as TravelRoomType));
  protected readonly duplicateRoomTypes = computed(() => new Set(this.usedRoomTypes()).size !== this.usedRoomTypes().length);
  protected readonly unusedRoomTypes = computed(() => this.roomTypes.filter(t => !this.usedRoomTypes().includes(t)));

  get stays(): FormArray { return this.form.controls.stays as FormArray; }
  get prices(): FormArray { return this.form.controls.prices as FormArray; }

  constructor() {
    if (this.id) this.load(this.id);
    else this.config.getSettings().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef))
      .subscribe(s => { if (s?.defaultCurrencyCode && !this.form.controls.currencyCode.value) this.form.controls.currencyCode.setValue(s.defaultCurrencyCode); });
  }

  private stayGroup(hotelId: number | null = null, nights: number | null = null) {
    return this.fb.group({ hotelId: [hotelId, Validators.required], nights: [nights, [Validators.required, Validators.min(1)]] });
  }
  private priceGroup(roomType: TravelRoomType, adult: number | null = null, child: number | null = null, infant: number | null = null) {
    return this.fb.group({
      roomType: [roomType, Validators.required],
      adultPrice: [adult, [Validators.required, Validators.min(0)]],
      childPrice: [child ?? 0, [Validators.required, Validators.min(0)]],
      infantPrice: [infant ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  protected addStay(): void { this.stays.push(this.stayGroup()); }
  protected addPrice(): void { const t = this.unusedRoomTypes()[0]; if (t) this.prices.push(this.priceGroup(t)); }
  protected moveStay(i: number, delta: number): void {
    const c = this.stays.at(i);
    this.stays.removeAt(i);
    this.stays.insert(i + delta, c);
  }

  private load(id: number): void {
    this.api.getPackage(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: p => this.patch(p),
      error: err => this.loadError.set(apiErrorMessage(err, 'Could not load the package.'))
    });
  }

  private patch(p: TravelPackage): void {
    this.pkg.set(p);
    this.stays.clear();
    [...p.stays].sort((a, b) => a.sequence - b.sequence).forEach(s => this.stays.push(this.stayGroup(s.hotelId, s.nights)));
    this.prices.clear();
    p.prices.forEach(x => this.prices.push(this.priceGroup(x.roomType, x.adultPrice, x.childPrice, x.infantPrice)));
    this.form.patchValue({
      code: p.code, name: p.name, packageType: p.packageType, visaTypeId: p.visaTypeId, durationNights: p.durationNights,
      nightsMakkah: p.nightsMakkah, nightsMadinah: p.nightsMadinah, currencyCode: p.currencyCode,
      inclusions: p.inclusions ?? '', exclusions: p.exclusions ?? ''
    });
    this.form.markAsPristine();
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.nightsMismatch() || this.duplicateRoomTypes()) {
      this.saveError.set('Check the highlighted fields.');
      return;
    }
    const v = this.form.getRawValue();
    const dto: SaveTravelPackage = {
      code: v.code!.trim().toUpperCase(), name: v.name!.trim(), packageType: v.packageType!, visaTypeId: v.visaTypeId,
      durationNights: Number(v.durationNights), nightsMakkah: Number(v.nightsMakkah ?? 0), nightsMadinah: Number(v.nightsMadinah ?? 0),
      currencyCode: v.currencyCode!.trim().toUpperCase(), inclusions: v.inclusions?.trim() || null, exclusions: v.exclusions?.trim() || null,
      stays: (v.stays as { hotelId: number; nights: number }[]).map(s => ({ hotelId: Number(s.hotelId), nights: Number(s.nights) })),
      prices: (v.prices as { roomType: TravelRoomType; adultPrice: number; childPrice: number; infantPrice: number }[]).map(p => ({
        roomType: p.roomType, adultPrice: Number(p.adultPrice), childPrice: Number(p.childPrice), infantPrice: Number(p.infantPrice)
      }))
    };
    this.busy.set(true);
    this.saveError.set(null);
    (this.id ? this.api.updatePackage(this.id, dto) : this.api.createPackage(dto)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => {
        this.busy.set(false);
        this.toast.success('Package saved.');
        if (this.id) this.load(this.id); else this.router.navigate(['/travel/packages', id]);
      },
      error: err => { this.busy.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the package.')); }
    });
  }

  protected publish(): void {
    if (this.form.dirty) { this.toast.error('Save your changes before publishing.'); return; }
    this.run(this.api.publish(this.id!), 'Package published.');
  }

  protected async archive(): Promise<void> {
    if (!(await this.confirm.open({ variant: 'warning', title: 'Archive package', message: 'Archived packages cannot be used for new groups or edited.', confirmText: 'Archive' }))) return;
    this.run(this.api.archive(this.id!), 'Package archived.');
  }

  protected async remove(): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete package "${this.pkg()?.name}"?`))) return;
    this.busy.set(true);
    this.api.deletePackage(this.id!).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Package deleted.'); this.router.navigate(['/travel/packages']); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not delete the package.')); }
    });
  }

  protected back(): void { this.router.navigate(['/travel/packages']); }

  private run(obs: ReturnType<TravelPackageService['publish']>, ok: string): void {
    this.busy.set(true);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.toast.success(ok); this.load(this.id!); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'The action was refused.')); }
    });
  }
}
