import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SiteHeaderComponent } from './layout/site-header/site-header.component';
import { SiteFooterComponent } from './layout/site-footer/site-footer.component';

@Component({
  standalone: true,
  selector: 'app-website-shell',
  imports: [RouterOutlet, RouterLink, SiteHeaderComponent, SiteFooterComponent],
  template: `
    <div class="website-shell">
      <app-site-header />
      <main class="website-main">
        <router-outlet />
      </main>
      <app-site-footer />
      <div class="mobile-cta">
        <a routerLink="/request-demo" class="btn btn-primary">Request a Demo</a>
      </div>
    </div>
  `,
  styleUrl: './website-shell.component.scss',
})
export class WebsiteShellComponent {}
