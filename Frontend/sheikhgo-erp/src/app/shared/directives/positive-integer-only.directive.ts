import {
  Directive,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';
import { NgControl } from '@angular/forms';
import { Subscription } from 'rxjs';
import {
  blockNonDigitIntegerKey,
  digitsOnlyIntegerInput,
  parseOptionalPositiveInt
} from '../../core/utils/integer-input.util';

const MAX_DIGITS = 9;

/**
 * Restricts inputs to non-negative integers. Empty input maps to null (unlimited).
 * Keeps the DOM value in sync when the form control is patched externally (e.g. plan defaults).
 */
@Directive({
  selector: 'input[appPositiveInteger]',
  standalone: true,
  host: {
    inputmode: 'numeric',
    pattern: '[0-9]*',
    autocomplete: 'off',
    maxlength: `${MAX_DIGITS}`
  }
})
export class PositiveIntegerOnlyDirective implements OnInit, OnDestroy {
  private readonly ngControl = inject(NgControl, { optional: true, self: true });
  private readonly el = inject(ElementRef<HTMLInputElement>);
  private sub?: Subscription;

  ngOnInit(): void {
    const control = this.ngControl?.control;
    if (!control) return;

    this.writeDom(control.value);
    this.sub = control.valueChanges.subscribe(value => this.writeDom(value));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    blockNonDigitIntegerKey(event);
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = event.clipboardData?.getData('text') ?? '';
    this.applyDigits(digitsOnlyIntegerInput(pasted).slice(0, MAX_DIGITS));
  }

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.applyDigits(digitsOnlyIntegerInput(input.value).slice(0, MAX_DIGITS));
  }

  @HostListener('blur')
  onBlur(): void {
    const control = this.ngControl?.control;
    if (!control) return;
    const parsed = parseOptionalPositiveInt(this.el.nativeElement.value);
    if (control.value !== parsed) {
      control.setValue(parsed, { emitEvent: true });
    }
    this.writeDom(parsed);
  }

  private applyDigits(digits: string): void {
    const input = this.el.nativeElement;
    if (input.value !== digits) {
      input.value = digits;
    }

    const parsed = parseOptionalPositiveInt(digits);
    const control = this.ngControl?.control;
    if (control && control.value !== parsed) {
      control.setValue(parsed, { emitEvent: true });
    }
  }

  private writeDom(value: unknown): void {
    const input = this.el.nativeElement;
    const display =
      value === null || value === undefined || value === ''
        ? ''
        : String(parseOptionalPositiveInt(value as string | number) ?? '');
    if (input.value !== display) {
      input.value = display;
    }
  }
}
