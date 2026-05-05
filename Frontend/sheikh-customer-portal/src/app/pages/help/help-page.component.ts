import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-help-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">API &amp; testing</h1>
        <p class="mt-1 text-sm text-slate-600">
          The customer portal talks to the same .NET API as the staff app. Anonymous booking endpoints live under
          <code class="rounded bg-surface-alt px-1 font-mono text-xs">/api/customer-portal</code>; staff endpoints require a
          JWT from <code class="rounded bg-surface-alt px-1 font-mono text-xs">/api/auth/login</code>.
        </p>
      </div>

      <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-3">
        <h2 class="text-sm font-bold text-slate-900">Postman collection</h2>
        <p class="text-sm text-slate-600">
          A full collection (auth, bookings, payments, routes, and the public customer-portal folder) ships with this app.
          Download it and import into Postman. Set the collection variable <code class="font-mono text-xs">baseUrl</code>
          to your API origin (for example <code class="font-mono text-xs">http://localhost:5082</code>), run
          <strong>Auth → Login</strong> to fill <code class="font-mono text-xs">accessToken</code>, and set
          <code class="font-mono text-xs">portalPhone</code> for phone-scoped portal calls.
        </p>
        <a
          class="inline-flex rounded-xl bg-primary-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-primary-700"
          href="postman/SheikhTravelSystem.API.postman_collection.json"
          download="SheikhTravelSystem.API.postman_collection.json"
        >
          Download Postman collection
        </a>
        <p class="text-xs text-slate-500">
          File path in repo:
          <code class="font-mono">Backend/docs/SheikhTravelSystem.API.postman_collection.json</code>
          (kept in sync with the copy under <code class="font-mono">public/postman/</code>).
        </p>
      </section>

      <section class="rounded-2xl border border-stroke bg-surface p-5 shadow-card space-y-2">
        <h2 class="text-sm font-bold text-slate-900">Google Maps</h2>
        <p class="text-sm text-slate-600">
          Route preview on <strong>Book a ride</strong> uses the Directions service. Create a browser API key, enable
          <em>Maps JavaScript API</em> and <em>Directions API</em>, then set <code class="font-mono text-xs">googleMapsApiKey</code>
          in <code class="font-mono text-xs">environment.development.ts</code> (or production environment) and rebuild.
        </p>
      </section>

      <a routerLink="/dashboard" class="text-sm font-semibold text-primary-600 underline">Back to dashboard</a>
    </div>
  `
})
export class HelpPageComponent {}
