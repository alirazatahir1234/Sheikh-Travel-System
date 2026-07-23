import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
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
    private platform: PlatformService,
    private router: Router,
    private toast: UiToastService
  ) {
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
    this.auth.login(this.form.value).pipe(
      switchMap(user =>
        this.platform.getMyWorkspace().pipe(
          catchError(() => of(null)),
          switchMap(ws => {
            if (ws?.homeRoute) this.auth.setHomeRoute(ws.homeRoute);
            return this.platform.getMySecuritySummary().pipe(
              catchError(() => of(null)),
              switchMap(security => {
                this.auth.applySecuritySessionPolicy(security);
                return of(user);
              })
            );
          })
        )
      )
    ).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate([this.auth.getHomeRoute()]);
      },
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
