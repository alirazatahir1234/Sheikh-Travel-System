import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CookieConsentService } from '../../core/cookie-consent.service';

@Component({
  selector: 'app-cookie-consent',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './cookie-consent.component.html',
  styleUrl: './cookie-consent.component.scss',
})
export class CookieConsentComponent implements OnInit {
  readonly consent = inject(CookieConsentService);

  readonly draft = signal({
    analytics: false,
    functional: false,
    marketing: false,
  });

  ngOnInit(): void {
    const c = this.consent.consent();
    this.draft.set({
      analytics: c.analytics,
      functional: c.functional,
      marketing: c.marketing,
    });
    this.consent.applyConsentScripts();
  }

  openPreferences(): void {
    const c = this.consent.consent();
    this.draft.set({
      analytics: c.analytics,
      functional: c.functional,
      marketing: c.marketing,
    });
    this.consent.openPreferences();
  }

  toggle(key: 'analytics' | 'functional' | 'marketing'): void {
    this.draft.update(d => ({ ...d, [key]: !d[key] }));
  }

  save(): void {
    this.consent.savePreferences(this.draft());
  }

  acceptAll(): void {
    this.consent.acceptAll();
  }

  rejectNonEssential(): void {
    this.consent.rejectNonEssential();
  }
}
