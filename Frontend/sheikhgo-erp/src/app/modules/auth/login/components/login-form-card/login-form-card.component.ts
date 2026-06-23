import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { APP_LOGO_PATH, APP_PRODUCT_NAME } from '../../../../../core/constants/app-brand';

@Component({
  selector: 'app-login-form-card',
  templateUrl: './login-form-card.component.html',
  styleUrls: ['./login-form-card.component.scss']
})
export class LoginFormCardComponent {
  readonly logoPath = APP_LOGO_PATH;
  readonly productName = APP_PRODUCT_NAME;
  @Input({ required: true }) form!: FormGroup;
  @Input() loading = false;
  @Input() hidePassword = true;
  @Input() rememberMe = true;

  @Output() hidePasswordChange = new EventEmitter<boolean>();
  @Output() rememberMeChange = new EventEmitter<boolean>();
  @Output() submitLogin = new EventEmitter<void>();
  @Output() forgotPasswordClick = new EventEmitter<void>();
  @Output() socialLoginClick = new EventEmitter<'google' | 'microsoft'>();

  togglePassword(): void {
    this.hidePasswordChange.emit(!this.hidePassword);
  }

  onSubmit(): void {
    const email = (this.form.get('email')?.value || '').trim();
    this.form.patchValue({ email }, { emitEvent: false });
    this.form.markAllAsTouched();
    this.form.updateValueAndValidity({ emitEvent: false });
    this.submitLogin.emit();
  }
}
