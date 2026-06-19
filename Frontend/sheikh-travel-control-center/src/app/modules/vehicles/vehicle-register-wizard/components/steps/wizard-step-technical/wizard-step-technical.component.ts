import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AbstractControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { UiInputComponent } from '../../../../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../../../shared/components/ui/types/ui.types';
import { FuelTypeSegmentComponent } from '../../fuel-type-segment/fuel-type-segment.component';
import { todayDateInputValue } from '../../../../../../core/utils/date-input.util';

@Component({
  selector: 'app-wizard-step-technical',
  standalone: true,
  imports: [ReactiveFormsModule, UiInputComponent, UiSelectComponent, FuelTypeSegmentComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6" [formGroup]="form()">
      <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
        <h2 class="mb-1 text-lg font-semibold text-fleet-text">Technical Specifications</h2>
        <p class="mb-5 text-sm text-fleet-text-muted">Engine, fuel, and compliance expiry dates.</p>

        <div class="mb-4">
          <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">Fuel Type</span>
          <app-fuel-type-segment formControlName="fuelType" />
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <ui-input
            formControlName="fuelAverage"
            label="Fuel Economy (km/L)"
            type="number"
            [required]="true"
            [error]="controlError('fuelAverage')" />
          <ui-input
            formControlName="seatingCapacity"
            label="Seating Capacity"
            type="number"
            [required]="true"
            [error]="controlError('seatingCapacity')" />
          <ui-input formControlName="engineNo" label="Engine Number" [required]="true" [error]="controlError('engineNo')" />
          <ui-input formControlName="chassisNo" label="Chassis Number" [required]="true" [error]="controlError('chassisNo')" />
          <ui-select
            formControlName="purchaseCurrencyCode"
            label="Purchase Currency"
            [options]="currencyOptions()"
            [searchable]="true"
            [required]="true"
            [error]="controlError('purchaseCurrencyCode')"
            searchPlaceholder="Search currencies…" />
          <ui-input
            formControlName="purchasePrice"
            label="Purchase Amount"
            type="number"
            [required]="true"
            [hint]="purchasePriceHint()"
            [error]="controlError('purchasePrice')" />
          <ui-select
            formControlName="branchId"
            label="Branch"
            [options]="branchOptions()"
            [searchable]="true"
            [required]="true"
            [error]="controlError('branchId')"
            searchPlaceholder="Search branches…" />
        </div>
      </section>

      <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
        <h3 class="mb-4 text-base font-semibold text-fleet-text">Compliance Expiry Dates</h3>
        <div class="grid gap-4 md:grid-cols-2">
          <label class="block">
            <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">Insurance Expiry</span>
            <input type="date" formControlName="insuranceExpiryDate" [min]="minDateFor(form().get('insuranceExpiryDate')?.value)" class="w-full rounded-sm border border-fleet-border px-3 py-2.5 text-sm" />
          </label>
          <label class="block">
            <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">Registration Expiry</span>
            <input type="date" formControlName="registrationExpiryDate" [min]="minDateFor(form().get('registrationExpiryDate')?.value)" class="w-full rounded-sm border border-fleet-border px-3 py-2.5 text-sm" />
          </label>
          <label class="block">
            <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">Road Tax Expiry</span>
            <input type="date" formControlName="roadTaxExpiryDate" [min]="minDateFor(form().get('roadTaxExpiryDate')?.value)" class="w-full rounded-sm border border-fleet-border px-3 py-2.5 text-sm" />
          </label>
          <label class="block">
            <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">Fitness Expiry</span>
            <input type="date" formControlName="fitnessExpiryDate" [min]="minDateFor(form().get('fitnessExpiryDate')?.value)" class="w-full rounded-sm border border-fleet-border px-3 py-2.5 text-sm" />
          </label>
        </div>
      </section>
    </div>
  `
})
export class WizardStepTechnicalComponent {
  readonly form = input.required<FormGroup>();
  readonly branchOptions = input.required<UiSelectOption[]>();
  readonly currencyOptions = input.required<UiSelectOption[]>();
  readonly selectedCurrency = input<string | null | undefined>('');
  readonly isEditMode = input(false);

  private readonly today = todayDateInputValue();

  readonly purchasePriceHint = computed(() => {
    const currency = String(this.selectedCurrency() ?? '').trim();
    return currency ? `Enter amount in ${currency}` : 'Select a currency first';
  });

  minDateFor(existingValue: string | null | undefined): string {
    if (this.isEditMode() && existingValue) {
      return existingValue < this.today ? existingValue : this.today;
    }
    return this.today;
  }

  controlError(name: string): string | undefined {
    const control = this.form().get(name);
    if (!control || !this.shouldShow(control)) return undefined;
    if (!control.errors) return undefined;

    if (control.hasError('required')) {
      return name === 'purchaseCurrencyCode'
        ? 'Select a currency before entering the purchase amount.'
        : 'This field is required.';
    }
    if (control.hasError('maxlength')) return 'Maximum length exceeded.';
    if (control.hasError('min')) {
      return name === 'seatingCapacity'
        ? 'Seating capacity must be at least 1.'
        : 'Value must be greater than zero.';
    }
    return undefined;
  }

  private shouldShow(control: AbstractControl): boolean {
    return control.touched || control.dirty;
  }
}
