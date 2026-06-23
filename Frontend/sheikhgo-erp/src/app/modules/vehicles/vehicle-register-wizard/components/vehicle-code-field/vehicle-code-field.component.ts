import { ChangeDetectionStrategy, Component, forwardRef, input, output } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiButtonComponent } from '../../../../../shared/components/ui/button/ui-button.component';

@Component({
  selector: 'app-vehicle-code-field',
  standalone: true,
  imports: [MatIconModule, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => VehicleCodeFieldComponent),
    multi: true
  }],
  template: `
    <label class="block">
      <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">
        {{ label() || 'Vehicle Code' }}
      </span>
      <div class="flex gap-2">
        <div class="relative flex flex-1 items-center rounded-sm border border-fleet-border bg-fleet-surface-muted px-3 py-2.5">
          <mat-icon class="mr-2 text-fleet-text-muted" style="font-size:18px;width:18px;height:18px;">tag</mat-icon>
          <input
            type="text"
            readonly
            [value]="value"
            class="w-full bg-transparent text-sm text-fleet-text focus:outline-none" />
        </div>
        <ui-button variant="outline" icon="refresh" (clicked)="regenerate.emit()">Regenerate</ui-button>
      </div>
      @if (hint()) {
        <span class="mt-1 block text-[12px] text-fleet-text-muted">{{ hint() }}</span>
      }
    </label>
  `
})
export class VehicleCodeFieldComponent implements ControlValueAccessor {
  readonly label = input<string>();
  readonly hint = input('Auto-generated fleet identifier');
  readonly regenerate = output<void>();

  value = '';
  private onChange: (v: string) => void = () => {};

  writeValue(v: string | null): void { this.value = v ?? ''; }
  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }
  registerOnTouched(): void {}
  setDisabledState(): void {}
}
