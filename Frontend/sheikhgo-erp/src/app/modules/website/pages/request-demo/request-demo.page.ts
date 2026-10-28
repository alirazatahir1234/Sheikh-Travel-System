import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteLeadsService } from '../../core/leads.service';
import {
  NEXUS_PROMO_120S_CAPTIONS_VTT,
  NEXUS_PROMO_120S_SRC,
  NEXUS_PROMO_120S_TITLE,
} from '../../core/demo-media';
import { CHANNEL_C_CTAS } from '../../core/channel-c-solutions';
import { WebsiteGoogleAnalyticsService } from '../../core/website-google-analytics.service';
import { WebsitePlausibleAnalyticsService } from '../../core/website-plausible-analytics.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

export type LeadFormIntent = 'demo' | 'assessment';

@Component({
  selector: 'app-request-demo-page',
  standalone: true,
  imports: [ReactiveFormsModule, WebsiteTranslatePipe, RouterLink],
  templateUrl: './request-demo.page.html',
  styleUrl: './request-demo.page.scss',
})
export class RequestDemoPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly leads = inject(WebsiteLeadsService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly ga = inject(WebsiteGoogleAnalyticsService);
  private readonly plausible = inject(WebsitePlausibleAnalyticsService);

  readonly promoSrc = NEXUS_PROMO_120S_SRC;
  readonly promoCaptionsSrc = NEXUS_PROMO_120S_CAPTIONS_VTT;
  readonly promoTitle = NEXUS_PROMO_120S_TITLE;
  readonly ctas = CHANNEL_C_CTAS;

  readonly intent = signal<LeadFormIntent>('demo');
  readonly isAssessment = computed(() => this.intent() === 'assessment');

  readonly submitting = signal(false);
  readonly status = signal<{ type: 'ok' | 'err'; text: string } | null>(null);

  readonly fleetSizes = ['1–10', '11–50', '51–200', '200+'];

  readonly interestOptions = [
    'Fleet Management',
    'GPS Tracking',
    'GPS Alerts',
    'Vehicle Rental',
    'Transportation Operations',
    'AI Fleet Insights',
    'Live Tracking',
    'Enterprise',
  ];

  readonly demoSteps = [
    'Submit your demo request',
    'Our team contacts you',
    'Discovery walkthrough of Nexus',
    'Scope & commercial discussion',
    'Pilot or kickoff with SheikhGo',
  ];

  readonly assessmentSteps = [
    'Submit fleet assessment details',
    'We review fleet size and workflows',
    'Structured gap / fit discussion',
    'Recommended next step (demo or pilot)',
    'Optional paid campaign readiness',
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
    const dataIntent = this.route.snapshot.data['intent'] as LeadFormIntent | undefined;
    const queryIntent = this.route.snapshot.queryParamMap.get('intent');
    const resolved: LeadFormIntent =
      dataIntent === 'assessment' || queryIntent === 'assessment' ? 'assessment' : 'demo';
    this.intent.set(resolved);
    this.applySeo();
  }

  private applySeo(): void {
    if (this.isAssessment()) {
      this.seo.set(
        'Request a Fleet Assessment',
        'Request a structured SheikhGo Nexus fleet assessment — coverage, workflows, and fit before a demo or paid traffic.',
        '/fleet-assessment',
      );
    } else {
      this.seo.set(
        'Book a Demo',
        'Book a SheikhGo Nexus demo — fleet management, GPS tracking, operations, and AI insights for your team.',
        '/request-demo',
      );
    }
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
    const intentLabel = this.isAssessment() ? 'Fleet Assessment' : 'Book a Demo';
    let interestedProduct = interestsJoined || intentLabel;
    let message = raw.message.trim();

    if (this.isAssessment()) {
      message = [`[Fleet Assessment]`, message].filter(Boolean).join('\n\n');
    }

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
          this.trackConversion();
          this.status.set({
            type: 'ok',
            text: this.isAssessment()
              ? 'Fleet assessment request received. We will contact you soon.'
              : 'Demo request received. We will contact you soon.',
          });
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

  private trackConversion(): void {
    const event = this.isAssessment() ? 'fleet_assessment_submit' : 'demo_request_submit';
    const params = {
      form_intent: this.intent(),
      page_path: this.isAssessment() ? '/fleet-assessment' : '/request-demo',
    };
    this.ga.trackEvent(event, params);
    this.plausible.trackEvent(event, params);
  }
}
