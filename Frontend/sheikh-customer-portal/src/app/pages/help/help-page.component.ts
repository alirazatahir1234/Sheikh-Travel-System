import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { take } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PortalApiService } from '../../core/services/portal-api.service';

@Component({
  selector: 'app-help-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Help &amp; support</h1>
        <p class="mt-1 text-sm text-slate-600">
          Book airport and city transfers, track your driver, and pay online when enabled.
        </p>
      </div>

      <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-3">
        <h2 class="text-sm font-bold text-slate-900">WhatsApp</h2>
        <p class="text-sm text-slate-600">Message our team for pickup changes or urgent help.</p>
        <a
          class="inline-flex rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700"
          [href]="whatsAppLink()"
          target="_blank"
          rel="noopener noreferrer"
        >
          Chat on WhatsApp
        </a>
      </section>

      <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-3">
        <h2 class="text-sm font-bold text-slate-900">Sign in</h2>
        <p class="text-sm text-slate-600">
          Use phone OTP on
          <a routerLink="/profile" class="font-semibold text-primary-600 underline">Profile</a>. Dev OTP:
          <code class="font-mono text-xs">123456</code>.
        </p>
      </section>

      @if (gatewayMessage()) {
        <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-2">
          <h2 class="text-sm font-bold text-slate-900">Online payments</h2>
          <p class="text-sm text-slate-600">{{ gatewayMessage() }}</p>
        </section>
      }

      <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-3">
        <h2 class="text-sm font-bold text-slate-900">Postman collection</h2>
        <a
          class="inline-flex rounded-xl bg-primary-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-primary-700"
          href="postman/SheikhTravelSystem.API.postman_collection.json"
          download="SheikhTravelSystem.API.postman_collection.json"
        >
          Download API collection
        </a>
      </section>

      <a routerLink="/" class="text-sm font-semibold text-primary-600 underline">Back to home</a>
    </div>
  `
})
export class HelpPageComponent {
  private readonly api = inject(PortalApiService);
  readonly gatewayMessage = signal<string | null>(null);

  constructor() {
    this.api
      .getPaymentGatewayInfo()
      .pipe(take(1))
      .subscribe({
        next: (g) => this.gatewayMessage.set(`${g.provider}: ${g.message}`)
      });
  }

  whatsAppLink(): string {
    const n = environment.whatsAppNumber.replace(/\D/g, '');
    return `https://wa.me/${n}?text=${encodeURIComponent('Hello Sheikh Travel, I need help with my booking.')}`;
  }
}
