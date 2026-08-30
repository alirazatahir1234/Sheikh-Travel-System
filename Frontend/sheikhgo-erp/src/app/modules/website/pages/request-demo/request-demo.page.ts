import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteLeadsService } from '../../core/leads.service';

@Component({
  selector: 'app-request-demo-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './request-demo.page.html',
  styleUrl: './request-demo.page.scss',
})
export class RequestDemoPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly leads = inject(WebsiteLeadsService);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly status = signal<{ type: 'ok' | 'err'; text: string } | null>(null);

  readonly fleetSizes = ['1–10', '11–50', '51–200', '200+'];

  readonly interestOptions = [
    'GPS Tracking',
    'Fleet Management',
    'Trip Management',
    'Vehicle Rental',
    'Travel Management',
    'AI',
    'Enterprise',
  ];

  readonly steps = [
    'Submit your request',
    'Our team contacts you',
    'Product demonstration',
    'Fleet requirements analysis',
    'SheikhGo setup',
  ];

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    company: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    country: [''],
    vehicleCount: [''],
    interests: this.fb.nonNullable.control<string[]>([]),
    message: [''],
    website: [''],
  });

  ngOnInit(): void {
    this.seo.set('Request a Demo', 'Request a SheikhGo product demonstration for your fleet.', '/request-demo');
  }

  toggleInterest(value: string, checked: boolean): void {
    const current = [...this.form.controls.interests.value];
    if (checked) {
      if (!current.includes(value)) current.push(value);
    } else {
      const i = current.indexOf(value);
      if (i >= 0) current.splice(i, 1);
    }
    this.form.controls.interests.setValue(current);
  }

  isInterestChecked(value: string): boolean {
    return this.form.controls.interests.value.includes(value);
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.status.set({ type: 'err', text: 'Please complete the required fields.' });
      return;
    }

    const raw = this.form.getRawValue();
    const interestsJoined = raw.interests.join(', ');
    let interestedProduct = interestsJoined || 'Full Platform';
    let message = raw.message.trim();

    if (interestedProduct.length > 120) {
      const overflow = interestedProduct.slice(120);
      interestedProduct = interestedProduct.slice(0, 117) + '…';
      message = [message, `Also interested in: ${overflow}`].filter(Boolean).join('\n\n');
    }

    this.submitting.set(true);
    this.status.set(null);

    this.leads
      .requestDemo({
        name: `${raw.firstName} ${raw.lastName}`.trim(),
        company: raw.company,
        email: raw.email,
        phone: raw.phone || undefined,
        country: raw.country || undefined,
        vehicleCount: raw.vehicleCount || undefined,
        interestedProduct,
        message: message || undefined,
        website: raw.website || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.status.set({ type: 'ok', text: 'Demo request received. We will contact you soon.' });
          this.form.reset({ interests: [], website: '', vehicleCount: '' });
        },
        error: err => {
          this.submitting.set(false);
          this.status.set({
            type: 'err',
            text: err?.error?.message || 'Could not submit your request. Please try again or email sales.',
          });
        },
      });
  }
}
