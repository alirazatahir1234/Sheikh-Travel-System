import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';

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
  selectedLanguage = 'EN';
  darkMode = true;
  readonly year = new Date().getFullYear();
  readonly languages = ['EN', 'AR'];

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    // Pre-fill with admin credentials for easy access
    this.form = this.fb.group({
      email: ['admin@sheikhtravel.com', [Validators.required, Validators.email]],
      password: ['Pass@123', Validators.required]
    });

    if (this.auth.isLoggedIn()) {
      this.router.navigate(['/dashboard']);
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
    if (this.form.invalid) return;

    if (this.rememberMe) {
      localStorage.setItem('stb_last_email', this.form.get('email')?.value || '');
    } else {
      localStorage.removeItem('stb_last_email');
    }

    this.loading = true;
    this.auth.login(this.form.value).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => {
        this.loading = false;
        const message = err?.error?.message || err?.message || 'Invalid email or password';
        this.snackBar.open(message, 'Close', { duration: 4000 });
      }
    });
  }

  forgotPassword(): void {
    this.snackBar.open('Please contact your administrator to reset password.', 'Close', { duration: 3500 });
  }

  socialLogin(provider: 'google' | 'microsoft'): void {
    this.snackBar.open(`${provider[0].toUpperCase()}${provider.slice(1)} login is not configured yet.`, 'Close', {
      duration: 3000
    });
  }

  toggleTheme(): void {
    this.darkMode = !this.darkMode;
  }
}
