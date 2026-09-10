import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';

@Component({
  selector: 'app-delete-account-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './delete-account.page.html',
  styleUrl: './delete-account.page.scss',
})
export class DeleteAccountPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly status = signal<{ type: 'ok' | 'err'; text: string } | null>(null);

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    organization: [''],
    reason: [''],
    confirm: [false, Validators.requiredTrue],
    website: [''], // honeypot
  });

  get mailtoHref(): string {
    const subject = encodeURIComponent('SheikhGo Fleet – Account Deletion Request');
    return `mailto:${this.brand.privacyEmail}?subject=${subject}`;
  }

  ngOnInit(): void {
    this.seo.set(
      'Request Account Deletion',
      'Request deletion of your SheikhGo Fleet account and associated personal information.',
      '/delete-account',
    );
  }

  submit(): void {
    this.status.set(null);
    if (this.form.controls.website.value) {
      this.status.set({
        type: 'ok',
        text: 'Your deletion request has been recorded. We will follow up by email if needed.',
      });
      this.form.reset({ confirm: false, website: '' });
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.status.set({ type: 'err', text: 'Please complete the required fields.' });
      return;
    }

    this.submitting.set(true);
    const v = this.form.getRawValue();
    const body = [
      'SheikhGo Fleet – Account Deletion Request',
      '',
      `Name: ${v.fullName}`,
      `Email: ${v.email}`,
      `Phone: ${v.phone || '—'}`,
      `Organization: ${v.organization || '—'}`,
      '',
      'Details:',
      v.reason || '—',
    ].join('\n');

    const mailto = `mailto:${this.brand.privacyEmail}?subject=${encodeURIComponent(
      'SheikhGo Fleet – Account Deletion Request',
    )}&body=${encodeURIComponent(body)}`;

    window.location.href = mailto;
    this.submitting.set(false);
    this.status.set({
      type: 'ok',
      text: 'Your email app should open with a pre-filled deletion request. Send it to complete the request.',
    });
  }
}
