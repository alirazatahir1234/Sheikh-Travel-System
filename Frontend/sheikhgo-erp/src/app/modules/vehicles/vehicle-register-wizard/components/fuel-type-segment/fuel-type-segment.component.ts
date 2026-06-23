import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { FuelType } from '../../../../../core/models/vehicle.model';

@Component({
  selector: 'app-fuel-type-segment',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => FuelTypeSegmentComponent),
    multi: true
  }],
  template: `
    <div class="flex rounded-md border border-fleet-border bg-fleet-surface-muted p-1">
      @for (opt of options; track opt.value) {
        <button
          type="button"
          class="flex-1 rounded-sm px-3 py-2 text-sm font-medium transition-colors"
          [class.bg-white]="value === opt.value"
          [class.text-fleet-primary]="value === opt.value"
          [class.shadow-sm]="value === opt.value"
          [class.text-fleet-text-muted]="value !== opt.value"
          (click)="select(opt.value)">
          {{ opt.label }}
        </button>
      }
    </div>
  `
})
export class FuelTypeSegmentComponent implements ControlValueAccessor {
  readonly label = input<string>();

  readonly options = [
    { value: String(FuelType.Diesel), label: 'Diesel' },
    { value: String(FuelType.Petrol), label: 'Petrol' },
    { value: String(FuelType.CNG), label: 'CNG' }
  ];

  value = String(FuelType.Petrol);
  disabled = false;
  private onChange: (v: string) => void = () => {};
  private onTouched: () => void = () => {};

  select(v: string): void {
    if (this.disabled) return;
    this.value = v;
    this.onChange(v);
    this.onTouched();
  }

  writeValue(v: string | null): void {
    this.value = v ?? String(FuelType.Petrol);
  }

  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled = isDisabled; }
}
