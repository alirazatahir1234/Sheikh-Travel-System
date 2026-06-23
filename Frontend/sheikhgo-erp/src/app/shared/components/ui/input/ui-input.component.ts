import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  signal
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiInputType } from '../types/ui.types';

let uiInputId = 0;

@Component({
  selector: 'ui-input',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UiInputComponent),
      multi: true
    }
  ],
  template: `
    <label class="block">
      @if (label()) {
        <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">
          {{ label() }}
          @if (required()) { <span class="text-fleet-error">*</span> }
        </span>
      }

      <span
        class="relative flex items-center rounded-sm border bg-white transition-colors"
        [class.border-fleet-border]="!error()"
        [class.border-fleet-error]="!!error()"
        [class.bg-fleet-surface-muted]="disabled()"
        [class.focus-within:border-fleet-primary]="!error()">
        @if (prefixIcon()) {
          <mat-icon class="pointer-events-none ml-3 text-fleet-text-muted" style="font-size:20px;width:20px;height:20px;">{{ prefixIcon() }}</mat-icon>
        }

        <input
          [id]="inputId"
          [type]="type()"
          [value]="value()"
          [placeholder]="placeholder()"
          [disabled]="disabled()"
          [attr.aria-invalid]="!!error()"
          class="w-full bg-transparent px-3 py-2.5 text-sm text-fleet-text placeholder:text-fleet-text-muted/70 focus:outline-none disabled:cursor-not-allowed"
          [class.pl-2]="!!prefixIcon()"
          (input)="onInput($event)"
          (blur)="onTouched()" />

        @if (suffixIcon()) {
          <mat-icon class="pointer-events-none mr-3 text-fleet-text-muted" style="font-size:20px;width:20px;height:20px;">{{ suffixIcon() }}</mat-icon>
        }
      </span>

      @if (error()) {
        <span class="mt-1 block text-[12px] font-medium text-fleet-error">{{ error() }}</span>
      } @else if (hint()) {
        <span class="mt-1 block text-[12px] text-fleet-text-muted">{{ hint() }}</span>
      }
    </label>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class UiInputComponent implements ControlValueAccessor {
  readonly label = input<string>();
  readonly placeholder = input('');
  readonly type = input<UiInputType>('text');
  readonly prefixIcon = input<string>();
  readonly suffixIcon = input<string>();
  readonly error = input<string>();
  readonly hint = input<string>();
  readonly required = input(false);

  readonly inputId = `ui-input-${uiInputId++}`;

  protected readonly value = signal<string>('');
  protected readonly disabled = signal(false);

  private onChange: (value: string) => void = () => {};
  protected onTouched: () => void = () => {};

  protected readonly hasIcons = computed(() => !!this.prefixIcon() || !!this.suffixIcon());

  onInput(event: Event): void {
    const next = (event.target as HTMLInputElement).value;
    this.value.set(next);
    this.onChange(next);
  }

  writeValue(value: string | number | null): void {
    this.value.set(value == null ? '' : String(value));
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
