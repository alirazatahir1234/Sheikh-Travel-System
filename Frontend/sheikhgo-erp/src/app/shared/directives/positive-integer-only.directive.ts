import { Directive, HostListener, inject } from '@angular/core';
import { NgControl } from '@angular/forms';
import {
  blockNonDigitIntegerKey,
  digitsOnlyIntegerInput,
  parseOptionalPositiveInt
} from '../../core/utils/integer-input.util';

/**
 * Restricts inputs to non-negative integers. Empty input maps to null (unlimited).
 */
@Directive({
  selector: 'input[appPositiveInteger]',
  standalone: true,
  host: {
    inputmode: 'numeric',
    pattern: '[0-9]*',
    autocomplete: 'off'
  }
})
export class PositiveIntegerOnlyDirective {
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    blockNonDigitIntegerKey(event);
  }

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = digitsOnlyIntegerInput(input.value);
    if (input.value !== digits) {
      input.value = digits;
    }

    const parsed = parseOptionalPositiveInt(digits);
    const control = this.ngControl?.control;
    if (control && control.value !== parsed) {
      control.setValue(parsed);
    }
  }
}
