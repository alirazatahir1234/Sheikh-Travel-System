import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  form: FormGroup;
  loading = false;
  hidePassword = true;
  rememberMe = true;
  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private toast: UiToastService
  ) {
    // Pre-fill with admin credentials for easy access
    this.form = this.fb.group({
      email: ['admin@sheikhtravel.com', [Validators.required, Validators.email]],
      password: ['Pass@123', Validators.required]
    });

    if (this.auth.isLoggedIn()) {
      this.router.navigate([this.auth.getHomeRoute()]);
    }
  }

  ngOnInit(): void {
    const savedEmail = localStorage.getItem('stb_last_email');
    if (savedEmail) {
      this.form.patchValue({ email: savedEmail });
      this.rememberMe = true;
    }
  }

  submit(): void {
    this.form.markAllAsTouched();
    this.form.updateValueAndValidity({ emitEvent: false });
    if (this.form.invalid) {
      this.toast.warning('Please enter a valid email and password.');
      return;
    }

    if (this.rememberMe) {
      localStorage.setItem('stb_last_email', this.form.get('email')?.value || '');
    } else {
      localStorage.removeItem('stb_last_email');
    }

    this.loading = true;
    this.auth.login(this.form.value).subscribe({
      next: user => this.router.navigate([user.roles?.includes('Driver') ? '/my-trips' : '/dashboard']),
      error: err => {
        this.loading = false;
        const message = err?.error?.message || err?.message || 'Invalid email or password';
        this.toast.error(message);
      }
    });
  }

  forgotPassword(): void {
    this.toast.warning('Please contact your administrator to reset password.');
  }

  socialLogin(provider: 'google' | 'microsoft'): void {
    this.toast.info(`${provider[0].toUpperCase()}${provider.slice(1)} login is not configured yet.`);
  }
}
