import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { take } from 'rxjs/operators';
import { CustomerSessionService } from '../../core/services/customer-session.service';
import { PortalApiService } from '../../core/services/portal-api.service';
import { PhoneDigitsOnlyDirective } from '../../shared/directives/phone-digits-only.directive';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, PhoneDigitsOnlyDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50 px-4 py-8">
      <div class="mx-auto max-w-md space-y-6">
        <div>
          <a routerLink="/" class="text-sm font-semibold text-primary-600 underline">← Home</a>
          <h1 class="mt-4 text-2xl font-bold tracking-tight text-slate-900">Sign in</h1>
          <p class="mt-1 text-sm text-slate-600">
            Verify your phone with a one-time code. Dev mode uses OTP
            <code class="rounded bg-slate-100 px-1 font-mono text-xs">123456</code> from server config.
          </p>
        </div>

        @if (session.isAuthenticated()) {
          <div class="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
            Signed in as <strong>{{ session.fullName() }}</strong> ({{ session.phone() }}).
          </div>
        }

        <form [formGroup]="form" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-card space-y-4">
          <div>
            <label class="text-xs font-semibold uppercase text-slate-500">Full name</label>
            <input type="text" formControlName="fullName" class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm" />
          </div>
          <div>
            <label class="text-xs font-semibold uppercase text-slate-500">Phone</label>
            <input type="tel" formControlName="phone" class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm" />
          </div>

          @if (otpSent()) {
            <div>
              <label class="text-xs font-semibold uppercase text-slate-500">OTP code</label>
              <input
                type="text"
                formControlName="otpCode"
                maxlength="6"
                class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm tracking-widest"
              />
            </div>
          }

          @if (info()) {
            <p class="text-sm text-slate-600">{{ info() }}</p>
          }
          @if (error()) {
            <p class="text-sm text-rose-700">{{ error() }}</p>
          }

          @if (!otpSent()) {
            <button
              type="button"
              (click)="sendOtp()"
              [disabled]="busy()"
              class="w-full rounded-xl bg-primary-600 py-3 text-sm font-bold text-white disabled:opacity-50"
            >
              {{ busy() ? 'Sending…' : 'Send OTP' }}
            </button>
          } @else {
            <button
              type="button"
              (click)="verifyOtp()"
              [disabled]="busy()"
              class="w-full rounded-xl bg-primary-600 py-3 text-sm font-bold text-white disabled:opacity-50"
            >
              {{ busy() ? 'Verifying…' : 'Verify & sign in' }}
            </button>
          }
        </form>

        @if (session.isAuthenticated()) {
          <section [formGroup]="notifyForm" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-card space-y-3">
            <h2 class="text-sm font-bold text-slate-900">Notifications</h2>
            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" [formControl]="notifyForm.controls.smsEnabled" />
              SMS updates (booking &amp; driver)
            </label>
            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" [formControl]="notifyForm.controls.emailEnabled" />
              Email receipts
            </label>
            @if (notifyForm.controls.emailEnabled.value) {
              <input
                type="email"
                formControlName="email"
                placeholder="you@example.com"
                class="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm"
              />
            }
            <button
              type="button"
              (click)="saveNotifications()"
              class="w-full rounded-xl border border-primary-200 py-2 text-sm font-semibold text-primary-800"
            >
              Save notification preferences
            </button>
          </section>

          <button
            type="button"
            (click)="logout()"
            class="w-full rounded-xl border border-slate-200 bg-white py-2.5 text-sm font-semibold text-slate-800"
          >
            Sign out
          </button>
          <a routerLink="/dashboard" class="block text-center text-sm font-semibold text-primary-600 underline"
            >Go to dashboard</a
          >
        }
      </div>
    </div>
  `
})
export class ProfilePageComponent {
  private readonly fb = inject(FormBuilder);
  readonly session = inject(CustomerSessionService);
  private readonly api = inject(PortalApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly busy = signal(false);
  readonly otpSent = signal(false);
  readonly error = signal<string | null>(null);
  readonly info = signal<string | null>(null);

  readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(20)]],
    otpCode: ['', [Validators.minLength(6), Validators.maxLength(6)]]
  });

  readonly notifyForm = this.fb.group({
    smsEnabled: [true],
    emailEnabled: [false],
    email: ['']
  });

  constructor() {
    const n = this.session.fullName();
    const p = this.session.phone();
    if (n) this.form.patchValue({ fullName: n });
    if (p) this.form.patchValue({ phone: p });
    if (this.session.isAuthenticated()) {
      this.api
        .getNotificationPreferences()
        .pipe(take(1))
        .subscribe({
          next: (prefs) =>
            this.notifyForm.patchValue({
              smsEnabled: prefs.smsEnabled,
              emailEnabled: prefs.emailEnabled,
              email: prefs.email ?? ''
            })
        });
    }
  }

  saveNotifications(): void {
    if (!this.session.isAuthenticated()) return;
    this.api
      .updateNotificationPreferences({
        smsEnabled: !!this.notifyForm.value.smsEnabled,
        emailEnabled: !!this.notifyForm.value.emailEnabled,
        email: this.notifyForm.value.email?.trim() || null
      })
      .pipe(take(1))
      .subscribe({
        next: () => this.info.set('Notification preferences saved.')
      });
  }

  sendOtp(): void {
    this.error.set(null);
    this.info.set(null);
    if (this.form.controls.phone.invalid) {
      this.form.controls.phone.markAsTouched();
      return;
    }
    this.busy.set(true);
    this.api
      .sendOtp(this.form.value.phone!.trim())
      .pipe(take(1))
      .subscribe({
        next: (res) => {
          this.busy.set(false);
          this.otpSent.set(true);
          this.info.set(res.message);
        },
        error: () => {
          this.busy.set(false);
          this.error.set('Could not send OTP. Try again.');
        }
      });
  }

  verifyOtp(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.api
      .verifyOtp(
        this.form.value.phone!.trim(),
        this.form.value.otpCode!.trim(),
        this.form.value.fullName!.trim()
      )
      .pipe(take(1))
      .subscribe({
        next: (res) => {
          this.busy.set(false);
          this.session.setSession(res.phone, res.fullName, res.accessToken);
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/dashboard';
          void this.router.navigateByUrl(returnUrl);
        },
        error: () => {
          this.busy.set(false);
          this.error.set('Invalid or expired OTP.');
        }
      });
  }

  logout(): void {
    this.session.clear();
    this.otpSent.set(false);
    void this.router.navigate(['/profile']);
  }
}
