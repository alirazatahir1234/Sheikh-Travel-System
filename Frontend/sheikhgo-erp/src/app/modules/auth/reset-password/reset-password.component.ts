import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss']
})
export class ResetPasswordComponent implements OnInit {
  form: FormGroup;
  loading = false;
  hidePassword = true;
  token = '';
  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirm: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    if (!this.token) {
      this.toast.error('This reset link is missing a token.');
    }
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (!this.token) {
      this.toast.error('This reset link is invalid.');
      return;
    }
    if (this.form.invalid) {
      this.toast.warning('Enter a valid password (min 6 characters).');
      return;
    }
    const password = this.form.value.password as string;
    const confirm = this.form.value.confirm as string;
    if (password !== confirm) {
      this.toast.warning('Passwords do not match.');
      return;
    }

    this.loading = true;
    this.auth.resetPassword(this.token, password).subscribe({
      next: res => {
        this.loading = false;
        if (res?.success === false) {
          this.toast.error(res.message || 'Could not reset password.');
          return;
        }
        this.toast.success(res?.message || 'Password updated.');
        this.router.navigate(['/auth/login']);
      },
      error: err => {
        this.loading = false;
        this.toast.error(err?.error?.message || 'Could not reset password.');
      }
    });
  }

  backToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
