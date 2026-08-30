import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteLeadsService } from '../../core/leads.service';
import { WEBSITE_BRAND } from '../../core/brand';

@Component({
  selector: 'app-contact-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './contact.page.html',
  styleUrl: './contact.page.scss',
})
export class ContactPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly leads = inject(WebsiteLeadsService);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly status = signal<{ type: 'ok' | 'err'; text: string } | null>(null);

  readonly interests = [
    'Fleet Management',
    'GPS Tracking',
    'Vehicle Rental',
    'Travel Management',
    'Trip Management',
    'Reports',
    'Enterprise',
    'AI',
    'Other',
  ];

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    company: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    country: [''],
    fleetSize: [''],
    interestedIn: ['Fleet Management'],
    message: ['', Validators.required],
    website: [''],
  });

  ngOnInit(): void {
    this.seo.set('Contact', 'Contact SheikhGo sales and support for fleet and travel management.', '/contact');
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.status.set({ type: 'err', text: 'Please complete the required fields.' });
      return;
    }
    this.submitting.set(true);
    this.status.set(null);
    this.leads.contact(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.status.set({ type: 'ok', text: 'Thanks — our team will get back to you shortly.' });
        this.form.reset({ interestedIn: 'Fleet Management', website: '' });
      },
      error: err => {
        this.submitting.set(false);
        this.status.set({
          type: 'err',
          text: err?.error?.message || 'Could not send your message. Please email us directly.',
        });
      },
    });
  }
}
