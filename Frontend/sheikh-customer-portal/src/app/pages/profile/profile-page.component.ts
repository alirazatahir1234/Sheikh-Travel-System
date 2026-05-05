import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CustomerSessionService } from '../../core/services/customer-session.service';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-md space-y-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Profile</h1>
        <p class="mt-1 text-sm text-slate-600">
          We use your phone to load your bookings (no staff password). Stored only in this browser session.
        </p>
      </div>
      <form [formGroup]="form" (ngSubmit)="save()" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-card space-y-4">
        <div>
          <label class="text-xs font-semibold uppercase text-slate-500">Full name</label>
          <input type="text" formControlName="fullName" class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm" />
        </div>
        <div>
          <label class="text-xs font-semibold uppercase text-slate-500">Phone</label>
          <input type="tel" formControlName="phone" class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm" />
        </div>
        @if (saved()) {
          <p class="text-sm font-medium text-emerald-700">Saved. You can open Dashboard or My bookings.</p>
        }
        <button
          type="submit"
          class="w-full rounded-xl bg-primary-600 py-3 text-sm font-bold text-white hover:bg-primary-700"
        >
          Save profile
        </button>
        <button
          type="button"
          (click)="logout()"
          class="w-full rounded-xl border border-slate-200 py-2.5 text-sm font-semibold text-slate-800"
        >
          Clear session
        </button>
      </form>
      <a routerLink="/dashboard" class="text-sm font-semibold text-primary-600 underline">Back to dashboard</a>
    </div>
  `
})
export class ProfilePageComponent {
  private readonly fb = inject(FormBuilder);
  readonly session = inject(CustomerSessionService);

  readonly saved = signal(false);

  readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(20)]]
  });

  constructor() {
    const n = this.session.fullName();
    const p = this.session.phone();
    if (n) this.form.patchValue({ fullName: n });
    if (p) this.form.patchValue({ phone: p });
  }

  save(): void {
    this.saved.set(false);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.session.setSession(this.form.value.phone!.trim(), this.form.value.fullName!.trim());
    this.saved.set(true);
  }

  logout(): void {
    this.session.clear();
    this.saved.set(false);
    this.form.reset();
  }
}
