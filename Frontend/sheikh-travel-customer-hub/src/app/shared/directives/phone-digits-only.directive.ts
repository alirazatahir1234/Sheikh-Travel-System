import { Directive, HostListener, inject } from '@angular/core';
import { NgControl } from '@angular/forms';
import { blockNonDigitPhoneKey, digitsOnlyPhoneInput } from '../../core/utils/phone-input.util';

@Directive({
  selector: 'input[appPhoneDigitsOnly], input[type="tel"]',
  standalone: true,
  host: {
    inputmode: 'numeric',
    pattern: '[0-9]*',
    autocomplete: 'tel'
  }
})
export class PhoneDigitsOnlyDirective {
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    blockNonDigitPhoneKey(event);
  }

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = digitsOnlyPhoneInput(input.value);
    if (input.value === digits) return;

    input.value = digits;
    const control = this.ngControl?.control;
    if (control && control.value !== digits) {
      control.setValue(digits);
    }
  }
}
