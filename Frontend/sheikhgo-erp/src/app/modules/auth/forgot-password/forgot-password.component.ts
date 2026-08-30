import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss']
})
export class ForgotPasswordComponent {
  form: FormGroup;
  loading = false;
  sent = false;
  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.toast.warning('Enter a valid email address.');
      return;
    }
    this.loading = true;
    this.auth.forgotPassword(this.form.value.email).subscribe({
      next: res => {
        this.loading = false;
        this.sent = true;
        this.toast.success(res?.message || 'Check your email for further instructions.');
      },
      error: err => {
        this.loading = false;
        // Still show success-style messaging for enumeration safety when API returns 200;
        // on network/5xx show a soft error.
        this.toast.error(err?.error?.message || 'Could not send reset email. Try again shortly.');
      }
    });
  }

  backToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
