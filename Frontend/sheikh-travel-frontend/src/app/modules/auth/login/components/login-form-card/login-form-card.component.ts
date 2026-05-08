import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup } from '@angular/forms';

@Component({
  selector: 'app-login-form-card',
  templateUrl: './login-form-card.component.html',
  styleUrls: ['./login-form-card.component.scss']
})
export class LoginFormCardComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() loading = false;
  @Input() hidePassword = true;
  @Input() rememberMe = true;
  @Input() selectedLanguage = 'EN';
  @Input() darkMode = true;

  @Output() hidePasswordChange = new EventEmitter<boolean>();
  @Output() rememberMeChange = new EventEmitter<boolean>();
  @Output() submitLogin = new EventEmitter<void>();
  @Output() forgotPasswordClick = new EventEmitter<void>();
  @Output() themeToggle = new EventEmitter<void>();
  @Output() socialLoginClick = new EventEmitter<'google' | 'microsoft'>();

  togglePassword(): void {
    this.hidePasswordChange.emit(!this.hidePassword);
  }
}
