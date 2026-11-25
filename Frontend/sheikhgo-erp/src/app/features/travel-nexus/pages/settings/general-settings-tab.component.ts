import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelSettings } from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';

const PREFIX = [Validators.required, Validators.maxLength(10), Validators.pattern(/^[A-Za-z0-9-]+$/)];

@Component({
  selector: 'app-general-settings-tab',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadError()) {
      <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div>
    } @else if (loading()) {
      <div class="tn-card tn-card-body space-y-3">@for (s of [1,2]; track s) { <div class="h-10 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
    } @else {
      <form class="tn-card tn-card-body space-y-5" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-grid">
          <div class="tn-field">
            <label for="gs-cur">Default currency (ISO 4217)</label>
            <input id="gs-cur" class="tn-input uppercase" formControlName="defaultCurrencyCode" maxlength="3" placeholder="PKR" />
            <span class="tn-hint">Used as the default on invoices. Leave blank until you set up finance.</span>
            @if (form.controls.defaultCurrencyCode.invalid) { <span class="tn-error">Three letters, e.g. PKR or SAR.</span> }
          </div>
          <div class="tn-field">
            <label for="gs-pp">Passport expiry warning (days)</label>
            <input id="gs-pp" class="tn-input" type="number" min="0" max="730" formControlName="passportExpiryWarningDays" />
            <span class="tn-hint">Passports expiring within this window are flagged. Most countries require 180.</span>
          </div>
        </div>
        <h4 class="tn-section-title">Numbering prefixes</h4>
        <div class="tn-grid">
          <div class="tn-field"><label for="gs-enq">Enquiry</label><input id="gs-enq" class="tn-input uppercase" formControlName="enquiryPrefix" maxlength="10" /></div>
          <div class="tn-field"><label for="gs-vc">Visa case</label><input id="gs-vc" class="tn-input uppercase" formControlName="visaCasePrefix" maxlength="10" /></div>
          <div class="tn-field"><label for="gs-inv">Invoice</label><input id="gs-inv" class="tn-input uppercase" formControlName="invoicePrefix" maxlength="10" /></div>
        </div>
        @if (form.invalid && form.touched) { <span class="text-xs text-red-600">Prefixes: 1–10 letters, digits or '-'.</span> }
        @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
        <div class="tn-actions">
          <button mat-flat-button color="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save settings' }}</button>
        </div>
      </form>
    }
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GeneralSettingsTabComponent {
  private readonly service = inject(TravelConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    defaultCurrencyCode: ['', Validators.pattern(/^([A-Za-z]{3})?$/)],
    passportExpiryWarningDays: [180, [Validators.required, Validators.min(0), Validators.max(730)]],
    enquiryPrefix: ['ENQ', PREFIX],
    visaCasePrefix: ['VC', PREFIX],
    invoicePrefix: ['TINV', PREFIX]
  });

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.service.getSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => { this.patch(s); this.loading.set(false); },
      error: err => { this.loadError.set(apiErrorMessage(err, 'Could not load settings.')); this.loading.set(false); }
    });
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const dto: TravelSettings = {
      defaultCurrencyCode: v.defaultCurrencyCode?.trim() ? v.defaultCurrencyCode.trim().toUpperCase() : null,
      passportExpiryWarningDays: Number(v.passportExpiryWarningDays),
      enquiryPrefix: v.enquiryPrefix!.trim().toUpperCase(),
      visaCasePrefix: v.visaCasePrefix!.trim().toUpperCase(),
      invoicePrefix: v.invoicePrefix!.trim().toUpperCase()
    };
    this.saving.set(true);
    this.saveError.set(null);
    this.service.updateSettings(dto).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => { this.saving.set(false); this.patch(saved); this.toast.success('Settings saved.'); },
      error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save settings.')); }
    });
  }

  private patch(s: TravelSettings): void {
    this.form.reset({
      defaultCurrencyCode: s.defaultCurrencyCode ?? '',
      passportExpiryWarningDays: s.passportExpiryWarningDays,
      enquiryPrefix: s.enquiryPrefix,
      visaCasePrefix: s.visaCasePrefix,
      invoicePrefix: s.invoicePrefix
    });
  }
}
